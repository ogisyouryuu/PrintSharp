import { defineConfig } from '@playwright/test';
const blazorPort = Number(process.env.PREVIEW_TEST_BLAZOR_PORT ?? 5200);
const reuseExistingServer = process.env.PREVIEW_TEST_REUSE_SERVERS === '1';
export default defineConfig({
  testDir: './demo-tests',
  timeout: 60_000,
  workers: 1,
  use: { browserName: 'chromium', headless: true, viewport: { width: 1280, height: 1100 }, deviceScaleFactor: 1 },
  webServer: [
    { command: 'dotnet run --no-launch-profile --project ../../src/PrintSharp.Api --urls http://127.0.0.1:5080', url: 'http://127.0.0.1:5080/api/v1/health', timeout: 60_000, reuseExistingServer },
    { command: 'npm --prefix ../../demos/PrintSharp.Preview.Vue run dev', url: 'http://127.0.0.1:5199', timeout: 60_000, reuseExistingServer },
    { command: `dotnet run --no-launch-profile --project ../../demos/PrintSharp.Preview.Blazor.Demo --urls http://127.0.0.1:${blazorPort}`, url: `http://127.0.0.1:${blazorPort}`, timeout: 60_000, reuseExistingServer }
  ]
});
