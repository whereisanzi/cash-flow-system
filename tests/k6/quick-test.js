import http from 'k6/http';
import { check, sleep } from 'k6';

// Test to validate transactions and consolidations
export const options = {
  vus: 1,
  duration: '30s',
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8000';

const AUTH_CONFIG = {
  client_id: 'cash-flow-api',
  client_secret: 'cash-flow-secret-2024',
  username: 'merchant1',
  password: 'merchant123'
};

let accessToken = null;

function getAuthToken() {
  if (accessToken) return accessToken;

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
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
    }
  );

  if (authResponse.status === 200) {
    const authData = authResponse.json();
    accessToken = authData.access_token;
    console.log('Authentication successful');
    return accessToken;
  } else {
    console.log(`Authentication failed: ${authResponse.status} - ${authResponse.body}`);
    return null;
  }
}

function getCurrentDate() {
  const now = new Date();
  return now.toISOString().split('T')[0]; // Format: YYYY-MM-DD
}

function createTransaction(token, merchantId, amount, type, description) {
  const transaction = {
    Amount: amount,
    Type: type,
    Description: description
  };

  console.log(`Creating transaction for ${merchantId}: ${JSON.stringify(transaction)}`);

  const response = http.post(
    `${BASE_URL}/api/v1/merchants/${merchantId}/transactions`,
    JSON.stringify(transaction),
    {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      }
    }
  );

  console.log(`Transaction response: ${response.status} - ${response.body}`);

  const success = check(response, {
    'transaction created successfully': (r) => r.status === 200 || r.status === 201,
    'no JSON parsing errors': (r) => !r.body.includes('JsonException'),
    'response has valid JSON': (r) => {
      try {
        JSON.parse(r.body);
        return true;
      } catch (e) {
        return false;
      }
    }
  });

  return { success, response };
}

function checkConsolidation(token, merchantId, date, previousBalance = null) {
  console.log(`Checking consolidation for ${merchantId} on date ${date}`);

  const response = http.get(
    `${BASE_URL}/api/v1/merchants/${merchantId}/consolidations/daily?date=${date}`,
    {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      }
    }
  );

  console.log(`Consolidation response: ${response.status} - ${response.body}`);

  const checks = {
    'consolidation retrieved successfully': (r) => r.status === 200,
    'consolidation has valid JSON': (r) => {
      try {
        JSON.parse(r.body);
        return true;
      } catch (e) {
        return false;
      }
    },
    'consolidation has required fields': (r) => {
      if (r.status !== 200) return false;
      try {
        const data = JSON.parse(r.body);
        return data.hasOwnProperty('netBalance') &&
          data.hasOwnProperty('totalCredits') &&
          data.hasOwnProperty('totalDebits') &&
          data.hasOwnProperty('transactionCount');
      } catch (e) {
        return false;
      }
    }
  };

  // Validate that balance changed correctly after transactions
  if (previousBalance !== null && response.status === 200) {
    try {
      checks['balance changed correctly after transactions'] = (r) => {
        const data = JSON.parse(r.body);
        const currentBalance = parseFloat(data.netBalance);
        const balanceChange = currentBalance - previousBalance;

        // Expected change: +1500 +500 -300 = +1700 per iteration
        const expectedChange = 1700;

        console.log(`Previous balance: ${previousBalance}, Current balance: ${currentBalance}`);
        console.log(`Balance change: ${balanceChange}, Expected change: ${expectedChange}`);

        // Allow for small floating point differences and validate the change is approximately correct
        return Math.abs(balanceChange - expectedChange) < 0.01;
      };
    } catch (e) {
      console.log(`Error comparing balances: ${e.message}`);
    }
  }

  const success = check(response, checks);

  return { success, response };
}

export default function () {
  const token = getAuthToken();
  if (!token) return;

  const merchantId = 'merchant_1';
  const currentDate = getCurrentDate();

  // Get current balance before creating transactions
  let initialConsolidation = checkConsolidation(token, merchantId, currentDate);
  let previousBalance = 0;

  if (initialConsolidation.success && initialConsolidation.response.status === 200) {
    try {
      const data = initialConsolidation.response.json();
      previousBalance = parseFloat(data.netBalance);
      console.log(`Initial balance: ${previousBalance}`);
    } catch (e) {
      console.log(`Error reading initial balance: ${e.message}`);
    }
  }

  // Create multiple transactions to test consolidation
  const transactions = [
    { amount: 1500, type: 'CREDITO', description: 'Test credit transaction 1' },
    { amount: 500, type: 'CREDITO', description: 'Test credit transaction 2' },
    { amount: 300, type: 'DEBITO', description: 'Test debit transaction 1' }
  ];

  let allTransactionsSuccessful = true;

  // Create transactions
  transactions.forEach((txn, index) => {
    const result = createTransaction(token, merchantId, txn.amount, txn.type, txn.description);

    if (!result.success) {
      allTransactionsSuccessful = false;
      console.log(`Transaction ${index + 1} failed`);
    }

    sleep(0.5); // Small delay between transactions
  });

  // Wait a bit for consolidation to be processed
  sleep(2);

  // Check consolidation after transactions
  if (allTransactionsSuccessful) {
    console.log('Checking consolidation after transactions...');

    const consolidationResult = checkConsolidation(token, merchantId, currentDate, previousBalance);

    if (!consolidationResult.success) {
      console.log('Consolidation check failed');
    } else {
      // Log the consolidation details for analysis
      try {
        const data = consolidationResult.response.json();
        console.log(`Consolidation summary for ${merchantId}:`);
        console.log(`  Total Credits: ${data.totalCredits}`);
        console.log(`  Total Debits: ${data.totalDebits}`);
        console.log(`  Net Balance: ${data.netBalance}`);
        console.log(`  Transaction Count: ${data.transactionCount}`);
        console.log(`  Last Updated: ${data.lastUpdated}`);
      } catch (e) {
        console.log(`Error logging consolidation details: ${e.message}`);
      }
    }
  } else {
    console.log('Skipping consolidation check due to failed transactions');
  }

  sleep(1);
}
