import { defineConfig, devices } from '@playwright/test';

// The end-to-end tests run against the Docker Compose stack: docker compose up --build --wait first.
export default defineConfig({
  // The coverage check reads scenario ids from *.e2e.ts files; Playwright's default finds only *.spec and *.test.
  testMatch: '**/*.e2e.ts',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:8080',
    trace: 'retain-on-failure',
  },
  // Chromium only: Safari drops the Secure session cookie on http://localhost.
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
