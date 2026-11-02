import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests',
  use: { browserName: 'chromium', headless: true, viewport: { width: 1000, height: 800 }, deviceScaleFactor: 1 },
  webServer: { command: 'node tests/server.mjs', url: 'http://127.0.0.1:5197', reuseExistingServer: false }
});
