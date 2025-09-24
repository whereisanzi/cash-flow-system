import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom metrics for business requirements validation
const transactionAvailability = new Rate('transaction_service_availability');
const consolidationErrorRate = new Rate('consolidation_error_rate');
const transactionLatency = new Trend('transaction_latency');
const consolidationLatency = new Trend('consolidation_latency');
const transactionFailures = new Counter('transaction_failures');
const consolidationFailures = new Counter('consolidation_failures');
const authErrors = new Counter('auth_errors');

// Test scenarios to validate NFRs
export const options = {
  scenarios: {
    // Scenario 1: Peak consolidation load (50 RPS requirement)
    peak_consolidations: {
      executor: 'constant-arrival-rate',
      rate: 50, // 50 requests per second - exact business requirement
      timeUnit: '1s',
      duration: '3m', // 3 minutes of sustained peak load
      preAllocatedVUs: 30,
      maxVUs: 60,
      tags: { test_type: 'peak_consolidations' },
    },

    // Scenario 2: Transaction service resilience test
    // This runs simultaneously to validate transactions don't fail when consolidations are stressed
    transaction_resilience: {
      executor: 'constant-vus',
      vus: 5, // Lower VUs but constant throughout consolidation stress
      duration: '3m30s', // Runs 30s longer than peak consolidations
      tags: { test_type: 'transaction_resilience' },
      startTime: '0s', // Starts at same time as consolidation peak
    },

    // Scenario 3: Post-peak validation
    // Validates system recovery after peak load
    post_peak_validation: {
      executor: 'constant-vus',
      vus: 10,
      duration: '1m',
      tags: { test_type: 'post_peak' },
      startTime: '3m30s',
    }
  },

  // Business requirement thresholds
  thresholds: {
    // REQUIREMENT: Max 5% error rate during 50 RPS consolidation peak
    'consolidation_error_rate{test_type:peak_consolidations}': ['rate<=0.05'],

    // REQUIREMENT: Transaction service must remain available (max 2% failure)
    'transaction_service_availability{test_type:transaction_resilience}': ['rate>=0.98'],

    // Performance thresholds during peak load
    'http_req_duration{test_type:peak_consolidations}': ['p(95)<3000'],
    'http_req_duration{test_type:transaction_resilience}': ['p(95)<2000'],

    // Overall system health
    'http_req_failed': ['rate<0.1'],
    'auth_errors': ['count<10'],

    // Consolidation specific validations
    'consolidation_failures': ['count<150'], // 5% of 3000 requests (50 RPS * 60s * 3min)

    // Transaction service must not fail excessively during consolidation stress
    'transaction_failures': ['count<10'],
  }
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8000';

const AUTH_CONFIG = {
  client_id: 'cash-flow-api',
  client_secret: 'cash-flow-secret-2024',
  username: 'merchant1',
  password: 'merchant123'
};

// Token management (per VU)
let accessToken = null;
let tokenExpiry = 0;

function getAuthToken() {
  // Check if token is still valid (with 30s buffer)
  if (accessToken && Date.now() < (tokenExpiry - 30000)) {
    return accessToken;
  }

  const authStart = Date.now();
  const authResponse = http.post(
    `${BASE_URL}/api/v1/auth/token`,
    {
      grant_type: 'password',
      client_id: AUTH_CONFIG.client_id,
      client_secret: AUTH_CONFIG.client_secret,
      username: AUTH_CONFIG.username,
      password: AUTH_CONFIG.password
    },
    {
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      tags: { api: 'auth' }
    }
  );

  if (check(authResponse, {
    'auth successful': (r) => r.status === 200,
    'auth has token': (r) => r.json('access_token') !== undefined,
  })) {
    const authData = authResponse.json();
    accessToken = authData.access_token;
    tokenExpiry = Date.now() + (authData.expires_in * 1000);
    return accessToken;
  } else {
    authErrors.add(1);
    console.log(`Auth failed: ${authResponse.status} - ${authResponse.body}`);
    return null;
  }
}

function generateTransaction() {
  return {
    Amount: Math.floor(Math.random() * 5000) + 500,
    Type: Math.random() > 0.6 ? 'CREDITO' : 'DEBITO',
    Description: `NFR test txn ${Math.random().toString(36).substr(2, 8)}`
  };
}

function getCurrentDate() {
  const now = new Date();
  return now.toISOString().split('T')[0];
}

function getRandomMerchantId() {
  // Use multiple merchants to simulate realistic load distribution
  const merchants = ['merchant_1', 'merchant_2', 'merchant_3'];
  return merchants[Math.floor(Math.random() * merchants.length)];
}

// Shared data structure to track which merchants have transactions on current date
// This ensures consolidation queries target merchants that should have data
const merchantTransactionTracker = {};

export default function () {
  const scenario = __ENV.K6_SCENARIO_NAME || 'default';

  if (scenario === 'peak_consolidations') {
    testConsolidationsUnderLoad();
  } else if (scenario === 'transaction_resilience') {
    testTransactionResilience();
  } else if (scenario === 'post_peak') {
    testPostPeakRecovery();
  } else {
    // Default mixed load - create transactions first to ensure data correlation
    if (Math.random() > 0.3) {
      testTransactionResilience(); // Creates transactions
    } else {
      testConsolidationsUnderLoad(); // Queries existing data
    }
  }

  sleep(Math.random() * 0.3 + 0.1);
}

function testConsolidationsUnderLoad() {
  const token = getAuthToken();
  if (!token) {
    consolidationFailures.add(1);
    consolidationErrorRate.add(1);
    return;
  }

  // Get a merchant that should have transactions on current date
  const currentDate = getCurrentDate();
  const availableMerchants = Object.keys(merchantTransactionTracker).filter(
    merchant => merchantTransactionTracker[merchant] === currentDate
  );

  // If no merchants have transactions yet, use any merchant (early in test)
  // or fall back to known merchants
  let merchantId;
  if (availableMerchants.length > 0) {
    merchantId = availableMerchants[Math.floor(Math.random() * availableMerchants.length)];
  } else {
    // Early in test or fallback - use merchant that likely has historical data
    merchantId = getRandomMerchantId();
  }

  const response = http.get(
    `${BASE_URL}/api/v1/merchants/${merchantId}/consolidations/daily?date=${currentDate}`,
    {
      headers: { 'Authorization': `Bearer ${token}` },
      tags: {
        api: 'consolidations',
        test_type: 'peak_consolidations',
        merchant: merchantId,
        date: currentDate,
        has_tracked_transactions: availableMerchants.length > 0 ? 'yes' : 'no'
      }
    }
  );

  const success = check(response, {
    'consolidation responds': (r) => r.status !== 0,
    'consolidation success or not found': (r) => r.status === 200 || r.status === 404,
    'consolidation within SLA': (r) => r.timings.duration < 5000,
    'consolidation not auth error': (r) => r.status !== 401 && r.status !== 403,
    'consolidation valid JSON on 200': (r) => {
      if (r.status === 200) {
        try {
          const data = r.json();
          return data.hasOwnProperty('merchantId') && data.hasOwnProperty('netBalance');
        } catch (e) {
          return false;
        }
      }
      return true; // 404s don't need valid JSON
    }
  });

  // Business rule: 404 is acceptable when querying merchants without transactions
  // Only count real errors (5xx, auth issues, timeouts, etc.) toward the 5% limit
  const isBusinessError = response.status !== 200 && response.status !== 404;

  if (isBusinessError) {
    consolidationFailures.add(1);
    if (response.status === 401 || response.status === 403) {
      console.log(`Consolidation auth error: ${response.status}`);
      accessToken = null;
    } else {
      console.log(`Consolidation error: ${response.status} for ${merchantId} on ${currentDate} - ${response.body?.substring(0, 100)}`);
    }
  }

  consolidationErrorRate.add(isBusinessError);
  consolidationLatency.add(response.timings.duration);
}

function testTransactionResilience() {
  const token = getAuthToken();
  if (!token) {
    transactionFailures.add(1);
    transactionAvailability.add(0); // Failed availability
    return;
  }

  const merchantId = getRandomMerchantId();
  const transaction = generateTransaction();
  const currentDate = getCurrentDate();

  const response = http.post(
    `${BASE_URL}/api/v1/merchants/${merchantId}/transactions`,
    JSON.stringify(transaction),
    {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
      tags: {
        api: 'transactions',
        test_type: 'transaction_resilience',
        merchant: merchantId
      }
    }
  );

  const success = check(response, {
    'transaction responds': (r) => r.status !== 0,
    'transaction created': (r) => r.status === 200 || r.status === 201,
    'transaction within SLA': (r) => r.timings.duration < 3000,
    'transaction not auth error': (r) => r.status !== 401 && r.status !== 403,
    'transaction returns valid data': (r) => {
      if (r.status === 200 || r.status === 201) {
        try {
          const data = r.json();
          return data.hasOwnProperty('id') && data.hasOwnProperty('merchantId');
        } catch (e) {
          return false;
        }
      }
      return false;
    }
  });

  // Track which merchants have transactions on current date
  if (success) {
    merchantTransactionTracker[merchantId] = currentDate;
  }

  if (!success) {
    transactionFailures.add(1);
    if (response.status === 401 || response.status === 403) {
      console.log(`Transaction auth error: ${response.status}`);
      accessToken = null;
    } else {
      console.log(`Transaction error: ${response.status} - ${response.body?.substring(0, 100)}`);
    }
  }

  // Track availability: 1 for success, 0 for failure
  transactionAvailability.add(success ? 1 : 0);
  transactionLatency.add(response.timings.duration);
}

function testPostPeakRecovery() {
  // Test both services to ensure system recovered after peak load
  if (Math.random() > 0.5) {
    testTransactionResilience();
  } else {
    testConsolidationsUnderLoad();
  }
}

export function setup() {
  console.log('=== Cash Flow NFR Validation Test ===');
  console.log(`Target: ${BASE_URL}`);
  console.log('');
  console.log('Business Requirements Being Validated:');
  console.log('1. Consolidation service: 50 RPS with max 5% error rate');
  console.log('2. Transaction service: Must remain available when consolidations stressed');
  console.log('');
  console.log('Test Strategy:');
  console.log('- Peak consolidations: 50 RPS for 3 minutes');
  console.log('- Concurrent transactions: Continuous load during consolidation stress');
  console.log('- Post-peak validation: System recovery verification');
  console.log('');

  // Validate both services are accessible
  const authResponse = http.post(
    `${BASE_URL}/api/v1/auth/token`,
    {
      grant_type: 'password',
      client_id: AUTH_CONFIG.client_id,
      client_secret: AUTH_CONFIG.client_secret,
      username: AUTH_CONFIG.username,
      password: AUTH_CONFIG.password
    },
    { headers: { 'Content-Type': 'application/x-www-form-urlencoded' } }
  );

  if (authResponse.status === 200) {
    console.log('✓ Authentication: READY');

    // Test consolidation endpoint
    const token = authResponse.json().access_token;
    const testConsolidation = http.get(
      `${BASE_URL}/api/v1/merchants/merchant_1/consolidations/daily?date=${getCurrentDate()}`,
      { headers: { 'Authorization': `Bearer ${token}` } }
    );

    if (testConsolidation.status === 200 || testConsolidation.status === 404) {
      console.log('✓ Consolidation API: READY');
    } else {
      console.log(`⚠ Consolidation API: Issues detected (${testConsolidation.status})`);
    }

    // Test transaction endpoint
    const testTransaction = {
      Amount: 100,
      Type: 'CREDITO',
      Description: 'Setup validation'
    };

    const transactionTest = http.post(
      `${BASE_URL}/api/v1/merchants/merchant_1/transactions`,
      JSON.stringify(testTransaction),
      {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        }
      }
    );

    if (transactionTest.status === 200 || transactionTest.status === 201) {
      console.log('✓ Transaction API: READY');
    } else {
      console.log(`⚠ Transaction API: Issues detected (${transactionTest.status})`);
    }

  } else {
    console.log(`✗ Authentication: FAILED (${authResponse.status})`);
  }

  console.log('');
  console.log('Starting NFR validation...');
  return { startTime: Date.now() };
}

export function teardown(data) {
  const duration = (Date.now() - data.startTime) / 1000;
  console.log('');
  console.log('=== NFR Validation Results ===');
  console.log(`Total Duration: ${duration.toFixed(1)}s`);
  console.log('');
  console.log('Key Metrics to Review:');
  console.log('1. consolidation_error_rate{test_type:peak_consolidations} - Must be ≤ 5%');
  console.log('2. transaction_service_availability{test_type:transaction_resilience} - Must be ≥ 98%');
  console.log('3. http_req_duration - Response times during peak load');
  console.log('');
  console.log('If thresholds PASSED: NFRs are satisfied');
  console.log('If thresholds FAILED: System needs optimization for production');
}
