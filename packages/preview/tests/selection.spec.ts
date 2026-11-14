import { test, expect, type Page } from '@playwright/test';
async function setup(page: Page, mode = 'single-page') {
  await page.goto('http://127.0.0.1:5197');
  await page.evaluate(async mode => {
    const { createPrintPreview } = await import('/index.js');
    const fixture = await (await fetch('/page.json')).json();
    (window as any).fixture = fixture;
    const pages = Array.from({length: 100}, (_, i) => ({ ...fixture.page, pageNumber: i + 1 }));
    const preview = createPrintPreview(document.querySelector('#preview'), { zoom: 1, viewMode: mode, source: {
      getManifest: async () => ({schemaVersion:1,documentId:'fixture',revision:'1',pageCount:100,pages}),
      getPage: async (n: number) => ({ ...fixture, page: (window as any).legacy ? { ...pages[n-1], textRegions: undefined } : pages[n-1] }),
      getTextIndex: async () => ({schemaVersion:1,documentId:'fixture',revision:'1',pages:pages.map(p=>({pageNumber:p.pageNumber,regions:p.textRegions}))})
    }});
    (window as any).preview = preview; await preview.ready;
  }, mode);
}
async function selectRegion(page: Page, index = 0) {
  await page.locator('.text-region').nth(index).evaluate(element => {
    const spans = element.querySelectorAll('.text-cluster');
    const range = document.createRange(); range.setStart(spans[0].firstChild!, 0); range.setEnd(spans[spans.length-1].firstChild!, spans[spans.length-1].textContent!.length);
    const selection = (element.getRootNode() as any).getSelection(); selection.removeAllRanges(); selection.addRange(range);
  });
}
test('native drag selects text; copy preserves multilingual logical order and original whitespace', async ({page, context}) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await setup(page);
  const first = await page.locator('.text-cluster').nth(0).boundingBox();
  const second = await page.locator('.text-cluster').nth(1).boundingBox();
  await page.mouse.move(first!.x + 1, first!.y + first!.height/2); await page.mouse.down();
  await page.mouse.move(second!.x + second!.width - 1, second!.y + second!.height/2, {steps: 8}); await page.mouse.up();
  await expect.poll(() => page.evaluate(() => (window as any).preview.selectedText)).toBe('中文');
  const arabicStart = await page.evaluate(() => (window as any).fixture.page.textRegions[0].text.indexOf('العربية'));
  const arabic = page.locator('.text-region').first();
  const right = (await arabic.locator(`[data-start="${arabicStart}"]`).boundingBox())!;
  const left = (await arabic.locator(`[data-start="${arabicStart + 6}"]`).boundingBox())!;
  await page.mouse.move(right.x + right.width - 1, right.y + right.height / 2); await page.mouse.down();
  await page.mouse.move(left.x + 1, left.y + left.height / 2, { steps: 10 }); await page.mouse.up();
  await expect.poll(() => page.evaluate(() => (window as any).preview.selectedText)).toBe('العربية');
  await selectRegion(page);
  const expected = await page.evaluate(() => (window as any).fixture.page.textRegions[0].text);
  expect(await page.evaluate(() => (window as any).preview.selectedText)).toBe(expected);
  await page.keyboard.press('Control+c');
  await expect(page.locator('.copy-status')).toHaveText(`已复制：${expected}`);
  expect((await page.evaluate(() => navigator.clipboard.readText())).replace(/\r\n/g, '\n')).toBe(expected);
  await page.getByRole('button', {name:'复制',exact:true}).click();
  await expect(page.locator('.copy-status')).toHaveText(`已复制：${expected}`);
  expect((await page.evaluate(() => navigator.clipboard.readText())).replace(/\r\n/g, '\n')).toBe(expected);
  await selectRegion(page, 1);
  expect(await page.evaluate(() => (window as any).preview.selectedText)).toBe(await page.evaluate(() => (window as any).fixture.page.textRegions[1].text));
  await selectRegion(page, 2);
  expect(await page.evaluate(() => (window as any).preview.selectedText)).not.toContain('123456789');
  await page.getByRole('button', {name:'文字选择',exact:true}).click();
  await expect(page.locator('.text-layer')).toBeHidden();
  expect(await page.evaluate(() => (window as any).preview.selectedText)).toBe('');
});
test('copy feedback handles empty selection, expires, and never reports failed writes as successful', async ({page, context}) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await setup(page);
  await page.getByRole('button', {name:'复制',exact:true}).click();
  await expect(page.locator('.copy-status')).toHaveText('请先选择要复制的文字');
  await selectRegion(page);
  await page.getByRole('button', {name:'复制',exact:true}).click();
  await expect(page.locator('.copy-status')).toContainText('已复制：');
  await expect(page.locator('.copy-status')).toBeHidden({timeout: 7000});
  await selectRegion(page);
  await page.evaluate(() => {
    Object.defineProperty(navigator.clipboard, 'writeText', { configurable: true, value: async () => { throw new Error('Clipboard permission denied'); } });
  });
  await page.getByRole('button', {name:'复制',exact:true}).click();
  await expect(page.locator('.status:not(.copy-status)')).toHaveText('Clipboard permission denied');
  await expect(page.locator('.copy-status')).toBeHidden();
});

