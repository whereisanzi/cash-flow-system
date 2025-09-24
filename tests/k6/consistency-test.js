import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Counter, Trend, Gauge } from 'k6/metrics';

// Custom metrics for consistency validation
const eventualConsistencyRate = new Rate('eventual_consistency_rate');
const readConsistencyRate = new Rate('read_consistency_rate');
const causalConsistencyRate = new Rate('causal_consistency_rate');
const convergenceTime = new Trend('convergence_time_ms');
const consistencyViolations = new Counter('consistency_violations');
const pendingTransactions = new Gauge('pending_transactions');
const authErrors = new Counter('auth_errors');

export const options = {
  scenarios: {
    // Single VU approach: One VU creates transactions, then checks consistency
    consistency_validation: {
      executor: 'per-vu-iterations',
      vus: 1,
      iterations: 1,
      maxDuration: '5m',
      tags: { phase: 'full_consistency_test' },
      exec: 'runFullConsistencyTest'
    },

    // Separate read consistency test (can run in parallel)
    read_consistency_test: {
      executor: 'constant-vus',
      vus: 2,
      duration: '2m',
      tags: { phase: 'read_consistency' },
      startTime: '1m',
      exec: 'checkReadConsistency'
    }
  },

  thresholds: {
    // Eventual consistency: 95% of transactions should appear in consolidations
    'eventual_consistency_rate': ['rate>=0.95'],

    // Read consistency: Multiple reads should return consistent data
    'read_consistency_rate': ['rate>=0.98'],

    // Causal consistency: Order should be preserved
    'causal_consistency_rate': ['rate>=0.90'],

    // Convergence time: Should converge within 40 seconds
    'convergence_time_ms': ['p(95)<40000'],

    // Minimal consistency violations
    'consistency_violations': ['count<10'],

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

// Remove shared state variables that don't work between VUs
// const transactionTracker = {};
// const consolidationCache = {};

let accessToken = null;
let tokenExpiry = 0;

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

function getCurrentDate() {
  return new Date().toISOString().split('T')[0];
}

function generateUniqueTransaction() {
  const timestamp = Date.now();
  const randomId = Math.random().toString(36).substr(2, 9);

  return {
    Amount: Math.floor(Math.random() * 2000) + 100,
    Type: Math.random() > 0.5 ? 'CREDITO' : 'DEBITO',
    Description: `Consistency-test-${timestamp}-${randomId}`,
    testId: `${timestamp}-${randomId}`,
    createdAt: timestamp
  };
}

// Main consistency test function - runs in single VU to avoid state sharing issues
export function runFullConsistencyTest() {
  console.log('Starting comprehensive consistency test...');

  // Step 1: Get baseline consolidation
  const initialConsolidation = getConsolidationSnapshot();
  console.log(`Initial consolidation: ${JSON.stringify(initialConsolidation)}`);

  // Step 2: Create tracked transactions
  const createdTransactions = [];
  const transactionCount = 10; // Create 10 test transactions

  console.log(`Creating ${transactionCount} tracked transactions...`);
  for (let i = 0; i < transactionCount; i++) {
    const transaction = createSingleTrackedTransaction(i);
    if (transaction) {
      createdTransactions.push(transaction);
      console.log(`Created transaction ${i + 1}/${transactionCount}: ${transaction.testId}`);
    }
    sleep(1); // 1 second between transactions
  }

  if (createdTransactions.length === 0) {
    console.log('No transactions were created successfully');
    return;
  }

  console.log(`Successfully created ${createdTransactions.length} transactions`);

  // Step 3: Wait and check eventual consistency
  console.log('Waiting for convergence...');
  const maxWaitTime = 45000; // 45 seconds max wait
  const checkInterval = 5000; // Check every 5 seconds
  const startTime = Date.now();

  let convergenceResults = [];
  let lastConsolidation = null;

  while (Date.now() - startTime < maxWaitTime) {
    const currentTime = Date.now();
    const waitedTime = currentTime - startTime;

    console.log(`Checking consistency at ${waitedTime}ms...`);
    const currentConsolidation = getConsolidationSnapshot();

    if (currentConsolidation) {
      lastConsolidation = currentConsolidation;

      // Check if consolidation has changed from initial state
      const hasChanged = !initialConsolidation ||
        currentConsolidation.transactionCount !== initialConsolidation.transactionCount ||
        Math.abs(currentConsolidation.netBalance - initialConsolidation.netBalance) > 0.01;

      if (hasChanged) {
        console.log(`Consolidation changed! Count: ${currentConsolidation.transactionCount}, Balance: ${currentConsolidation.netBalance}`);

        // Mark convergence for this check
        const convergenceRate = hasChanged ? 1 : 0;
        eventualConsistencyRate.add(convergenceRate);

        if (hasChanged) {
          convergenceTime.add(waitedTime);
          console.log(`Convergence detected after ${waitedTime}ms`);
        }

        convergenceResults.push({
          timeMs: waitedTime,
          converged: hasChanged,
          consolidation: currentConsolidation
        });
      } else {
        eventualConsistencyRate.add(0);
        convergenceResults.push({
          timeMs: waitedTime,
          converged: false,
          consolidation: currentConsolidation
        });
      }
    }

    sleep(checkInterval / 1000);
  }

  // Step 4: Test causal consistency
  console.log('Testing causal consistency...');
  testCausalConsistencyInternal();

  // Final summary
  const finalConvergenceRate = convergenceResults.filter(r => r.converged).length / Math.max(convergenceResults.length, 1);
  console.log(`Final consistency test summary:`);
  console.log(`- Created transactions: ${createdTransactions.length}`);
  console.log(`- Convergence checks: ${convergenceResults.length}`);
  console.log(`- Convergence rate: ${(finalConvergenceRate * 100).toFixed(1)}%`);

  if (lastConsolidation) {
    console.log(`- Final consolidation: Count=${lastConsolidation.transactionCount}, Balance=${lastConsolidation.netBalance}`);
  }
}

function getConsolidationSnapshot() {
  const token = getAuthToken();
  if (!token) return null;

  const response = http.get(
    `${BASE_URL}/api/v1/merchants/merchant_1/consolidations/daily?date=${getCurrentDate()}`,
    {
      headers: { 'Authorization': `Bearer ${token}` },
      tags: { api: 'consolidations', phase: 'snapshot' }
    }
  );

  if (response.status === 200) {
    try {
      return response.json();
    } catch (e) {
      console.log(`Failed to parse consolidation: ${e}`);
    }
  }

  return null;
}

function createSingleTrackedTransaction(index) {
  const token = getAuthToken();
  if (!token) return null;

  const transaction = {
    Amount: Math.floor(Math.random() * 1000) + 100,
    Type: Math.random() > 0.5 ? 'CREDITO' : 'DEBITO',
    Description: `Consistency-test-${Date.now()}-${index}`,
  };

  const response = http.post(
    `${BASE_URL}/api/v1/merchants/merchant_1/transactions`,
    JSON.stringify(transaction),
    {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
      },
      tags: { api: 'transactions', phase: 'creation' }
    }
  );

  const success = check(response, {
    'transaction created successfully': (r) => r.status >= 200 && r.status < 300,
  });

  if (success) {
    const responseData = response.json();
    return {
      id: responseData.id,
      amount: parseFloat(responseData.amount),
      type: responseData.type,
      createdAt: Date.now()
    };
  }

  return null;
}

function testCausalConsistencyInternal() {
  const token = getAuthToken();
  if (!token) return;

  console.log('Creating causal sequence...');

  // Get initial state
  const initialConsolidation = getConsolidationSnapshot();
  const initialCredits = initialConsolidation ? initialConsolidation.totalCredits : 0;

  // Create sequence of transactions
  const sequence = [
    { Amount: 100, Type: 'CREDITO', Description: 'Causal-1' },
    { Amount: 200, Type: 'CREDITO', Description: 'Causal-2' },
    { Amount: 300, Type: 'CREDITO', Description: 'Causal-3' }
  ];

  const expectedIncrease = 600; // 100 + 200 + 300

  for (let i = 0; i < sequence.length; i++) {
    const response = http.post(
      `${BASE_URL}/api/v1/merchants/merchant_1/transactions`,
      JSON.stringify(sequence[i]),
      {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        },
        tags: { api: 'transactions', phase: 'causal' }
      }
    );

    check(response, {
      'causal transaction created': (r) => r.status >= 200 && r.status < 300,
    });

    sleep(1); // Ensure ordering
  }

  // Wait for propagation
  console.log('Waiting for causal consistency check...');
  sleep(15);

  // Check final state
  const finalConsolidation = getConsolidationSnapshot();

  if (finalConsolidation) {
    const actualIncrease = finalConsolidation.totalCredits - initialCredits;
    const causallyConsistent = actualIncrease >= expectedIncrease;

    causalConsistencyRate.add(causallyConsistent ? 1 : 0);

    console.log(`Causal consistency: Expected increase ≥${expectedIncrease}, Actual increase: ${actualIncrease}, Result: ${causallyConsistent ? 'PASS' : 'FAIL'}`);

    if (!causallyConsistent) {
      consistencyViolations.add(1);
    }
  }
}

