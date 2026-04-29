// @ts-check
const { defineConfig, devices } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './tests',
  timeout: 30000,
  fullyParallel: true,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:5500',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    command: 'node .vscode/serve.js',
    url: 'http://localhost:5500',
    reuseExistingServer: !process.env.CI,
  },
});
