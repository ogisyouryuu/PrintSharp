import { test, expect } from '@playwright/test';

test('Blazor bridge exposes external controls, localized labels and authenticated fetch', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  await page.route('**/previews/demo**', async route => {
    expect(route.request().headers()['authorization']).toBe('Bearer demo');
    const pageNumber = Number(new URL(route.request().url()).pathname.split('/').at(-1));
    const body = pageNumber ? {
      documentId: 'demo', revision: '1', resources: {},
      page: { schemaVersion: 1, unit: 'pt', pageNumber, width: 400, height: 300, commands: [] }
    } : {
      schemaVersion: 1, documentId: 'demo', revision: '1', expiresAt: '2099-01-01T00:00:00Z', pageCount: 2,
      pages: [1, 2].map(pageNumber => ({ pageNumber, width: 400, height: 300 }))
    };
    await route.fulfill({ json: body });
  });
  await page.evaluate(async () => {
    document.documentElement.lang = 'ja';
    const { mountBlazor } = await import('/index.js');
    const events: [string, ...unknown[]][] = [];
    const preview = mountBlazor(document.querySelector('#preview'), '/previews', 'demo', 1, 1,
      { invokeMethodAsync: async (name, ...args) => { events.push([name, ...args]); } },
      'single-page', false, true, false, null, { Authorization: 'Bearer demo' }, 'include');
    (window as any).bridge = { preview, events };
    await preview.ready;
  });
  await expect.poll(() => page.evaluate(() => (window as any).bridge.events.some(e => e[0] === 'OnReady' && e.length === 1))).toBe(true);
  await expect(page.getByRole('button', { name: '拡大', exact: true })).toBeHidden();
  await page.evaluate(async () => { const p = (window as any).bridge.preview; await p.goToPage(2); p.zoomIn(); });
  await expect.poll(() => page.evaluate(() => (window as any).bridge.events.some(e => e[0] === 'OnPageChanged' && e[1] === 2))).toBe(true);
  await expect.poll(() => page.evaluate(() => (window as any).bridge.events.some(e => e[0] === 'OnZoomChanged' && e[1] === 1.25))).toBe(true);
  expect(await page.evaluate(() => (window as any).bridge.events.some(e => e[0] === 'OnPageCountChanged' && e[1] === 2))).toBe(true);
  await page.evaluate(() => (window as any).bridge.preview.destroy());
  await expect(page.locator('canvas')).toHaveCount(0);
});