test('selection geometry scales, search uses boxes, virtualization releases text and old sources still render', async ({page}) => {
  await setup(page, 'continuous');
  await expect(page.locator('.text-layer').first()).toBeVisible();
  const before = await page.locator('.text-cluster').first().boundingBox();
  await page.evaluate(() => (window as any).preview.setZoom(2));
  await expect.poll(async () => (await page.locator('.text-cluster').first().boundingBox())!.width).toBeCloseTo(before!.width*2, 0);
  await page.evaluate(() => (window as any).preview.search('中文'));
  await expect.poll(async () => {
    const hit = await page.locator('[data-page="1"] .search-hit-current').first().boundingBox();
    const text = await page.locator('[data-page="1"] .text-cluster').first().boundingBox();
    return hit && text ? Math.max(Math.abs(hit.x-text.x), Math.abs(hit.width-text.width*2)) : Infinity;
  }).toBeLessThan(2);
  await page.evaluate(() => (window as any).preview.closeSearch());
  await page.evaluate(() => (window as any).preview.fitPage());
  await expect(page.locator('[data-page="2"] .text-layer')).toBeVisible();
  await page.locator('.continuous-stage').evaluate(stage => {
    const first = stage.querySelector('[data-page="1"] .text-cluster')!;
    const second = stage.querySelector('[data-page="2"] .text-region')!.querySelectorAll('.text-cluster');
    const range = document.createRange(); range.setStart(first.firstChild!, 0); range.setEnd(second[second.length-1].firstChild!, second[second.length-1].textContent!.length);
    const selection = (stage.getRootNode() as any).getSelection(); selection.removeAllRanges(); selection.addRange(range);
  });
  expect((await page.evaluate(() => (window as any).preview.selectedText) as string).match(/中文/g)).toHaveLength(2);
  await selectRegion(page);
  await page.evaluate(() => (window as any).preview.goToPage(99));
  await page.evaluate(() => (window as any).preview.goToPage(98));
  const order = await page.locator('[data-page]').evaluateAll(elements => elements.map(element => Number((element as HTMLElement).dataset.page)));
  expect(order).toEqual([...order].sort((a,b) => a-b));
  expect(await page.locator('.text-layer').count()).toBeLessThan(8);
  expect(await page.locator('.text-cluster').count()).toBeLessThan(1000);
  expect(await page.evaluate(() => (window as any).preview.selectedText)).toBe('');
  await page.evaluate(() => (window as any).preview.destroy());
  await expect(page.locator('.text-layer')).toHaveCount(0);
  await setup(page);
  await page.evaluate(async () => { (window as any).legacy = true; await (window as any).preview.goToPage(2); });
  await expect(page.locator('.viewport canvas')).toBeVisible();
  await expect(page.locator('.text-layer')).toHaveCount(0);
});
