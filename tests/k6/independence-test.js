import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Counter, Trend } from 'k6/metrics';

// Custom metrics for service independence validation
const transactionAvailability = new Rate('transaction_availability');
const consolidationStressSuccess = new Rate('consolidation_stress_success');
const transactionLatencyDuringStress = new Trend('transaction_latency_during_stress');
const independenceViolations = new Counter('independence_violations');
const authErrors = new Counter('auth_errors');

export const options = {
  scenarios: {
    // Phase 1: Baseline - Both services working normally
    baseline: {
      executor: 'constant-vus',
      vus: 5,
      duration: '1m',
      tags: { phase: 'baseline' },
    },

    // Phase 2: Simulate consolidation service overload
    consolidation_overload: {
      executor: 'constant-arrival-rate',
      rate: 80, // High RPS to stress consolidations
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 20,
      maxVUs: 40,
      tags: { phase: 'consolidation_overload' },
      startTime: '1m',
      exec: 'overloadConsolidations'
    },

    // Phase 3: Critical test - Transactions during consolidation stress
    transaction_independence: {
      executor: 'constant-vus',
      vus: 8, // Moderate load on transactions
      duration: '2m',
      tags: { phase: 'independence_test' },
      startTime: '1m',
      exec: 'testTransactionResilience'
    },

    // Phase 4: Recovery validation
    recovery: {
      executor: 'constant-vus',
      vus: 3,
      duration: '30s',
      tags: { phase: 'recovery' },
      startTime: '3m',
    }
  },

  thresholds: {
    // CRITICAL: Transaction service must stay available (95%+) during consolidation stress
    'transaction_availability{phase:independence_test}': ['rate>=0.95'],

    // Independence violations must be minimal
    'independence_violations': ['count<3'],

    // Transaction latency shouldn't degrade excessively during stress
    'transaction_latency_during_stress': ['p(95)<5000'],

    // Authentication reliability
    'auth_errors': ['count<5'],
  }
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8000';

const AUTH_CONFIG = {
  client_id: 'cash-flow-api',
  client_secret: 'cash-flow-secret-2024',
  username: 'merchant1',
  password: 'merchant123'
};

// Token management per VU
let accessToken = null;
let tokenExpiry = 0;

function generateTransaction() {
  return {
    Amount: Math.floor(Math.random() * 3000) + 200,
    Type: Math.random() > 0.6 ? 'CREDITO' : 'DEBITO',
    Description: `Independence test ${Date.now()}-${Math.random().toString(36).substr(2, 5)}`
  };
}

function getRandomMerchantId() {
  const merchants = ['merchant_1', 'merchant_2'];
  return merchants[Math.floor(Math.random() * merchants.length)];
}

function getCurrentDate() {
  return new Date().toISOString().split('T')[0];
}

function getAuthToken() {
  if (accessToken && Date.now() < (tokenExpiry - 30000)) {
    return accessToken;
  }

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

  if (authResponse.status === 200) {
    const authData = authResponse.json();
    accessToken = authData.access_token;
    tokenExpiry = Date.now() + (authData.expires_in * 1000);
    return accessToken;
  } else {
    authErrors.add(1);
    console.log(`Auth failed: ${authResponse.status}`);
    return null;
  }
}

// Shared data structure to ensure consolidation queries target merchants with transactions
const merchantTransactionTracker = {};

// Default function - Mixed baseline and recovery testing
export default function () {
  if (Math.random() > 0.3) {
    testTransactionBaseline(); // Creates data
  } else {
    testConsolidationBaseline(); // Queries correlated data
  }
  sleep(Math.random() * 0.5 + 0.2);
}

// Aggressive consolidation testing to create service stress
export function overloadConsolidations() {
  const token = getAuthToken();
  if (!token) {
    consolidationStressSuccess.add(false);
    return;
  }

  const currentDate = getCurrentDate();

  // Prioritize merchants with known transactions, fallback to random
  const availableMerchants = Object.keys(merchantTransactionTracker).filter(
    merchant => merchantTransactionTracker[merchant] === currentDate
  );

  const merchantId = availableMerchants.length > 0
    ? availableMerchants[Math.floor(Math.random() * availableMerchants.length)]
    : getRandomMerchantId();

  const response = http.get(
    `${BASE_URL}/api/v1/merchants/${merchantId}/consolidations/daily?date=${currentDate}`,
    {
      headers: { 'Authorization': `Bearer ${token}` },
      tags: {
        api: 'consolidations',
        phase: 'consolidation_overload',
        has_tracked_data: availableMerchants.length > 0 ? 'yes' : 'no'
      },
      timeout: '3s'
    }
  );

  // Success defined as proper response (200 or 404)
  const success = response.status === 200 || response.status === 404;
  consolidationStressSuccess.add(success);

  check(response, {
    'consolidation overload test responds': (r) => r.status !== 0,
    'consolidation handles stress appropriately': (r) => r.status === 200 || r.status === 404 || r.status === 503 || r.status === 429,
  });

  // No sleep - maintain stress
}

// Critical test: Verify transaction independence during consolidation stress
export function testTransactionResilience() {
  const token = getAuthToken();
  if (!token) {
    transactionAvailability.add(0);
    independenceViolations.add(1);
    return;
  }

  const merchantId = getRandomMerchantId();
  const transaction = generateTransaction();
  const currentDate = getCurrentDate();
  const startTime = Date.now();

  const response = http.post(
    `${BASE_URL}/api/v1/merchants/${merchantId}/transactions`,
    JSON.stringify(transaction),
    {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
      },
      tags: {
        api: 'transactions',
        phase: 'independence_test'
      },
      timeout: '8s'
    }
  );

  const latency = Date.now() - startTime;
  const success = response.status >= 200 && response.status < 300;

  // Track successful transactions for future consolidation queries
  if (success) {
    merchantTransactionTracker[merchantId] = currentDate;
  }

  transactionAvailability.add(success ? 1 : 0);
  transactionLatencyDuringStress.add(latency);

  // Check for independence violations
  if (!success) {
    if (response.status === 0 || response.status >= 500 || response.status === 503) {
      independenceViolations.add(1);
      console.log(`INDEPENDENCE VIOLATION: Transaction failed (${response.status}) during consolidation stress`);
    }
  }

  const validationResult = check(response, {
    'transaction available during consolidation stress': (r) => r.status >= 200 && r.status < 300,
    'transaction responds during consolidation stress': (r) => r.status !== 0,
    'transaction maintains independence': (r) => !(r.status === 0 || r.status >= 500),
    'transaction latency acceptable during stress': (r) => r.timings.duration < 8000,
  });

  // Handle auth errors
  if (response.status === 401 || response.status === 403) {
    accessToken = null;
  }

  sleep(0.3);
}