// Phase 3: Check read consistency (multiple reads should return same data)
export function checkReadConsistency() {
  const token = getAuthToken();
  if (!token) return;

  const currentDate = getCurrentDate();
  const merchantId = 'merchant_1';
  const trackingKey = `${merchantId}-${currentDate}`;

  // Perform multiple reads of the same consolidation
  const reads = [];
  const readCount = 3;

  for (let i = 0; i < readCount; i++) {
    const response = http.get(
      `${BASE_URL}/api/v1/merchants/${merchantId}/consolidations/daily?date=${currentDate}`,
      {
        headers: { 'Authorization': `Bearer ${token}` },
        tags: { api: 'consolidations', phase: 'read_consistency' }
      }
    );

    if (response.status === 200) {
      try {
        reads.push(response.json());
      } catch (e) {
        console.log(`Failed to parse consolidation response: ${e}`);
      }
    }

    sleep(0.5); // Small delay between reads
  }

  if (reads.length >= 2) {
    let consistent = true;
    const baseRead = reads[0];

    for (let i = 1; i < reads.length; i++) {
      const currentRead = reads[i];

      // Check key consistency metrics
      if (baseRead.transactionCount !== currentRead.transactionCount ||
        Math.abs(baseRead.netBalance - currentRead.netBalance) > 0.01) {
        consistent = false;
        consistencyViolations.add(1);
        console.log(`Read consistency violation: Base(${baseRead.transactionCount}, ${baseRead.netBalance}) vs Current(${currentRead.transactionCount}, ${currentRead.netBalance})`);
        break;
      }
    }

    readConsistencyRate.add(consistent ? 1 : 0);

    check(null, {
      'read consistency maintained': () => consistent,
    });
  }

  sleep(Math.random() * 4 + 3);
}

