import { test, expect } from '@playwright/test';

test('thumbnail sidebar virtualizes 1000 pages, selects, highlights and releases on close', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  await page.evaluate(async () => {
    const { createPrintPreview } = await import('/index.js');
    const fixture = await (await fetch('/page.json')).json();
    const sizes = Array.from({ length: 1000 }, (_, i) => ({ pageNumber: i + 1, width: i % 2 ? 500 : 400, height: 700 }));
    (window as any).requests = [];
    const preview = createPrintPreview(document.querySelector('#preview'), { zoom: 1, cachePages: 2, source: {
      getManifest: async () => ({ schemaVersion: 1, documentId: 'fixture', revision: '1', pageCount: sizes.length, pages: sizes }),
      getPage: async (number: number) => { (window as any).requests.push(number); return { ...fixture, page: { ...fixture.page, ...sizes[number - 1] } }; }
    } });
    (window as any).preview = preview; await preview.ready;
  });
  expect(await page.evaluate(() => (window as any).requests)).toEqual([1]);
  await page.getByRole('button', { name: '缩略图', exact: true }).click();
  await expect(page.locator('[data-thumbnail-page="1"] canvas')).toBeVisible();
  await expect(page.locator('[data-thumbnail-page="1"]')).toHaveAttribute('aria-current', 'page');
  expect(await page.locator('[data-thumbnail-page]').count()).toBeLessThan(8);
  await page.locator('.thumbnail-sidebar').evaluate(element => { element.scrollTop = 499 * 172; });
  await expect(page.locator('[data-thumbnail-page="500"] canvas')).toBeVisible();
  const before = await page.evaluate(() => (window as any).preview.cacheUsage.pages);
  expect(before).toBe(1);
  await page.locator('[data-thumbnail-page="500"]').click();
  await expect(page.locator('.viewport canvas')).toHaveAttribute('aria-label', 'Page 500 / 1000');
  await expect(page.locator('[data-thumbnail-page="500"]')).toHaveAttribute('aria-current', 'page');
  await page.evaluate(() => (window as any).preview.goToPage(900));
  await expect(page.locator('[data-thumbnail-page="900"]')).toHaveAttribute('aria-current', 'page');
  await expect(page.locator('[data-thumbnail-page="900"] canvas')).toBeVisible();
  const pixels = await page.locator('.thumbnail-sidebar').evaluate(element => [...element.querySelectorAll('canvas')].reduce((sum, c) => sum + c.width * c.height, 0));
  expect(pixels).toBeLessThanOrEqual(7 * 60_000);
  await page.getByRole('button', { name: '缩略图', exact: true }).click();
  await expect(page.locator('[data-thumbnail-page]')).toHaveCount(0);
  const count = await page.evaluate(() => (window as any).requests.length);
  await page.waitForTimeout(250);
  expect(await page.evaluate(() => (window as any).requests.length)).toBe(count);
  await page.evaluate(() => (window as any).preview.destroy());
});

test('foreground navigation aborts a slow thumbnail and does not wait for it', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  await page.evaluate(async () => {
    const { createPrintPreview } = await import('/index.js');
    const fixture = await (await fetch('/page.json')).json();
    const sizes = Array.from({ length: 30 }, (_, i) => ({ ...fixture.page, pageNumber: i + 1 }));
    (window as any).events = [];
    const preview = createPrintPreview(document.querySelector('#preview'), { thumbnails: true, zoom: 1, source: {
      getManifest: async () => ({ schemaVersion: 1, documentId: 'fixture', revision: '1', pageCount: sizes.length, pages: sizes }),
      getPage: async (number: number, signal: AbortSignal) => {
        (window as any).events.push(`start:${number}`);
        if (number === 2) await new Promise((resolve, reject) => {
          const timer = setTimeout(resolve, 10_000);
          signal.addEventListener('abort', () => { clearTimeout(timer); (window as any).events.push('abort:2'); reject(signal.reason); }, { once: true });
        });
        return { ...fixture, page: sizes[number - 1] };
      }
    } });
    (window as any).preview = preview; await preview.ready;
  });
  await expect.poll(() => page.evaluate(() => (window as any).events.includes('start:2'))).toBe(true);
  await page.evaluate(() => (window as any).preview.goToPage(30));
  await expect(page.locator('.viewport canvas')).toHaveAttribute('aria-label', 'Page 30 / 30');
  const events = await page.evaluate(() => (window as any).events as string[]);
  expect(events[0]).toBe('start:1');
  expect(events.indexOf('abort:2')).toBeLessThan(events.indexOf('start:30'));
  await page.evaluate(() => (window as any).preview.destroy());
});

test('thumbnail failure stays local and retries without replacing the document', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  await page.evaluate(async () => {
    const { createPrintPreview } = await import('/index.js');
    const fixture = await (await fetch('/page.json')).json();
    (window as any).failThumbnail = true;
    const sizes = [fixture.page, { ...fixture.page, pageNumber: 2 }];
    const preview = createPrintPreview(document.querySelector('#preview'), { thumbnails: true, source: {
      getManifest: async () => ({ schemaVersion: 1, documentId: 'fixture', revision: '1', pageCount: 2, pages: sizes }),
      getPage: async (number: number) => {
        if (number === 2 && (window as any).failThumbnail) throw new Error('Thumbnail unavailable');
        return { ...fixture, page: sizes[number - 1] };
      }
    } });
    (window as any).preview = preview; await preview.ready;
  });
  await expect(page.locator('[data-thumbnail-page="2"]')).toHaveAttribute('data-state', 'error');
  await expect(page.locator('.viewport canvas')).toBeVisible();
  await expect(page.locator('.status')).toHaveText('');
  await page.evaluate(async () => { (window as any).failThumbnail = false; await (window as any).preview.retry(); });
  await expect(page.locator('[data-thumbnail-page="2"] canvas')).toBeVisible();
  await page.evaluate(() => (window as any).preview.destroy());
  await expect(page.locator('canvas')).toHaveCount(0);
});
