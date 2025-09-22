import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

// Custom metrics
const transactionErrors = new Counter('transaction_errors');
const consolidationErrors = new Counter('consolidation_errors');
const authErrors = new Counter('auth_errors');
const transactionRate = new Rate('transaction_success_rate');
const consolidationRate = new Rate('consolidation_success_rate');
const authLatency = new Trend('auth_latency');

// Environment variables
const BASE_URL = __ENV.K6_BASE_URL || 'http://krakend:8080';
const AUTH_URL = __ENV.K6_AUTH_URL || `${BASE_URL}/api/v1/auth/token`;
const CLIENT_ID = __ENV.K6_CLIENT_ID || 'cash-flow-api';
const CLIENT_SECRET = __ENV.K6_CLIENT_SECRET || 'cash-flow-secret-2024';
const USERNAME = __ENV.K6_USERNAME || 'merchant1';
const PASSWORD = __ENV.K6_PASSWORD || 'merchant123';
const MERCHANTS = [
  __ENV.K6_MERCHANT_A || 'merchant-001',
  __ENV.K6_MERCHANT_B || 'merchant-002',
  __ENV.K6_MERCHANT_C || 'merchant-003',
  __ENV.K6_MERCHANT_D || 'merchant-004',
  __ENV.K6_MERCHANT_E || 'merchant-005'
];

export const options = {
  thresholds: {
    // Overall system health
    'http_req_failed': ['rate<0.05'], // 95% success rate minimum
    'http_req_duration': ['p(95)<500', 'p(99)<1000'], // Response time SLAs

    // Custom metrics thresholds
    'transaction_success_rate': ['rate>0.95'],
    'consolidation_success_rate': ['rate>0.95'],
    'auth_latency': ['p(95)<200'],

    // Resource utilization
    'http_req_duration{scenario:create_transactions}': ['p(95)<300', 'p(99)<600'],
    'http_req_duration{scenario:read_consolidations}': ['p(95)<200', 'p(99)<400'],
    'http_req_duration{scenario:mixed_operations}': ['p(95)<400', 'p(99)<800']
  },
  scenarios: {
    // Heavy transaction creation load
    create_transactions: {
      executor: 'ramping-vus',
      startVUs: 5,
      stages: [
        { target: 20, duration: '5m' },
        { target: 50, duration: '10m' },
        { target: 80, duration: '5m' },
        { target: 30, duration: '5m' },
        { target: 0, duration: '2m' }
      ],
      tags: { scenario: 'create_transactions' },
      exec: 'createTransactionScenario'
    },

    // Read-heavy consolidation queries
    read_consolidations: {
      executor: 'ramping-vus',
      startVUs: 3,
      stages: [
        { target: 15, duration: '5m' },
        { target: 30, duration: '10m' },
        { target: 40, duration: '5m' },
        { target: 20, duration: '5m' },
        { target: 0, duration: '2m' }
      ],
      tags: { scenario: 'read_consolidations' },
      exec: 'readConsolidationScenario'
    },

    // Mixed operations (realistic usage pattern)
    mixed_operations: {
      executor: 'ramping-vus',
      startVUs: 3,
      stages: [
        { target: 10, duration: '5m' },
        { target: 25, duration: '10m' },
        { target: 35, duration: '5m' },
        { target: 15, duration: '5m' },
        { target: 0, duration: '2m' }
      ],
      tags: { scenario: 'mixed_operations' },
      exec: 'mixedOperationsScenario'
    }
  }
};

export function setup() {
  console.log('🚀 Starting comprehensive stress test setup');
  const token = fetchToken();

  if (!token) {
    console.error('❌ Failed to obtain authentication token');
    throw new Error('Authentication failed during setup');
  }

  console.log('✅ Authentication successful');
  return { token, merchants: MERCHANTS };
}

function fetchToken() {
  const start = Date.now();

  const payload = `grant_type=password&client_id=${encodeURIComponent(CLIENT_ID)}&client_secret=${encodeURIComponent(CLIENT_SECRET)}&username=${encodeURIComponent(USERNAME)}&password=${encodeURIComponent(PASSWORD)}`;
  const params = {
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    timeout: '5s'
  };

  const res = http.post(AUTH_URL, payload, params);

  authLatency.add(Date.now() - start);

  const success = check(res, {
    'auth status 200': (r) => r.status === 200,
    'auth response time < 5s': (r) => r.timings.duration < 5000
  });

  if (!success) {
    authErrors.add(1);
    return null;
  }

  try {
    const tokenData = JSON.parse(res.body);
    return tokenData.access_token;
  } catch (e) {
    console.error('Failed to parse auth response:', e);
    authErrors.add(1);
    return null;
  }
}

function randomTransaction() {
  const types = ['DEBITO', 'CREDITO'];
  const type = types[Math.floor(Math.random() * types.length)];
  const amount = Math.round((Math.random() * 999 + 1) * 100) / 100;

  return {
    Type: type,
    Amount: amount,
    Description: `stress-test-${type}-${amount}-${Date.now()}`
  };
}

function getRandomMerchant(merchants) {
  return merchants[Math.floor(Math.random() * merchants.length)];
}

function getRandomDate() {
  const dates = ['2025-09-18', '2025-09-19', '2025-09-20'];
  return dates[Math.floor(Math.random() * dates.length)];
}

export function createTransactionScenario(data) {
  group('Transaction Creation Load', () => {
    const merchant = getRandomMerchant(data.merchants);
    const transaction = randomTransaction();

    const url = `${BASE_URL}/api/v1/merchants/${merchant}/transactions`;
    const payload = JSON.stringify(transaction);
    const params = {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${data.token}`
      },
      timeout: '10s'
    };

    const res = http.post(url, payload, params);

    const success = check(res, {
      'transaction created': (r) => r.status === 201,
      'response time < 1s': (r) => r.timings.duration < 1000,
      'response has id': (r) => {
        try {
          const body = JSON.parse(r.body);
          return body && body.Id;
        } catch (e) {
          return false;
        }
      }
    });

    transactionRate.add(success);
    if (!success) {
      transactionErrors.add(1);
    }

    // Realistic think time
    sleep(Math.random() * 0.5 + 0.1);
  });
}

export function readConsolidationScenario(data) {
  group('Consolidation Read Load', () => {
    const merchant = getRandomMerchant(data.merchants);
    const date = getRandomDate();

    const url = `${BASE_URL}/api/v1/merchants/${merchant}/consolidations/daily?date=${encodeURIComponent(date)}`;
    const params = {
      headers: {
        'Authorization': `Bearer ${data.token}`
      },
      timeout: '5s'
    };

    const res = http.get(url, params);

    const success = check(res, {
      'consolidation retrieved': (r) => r.status === 200,
      'response time < 500ms': (r) => r.timings.duration < 500,
      'response is json': (r) => {
        try {
          JSON.parse(r.body);
          return true;
        } catch (e) {
          return false;
        }
      }
    });

    consolidationRate.add(success);
    if (!success) {
      consolidationErrors.add(1);
    }

    // Shorter think time for read operations
    sleep(Math.random() * 0.3 + 0.05);
  });
}

export function mixedOperationsScenario(data) {
  group('Mixed Operations Pattern', () => {
    const operations = ['create', 'read', 'read', 'create', 'read']; // 40% create, 60% read
    const operation = operations[Math.floor(Math.random() * operations.length)];

    if (operation === 'create') {
      createTransactionScenario(data);
    } else {
      readConsolidationScenario(data);
    }
  });
}

export function teardown(data) {
  console.log('🏁 Stress test completed');
  console.log(`📊 Total merchants tested: ${data.merchants.length}`);
}