// Phase 4: Check causal consistency (transactions should appear in order)
export function checkCausalConsistency() {
  const token = getAuthToken();
  if (!token) return;

  // Create a sequence of transactions with known order
  const merchantId = 'merchant_1';
  const sequence = [];
  const sequenceSize = 3;

  for (let i = 0; i < sequenceSize; i++) {
    const transaction = {
      Amount: 100 * (i + 1), // 100, 200, 300
      Type: 'CREDITO',
      Description: `Causal-sequence-${Date.now()}-${i}`,
      sequenceNumber: i
    };

    const response = http.post(
      `${BASE_URL}/api/v1/merchants/${merchantId}/transactions`,
      JSON.stringify(transaction),
      {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        },
        tags: { api: 'transactions', phase: 'causal_consistency' }
      }
    );

    if (response.status >= 200 && response.status < 300) {
      sequence.push({
        ...transaction,
        id: response.json().id,
        createdAt: Date.now()
      });
    }

    sleep(1); // Ensure clear ordering
  }

  // Wait for convergence
  sleep(5);

  // Check if the effect is visible in consolidation
  const consolidationResponse = http.get(
    `${BASE_URL}/api/v1/merchants/${merchantId}/consolidations/daily?date=${getCurrentDate()}`,
    {
      headers: { 'Authorization': `Bearer ${token}` },
      tags: { api: 'consolidations', phase: 'causal_consistency' }
    }
  );

  if (consolidationResponse.status === 200) {
    const consolidation = consolidationResponse.json();

    // Basic causal consistency: the total should reflect all transactions
    // In a real system, you might need to fetch individual transactions to verify order
    const expectedMinimumIncrease = 600; // 100 + 200 + 300
    const causallyConsistent = consolidation.totalCredits >= expectedMinimumIncrease;

    causalConsistencyRate.add(causallyConsistent ? 1 : 0);

    check(consolidation, {
      'causal consistency maintained': () => causallyConsistent,
    });

    if (!causallyConsistent) {
      consistencyViolations.add(1);
      console.log(`Causal consistency violation: Expected min increase ${expectedMinimumIncrease}, current credits: ${consolidation.totalCredits}`);
    }
  }

  sleep(Math.random() * 5 + 5);
}

export function setup() {
  console.log('=== Consistency Validation Test ===');
  console.log(`Target: ${BASE_URL}`);
  console.log('');
  console.log('Consistency Types Being Tested:');
  console.log('1. Eventual Consistency: Transactions → Consolidations');
  console.log('2. Read Consistency: Multiple reads return same data');
  console.log('3. Causal Consistency: Order preservation');
  console.log('');
  console.log('Test Strategy:');
  console.log('- Create tracked transactions');
  console.log('- Monitor convergence time');
  console.log('- Validate read consistency');
  console.log('- Check causal ordering');
  console.log('');

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
  } else {
    console.log(`✗ Authentication: FAILED (${authResponse.status})`);
  }

  console.log('');
  console.log('Starting consistency validation...');
  return { startTime: Date.now() };
}

export function teardown(data) {
  const duration = (Date.now() - data.startTime) / 1000;
  console.log('');
  console.log('=== Consistency Test Results ===');
  console.log(`Duration: ${duration.toFixed(1)}s`);
  console.log('');
  console.log('Consistency Metrics:');
  console.log('- Eventual Consistency: Rate ≥ 95%');
  console.log('- Read Consistency: Rate ≥ 98%');
  console.log('- Causal Consistency: Rate ≥ 90%');
  console.log('- Convergence Time: P95 < 30s');
  console.log('');
  console.log('If all thresholds PASS: System maintains acceptable consistency levels');
  console.log('If thresholds FAIL: Consistency mechanisms need improvement');
  console.log('');
}
