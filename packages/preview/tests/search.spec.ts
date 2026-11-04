import { test, expect, type Page } from '@playwright/test';

async function setup(page: Page, mode = 'single-page') {
  await page.goto('http://127.0.0.1:5197');
  await page.evaluate(async mode => {
    const { createPrintPreview } = await import('/index.js');
    const fixture = await (await fetch('/page.json')).json();
    const pages = Array.from({ length: 20 }, (_, i) => ({ pageNumber: i + 1, width: 400, height: 700 }));
    const region = (text: string, y = 500) => ({ text, x: 20, y, width: 200, height: 40 });
    (window as any).index = { schemaVersion: 1, documentId: 'fixture', revision: '1', pages: pages.map(p => ({ pageNumber: p.pageNumber, regions: [region(p.pageNumber === 20 ? '中文 العربية Alpha A+B <script> 😀' : 'Alpha', 500)] })) };
    (window as any).loads = 0; (window as any).aborted = 0;
    const preview = createPrintPreview(document.querySelector('#preview'), { viewMode: mode, zoom: 1, source: {
      getManifest: async () => ((window as any).manifest = { schemaVersion: 1, documentId: 'fixture', revision: '1', pageCount: pages.length, pages }),
      getPage: async (number: number) => ({ ...fixture, page: { ...fixture.page, ...pages[number - 1] } }),
      getTextIndex: async (signal: AbortSignal) => {
        (window as any).loads++;
        if ((window as any).slow) await new Promise((resolve, reject) => {
          const timer = setTimeout(resolve, 300);
          signal.addEventListener('abort', () => { clearTimeout(timer); (window as any).aborted++; reject(signal.reason); }, { once: true });
        });
        return (window as any).index;
      }
    } });
    (window as any).preview = preview; await preview.ready;
  }, mode);
}

test('search locates multilingual literal text across pages, wraps and keeps highlights aligned', async ({ page }) => {
  await setup(page);
  expect(await page.evaluate(() => (window as any).loads)).toBe(0);
  await page.evaluate(() => (window as any).preview.search('alpha'));
  await expect(page.locator('.search-status')).toHaveText('1 / 20');
  await page.getByRole('button', { name: '上一处', exact: true }).click();
  await expect(page.locator('.viewport canvas')).toHaveAttribute('aria-label', 'Page 20 / 20');
  await expect(page.locator('.search-status')).toHaveText('20 / 20');
  await page.evaluate(() => (window as any).preview.search('A+B'));
  await expect(page.locator('.search-status')).toHaveText('1 / 1');
  await expect(page.locator('.search-hit-current')).toBeVisible();
  const before = await page.locator('.search-hit-current').boundingBox();
  await page.evaluate(() => (window as any).preview.setZoom(2));
  await expect.poll(async () => (await page.locator('.search-hit-current').boundingBox())?.width ?? 0).toBeCloseTo((before!.width - 1.5) * 2 + 1.5, 0);
  await page.evaluate(() => (window as any).preview.setViewMode('continuous'));
  await expect(page.locator('.search-hit-current')).toHaveCount(1);
  for (const query of ['中文', 'العربية', '😀', '<script>']) {
    await page.evaluate(query => (window as any).preview.search(query), query);
    await expect(page.locator('.search-status')).toHaveText('1 / 1');
    await expect(page.locator('.search-hit-current')).toBeVisible();
  }
  expect(await page.evaluate(() => (window as any).loads)).toBe(1);
  await page.evaluate(() => (window as any).preview.search('missing'));
  await expect(page.locator('.search-status')).toHaveText('无匹配结果');
  await expect(page.locator('.search-overlay')).toHaveCount(0);
  await page.getByRole('button', { name: '关闭搜索' }).click();
  await expect(page.locator('.search-bar')).toBeHidden();
});

test('search cancels stale index loads, validates revision, retries and cleans up', async ({ page }) => {
  await setup(page);
  await page.evaluate(() => { (window as any).slow = true; void (window as any).preview.search('Alpha'); });
  await expect.poll(() => page.evaluate(() => (window as any).loads)).toBe(1);
  await page.evaluate(() => (window as any).preview.search('中文'));
  expect(await page.evaluate(() => (window as any).aborted)).toBe(1);
  await expect(page.locator('.search-status')).toHaveText('1 / 1');
  await page.evaluate(() => (window as any).preview.destroy());
  await expect(page.locator('.search-bar')).toHaveCount(0);
  await setup(page);
  await page.evaluate(async () => {
    (window as any).index.revision = 'wrong';
    await (window as any).preview.search('Alpha').catch(() => {});
  });
  await expect(page.locator('.search-status')).toContainText('当前文档版本');
  await expect(page.locator('.viewport canvas')).toBeVisible();
  await page.evaluate(async () => { (window as any).index.revision = '1'; await (window as any).preview.search('Alpha'); });
  await expect(page.locator('.search-status')).toHaveText('1 / 20');
  await page.evaluate(async () => { (window as any).manifest.expiresAt = '2000-01-01T00:00:00Z'; await (window as any).preview.search('missing').catch(() => {}); });
  await expect(page.locator('.search-status')).toContainText('预览已过期');
});

test('large result sets are capped and close aborts outstanding search', async ({ page }) => {
  await setup(page);
  await page.evaluate(async () => {
    (window as any).index.pages[0].regions[0].text = 'a '.repeat(1500);
    await (window as any).preview.search('a');
  });
  await expect(page.locator('.search-status')).toHaveText('1 / 1000（已达上限）');
  await page.evaluate(() => (window as any).preview.destroy());
  await setup(page);
  await page.evaluate(() => { (window as any).slow = true; void (window as any).preview.search('Alpha'); });
  await expect.poll(() => page.evaluate(() => (window as any).loads)).toBe(1);
  await page.getByRole('button', { name: '关闭搜索' }).click();
  await page.waitForTimeout(350);
  expect(await page.evaluate(() => (window as any).preview.searchState.query)).toBe('');
  await expect(page.locator('.search-overlay')).toHaveCount(0);
});
