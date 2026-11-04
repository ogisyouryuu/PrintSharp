import { test, expect } from '@playwright/test';

test('1000 mixed pages virtualize DOM, bound caches, follow scrolling and preserve the zoom anchor', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  await page.evaluate(async () => {
    const { createPrintPreview } = await import('/index.js');
    const fixture = await (await fetch('/page.json')).json();
    const sizes = Array.from({ length: 1000 }, (_, i) => ({ pageNumber: i + 1, width: i % 2 ? 500 : 400, height: i % 3 ? 700 : 600 }));
    const requests: number[] = [];
    const preview = createPrintPreview(document.querySelector('#preview'), {
      viewMode: 'continuous', zoom: 1, cachePages: 4, maxCacheBytes: 1_000_000, maxTotalCanvasPixels: 2_000_000,
      source: {
        getManifest: async () => ({ schemaVersion: 1, documentId: 'fixture', revision: '1', pageCount: sizes.length, pages: sizes }),
        getPage: async (number: number) => { requests.push(number); return { ...fixture, page: { ...fixture.page, ...sizes[number - 1] } }; }
      }
    });
    (window as any).preview = preview; (window as any).requests = requests;
    await preview.ready;
  });
  const view = page.locator('.viewport');
  const slots = page.locator('[data-page]');
  await expect.poll(() => slots.count()).toBeLessThan(8);
  expect(await page.evaluate(() => (window as any).requests.length)).toBeLessThan(8);
  for (const number of [100, 500, 997]) {
    await page.evaluate(number => (window as any).preview.goToPage(number), number);
    await expect(page.getByRole('spinbutton', { name: '页码' })).toHaveValue(String(number));
    await expect(page.locator(`[data-page="${number}"] canvas`)).toBeVisible();
    await expect.poll(() => slots.count()).toBeLessThan(8);
    const resources = await page.evaluate(() => {
      const root = document.querySelector('#preview')!.firstElementChild!.shadowRoot!;
      return { ...((window as any).preview.cacheUsage), pixels: [...root.querySelectorAll('canvas')].reduce((sum, c) => sum + c.width * c.height, 0) };
    });
    expect(resources.pages).toBeLessThanOrEqual(4);
    expect(resources.bytes).toBeLessThanOrEqual(1_000_000);
    expect(resources.pixels).toBeLessThanOrEqual(2_000_000);
  }
  await page.evaluate(() => (window as any).preview.goToPage(500));
  await view.evaluate(element => { element.scrollTop += 180; });
  await page.waitForTimeout(100);
  async function anchor() {
    return view.evaluate(element => {
      const viewport = element.getBoundingClientRect();
      const center = viewport.top + element.clientTop + element.clientHeight / 2;
      const slot = [...element.querySelectorAll<HTMLElement>('[data-page]')].find(s => { const r = s.getBoundingClientRect(); return r.top <= center && r.bottom >= center; })!;
      const r = slot.getBoundingClientRect();
      return { page: slot.dataset.page, relative: (center - r.top) / r.height };
    });
  }
  const before = await anchor();
  await page.evaluate(() => (window as any).preview.setZoom(1.6));
  await expect.poll(() => page.evaluate(() => (window as any).preview.actualZoom)).toBe(1.6);
  const after = await anchor();
  expect(after.page).toBe(before.page);
  expect(after.relative).toBeCloseTo(before.relative, 2);
  await page.evaluate(() => (window as any).preview.fitWidth());
  await page.waitForTimeout(100);
  const beforeResize = await anchor();
  await page.evaluate(() => { document.querySelector<HTMLElement>('#preview')!.style.width = '550px'; });
  await expect.poll(() => page.locator('[data-page="500"]').evaluate(element => element.getBoundingClientRect().width)).toBeLessThan(550);
  const afterResize = await anchor();
  expect(afterResize.page).toBe(beforeResize.page);
  expect(afterResize.relative).toBeCloseTo(beforeResize.relative, 2);
  await view.evaluate(element => { element.scrollTop += 1800; });
  await expect.poll(() => page.evaluate(() => (window as any).preview.page)).toBeGreaterThan(500);
  await page.evaluate(() => (window as any).preview.setViewMode('single-page'));
  await expect(page.locator('canvas')).toHaveCount(1);
  await expect(page.locator('canvas')).toBeVisible();
  await page.evaluate(() => (window as any).preview.destroy());
  await expect(page.locator('canvas')).toHaveCount(0);
});

test('continuous failures are retryable and oversized responses bypass the byte cache', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  const result = await page.evaluate(async () => {
    const { createPrintPreview } = await import('/index.js');
    const fixture = await (await fetch('/page.json')).json();
    let fails = true, rejected = false;
    const preview = createPrintPreview(document.querySelector('#preview'), { viewMode: 'continuous', maxCacheBytes: 1,
      source: {
        getManifest: async () => ({ schemaVersion: 1, documentId: 'fixture', revision: '1', pageCount: 1, pages: [fixture.page] }),
        getPage: async () => { if (fails) throw new Error('Temporary failure'); return fixture; }
      }
    });
    try { await preview.ready; } catch { rejected = true; }
    fails = false; await preview.retry();
    const canvas = document.querySelector('#preview')!.firstElementChild!.shadowRoot!.querySelector('canvas')!;
    const result = { rejected, visible: !canvas.hidden, ...preview.cacheUsage };
    preview.destroy(); return result;
  });
  expect(result).toEqual({ rejected: true, visible: true, pages: 0, bytes: 0 });
});

test('continuous view keeps completed pages during zoom and cancels obsolete loads', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  await page.evaluate(async () => {
    const { createPrintPreview } = await import('/index.js');
    const fixture = await (await fetch('/page.json')).json();
    const sizes = Array.from({ length: 50 }, (_, i) => ({ pageNumber: i + 1, width: 400, height: 800 }));
    (window as any).aborted = 0;
    const preview = createPrintPreview(document.querySelector('#preview'), { viewMode: 'continuous', zoom: 1, source: {
      getManifest: async () => ({ schemaVersion: 1, documentId: 'fixture', revision: '1', pageCount: 50, pages: sizes }),
      getPage: async (number: number, signal: AbortSignal) => {
        await new Promise((resolve, reject) => {
          const timer = setTimeout(resolve, 60);
          signal.addEventListener('abort', () => { clearTimeout(timer); (window as any).aborted++; reject(signal.reason); }, { once: true });
        });
        return { ...fixture, page: { ...fixture.page, ...sizes[number - 1] } };
      }
    } });
    (window as any).preview = preview; await preview.ready;
  });
  await page.evaluate(() => (window as any).preview.setZoom(1.2));
  await expect(page.locator('[data-page="1"] canvas')).toBeVisible();
  await page.evaluate(async () => { const p = (window as any).preview; await Promise.all([p.goToPage(20), p.goToPage(40)]); });
  await expect(page.locator('[data-page="40"] canvas')).toBeVisible();
  await expect(page.getByRole('spinbutton', { name: '页码' })).toHaveValue('40');
  expect(await page.evaluate(() => (window as any).aborted)).toBeGreaterThan(0);
  await page.evaluate(() => (window as any).preview.destroy());
});