function testTransactionBaseline() {
  const token = getAuthToken();
  if (!token) return;

  const merchantId = getRandomMerchantId();
  const transaction = generateTransaction();
  const currentDate = getCurrentDate();

  const response = http.post(
    `${BASE_URL}/api/v1/merchants/${merchantId}/transactions`,
    JSON.stringify(transaction),
    {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
      },
      tags: { api: 'transactions', phase: 'baseline' }
    }
  );

  // Track successful transactions for correlation
  if (response.status >= 200 && response.status < 300) {
    merchantTransactionTracker[merchantId] = currentDate;
  }

  check(response, {
    'baseline transaction success': (r) => r.status >= 200 && r.status < 300,
  });

  if (response.status === 401 || response.status === 403) {
    accessToken = null;
  }
}

function testConsolidationBaseline() {
  const token = getAuthToken();
  if (!token) return;

  const currentDate = getCurrentDate();

  // Use merchants that have transactions on current date
  const availableMerchants = Object.keys(merchantTransactionTracker).filter(
    merchant => merchantTransactionTracker[merchant] === currentDate
  );

  const merchantId = availableMerchants.length > 0
    ? availableMerchants[Math.floor(Math.random() * availableMerchants.length)]
    : getRandomMerchantId(); // Fallback for early test execution

  const response = http.get(
    `${BASE_URL}/api/v1/merchants/${merchantId}/consolidations/daily?date=${currentDate}`,
    {
      headers: { 'Authorization': `Bearer ${token}` },
      tags: {
        api: 'consolidations',
        phase: 'baseline',
        has_tracked_data: availableMerchants.length > 0 ? 'yes' : 'no'
      }
    }
  );

  check(response, {
    'baseline consolidation appropriate': (r) => r.status === 200 || r.status === 404,
  });

  if (response.status === 401 || response.status === 403) {
    accessToken = null;
  }
}

export function setup() {
  console.log('=== Service Independence Validation Test ===');
  console.log(`Target: ${BASE_URL}`);
  console.log('');
  console.log('Business Requirement:');
  console.log('  "O serviço de controle de lançamento não deve ficar');
  console.log('   indisponível se o sistema de consolidado diário cair"');
  console.log('');
  console.log('Test Plan:');
  console.log('  1. Baseline (1min): Normal operation of both services');
  console.log('  2. Stress Phase (2min): Overload consolidations with 80 RPS');
  console.log('  3. Independence Test (2min): Verify transactions remain available');
  console.log('  4. Recovery (30s): Validate system recovery');
  console.log('');

  // Pre-test validation
  console.log('Validating system readiness...');

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

    const token = authResponse.json().access_token;

    // Test transaction endpoint
    const transactionTest = http.post(
      `${BASE_URL}/api/v1/merchants/merchant_1/transactions`,
      JSON.stringify({
        Amount: 100,
        Type: 'CREDITO',
        Description: 'Setup test'
      }),
      {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        }
      }
    );

    if (transactionTest.status >= 200 && transactionTest.status < 300) {
      console.log('✓ Transaction Service: READY');
    } else {
      console.log(`⚠ Transaction Service: Issues (${transactionTest.status})`);
    }

    // Test consolidation endpoint
    const consolidationTest = http.get(
      `${BASE_URL}/api/v1/merchants/merchant_1/consolidations/daily?date=${getCurrentDate()}`,
      {
        headers: { 'Authorization': `Bearer ${token}` }
      }
    );

    if (consolidationTest.status === 200 || consolidationTest.status === 404) {
      console.log('✓ Consolidation Service: READY');
    } else {
      console.log(`⚠ Consolidation Service: Issues (${consolidationTest.status})`);
    }

  } else {
    console.log(`✗ Authentication: FAILED (${authResponse.status})`);
  }

  console.log('');
  console.log('Starting independence test...');
  return { startTime: Date.now() };
}

export function teardown(data) {
  const duration = (Date.now() - data.startTime) / 1000;
  console.log('');
  console.log('=== Service Independence Test Results ===');
  console.log(`Duration: ${duration.toFixed(1)}s`);
  console.log('');
  console.log('Critical Success Criteria:');
  console.log('  ✓ transaction_availability{phase:independence_test} ≥ 95%');
  console.log('  ✓ independence_violations < 3');
  console.log('  ✓ Transaction latency acceptable during stress');
  console.log('');
  console.log('If all thresholds PASS: Service independence requirement is satisfied');
  console.log('If thresholds FAIL: System architecture needs improvement');
  console.log('');
}
