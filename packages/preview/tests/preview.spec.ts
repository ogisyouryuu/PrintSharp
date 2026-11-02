import { test, expect } from '@playwright/test';
import { readFileSync, writeFileSync } from 'node:fs';
import { PNG } from 'pngjs';

test('Canvas paths agree with the shared Skia page', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  const data = await page.evaluate(async () => {
    const { CanvasRenderer } = await import('/index.js');
    const response = await (await fetch('/page.json')).json();
    const canvas = document.createElement('canvas'); document.body.append(canvas);
    await new CanvasRenderer().render(canvas, response, .75, new AbortController().signal);
    return canvas.toDataURL();
  });
  const actual = PNG.sync.read(Buffer.from(data.split(',')[1], 'base64'));
  const expected = PNG.sync.read(readFileSync(new URL('../../../artifacts/preview-tests/reference.png', import.meta.url)));
  expect([actual.width, actual.height]).toEqual([expected.width, expected.height]);
  let changed = 0;
  for (let i = 0; i < actual.data.length; i += 4) {
    if (Math.max(...[0, 1, 2].map(c => Math.abs(actual.data[i + c] - expected.data[i + c]))) > 50) changed++;
  }
  expect(changed / (actual.width * actual.height)).toBeLessThan(.035);
  writeFileSync(new URL('../../../artifacts/preview-tests/canvas.png', import.meta.url), PNG.sync.write(actual));
  await test.info().attach('canvas.png', { body: PNG.sync.write(actual), contentType: 'image/png' });
});

test('navigation, zoom, racing loads, resize, errors, and disposal', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  await page.evaluate(async () => {
    const api = await import('/index.js');
    const fixture = await (await fetch('/page.json')).json();
    (window as any).renders = 0;
    const preview = api.createPrintPreview(document.querySelector('#preview'), {
      source: {
        getManifest: async () => ({ schemaVersion: 1, documentId: 'fixture', revision: '1', pageCount: 3, pages: [1, 2, 3].map(pageNumber => ({ pageNumber, width: 400, height: 300 })) }),
        getPage: async (number: number) => {
          await new Promise(resolve => setTimeout(resolve, number === 2 ? 150 : 10));
          return { ...fixture, page: { ...fixture.page, pageNumber: number } };
        }
      }, zoom: 1
    });
    preview.addEventListener('pagechange', () => (window as any).renders++);
    (window as any).preview = preview;
    await preview.ready;
  });
  await expect(page.getByRole('button', { name: '首页', exact: true })).toBeDisabled();
  await page.evaluate(async () => {
    const preview = (window as any).preview;
    await Promise.all([preview.goToPage(2), preview.goToPage(3)]);
  });
  await expect(page.getByRole('spinbutton', { name: '页码' })).toHaveValue('3');
  await expect(page.locator('canvas')).toHaveAttribute('aria-label', 'Page 3 / 3');
  await page.getByRole('button', { name: '放大', exact: true }).click();
  await expect.poll(() => page.evaluate(() => (window as any).preview.actualZoom)).toBe(1.25);
  await page.getByRole('button', { name: '适合宽度' }).click();
  await page.evaluate(() => { document.querySelector<HTMLElement>('#preview')!.style.width = '500px'; });
  await expect.poll(() => page.locator('canvas').evaluate(c => parseFloat((c as HTMLElement).style.width))).toBeLessThan(500);
  await page.waitForTimeout(300);
  const count = await page.evaluate(() => (window as any).renders);
  await page.waitForTimeout(300);
  expect(await page.evaluate(() => (window as any).renders)).toBe(count);
  await page.evaluate(async () => { try { await (window as any).preview.goToPage(99); } catch {} });
  await expect(page.getByRole('status')).toContainText('outside');
  await page.evaluate(() => (window as any).preview.destroy());
  await expect(page.locator('canvas')).toHaveCount(0);
});

test('Vue wrapper updates v-model and unmounts the shared control', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  await page.evaluate(async () => {
    const { createApp, h, ref } = await import('/vue-runtime.js');
    const { PrintSharpPreview } = await import('/vue.js');
    const fixture = await (await fetch('/page.json')).json();
    const source = {
      getManifest: async () => ({ schemaVersion: 1, documentId: 'fixture', revision: '1', pageCount: 2, pages: [1, 2].map(pageNumber => ({ pageNumber, width: 400, height: 300 })) }),
      getPage: async (pageNumber: number) => ({ ...fixture, page: { ...fixture.page, pageNumber } })
    };
    const app = createApp({ setup() {
      const number = ref(1);
      return () => h(PrintSharpPreview, { source, page: number.value, 'onUpdate:page': (value: number) => { number.value = value; (window as any).vuePage = value; } });
    } });
    app.mount('#preview'); (window as any).vueApp = app;
  });
  await expect(page.locator('canvas')).toBeVisible();
  await page.getByRole('button', { name: '下一页' }).click();
  await expect.poll(() => page.evaluate(() => (window as any).vuePage)).toBe(2);
  await page.evaluate(() => (window as any).vueApp.unmount());
  await expect(page.locator('canvas')).toHaveCount(0);
});

test('rejects unsupported protocol and mismatched revision', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  const errors = await page.evaluate(async () => {
    const { CanvasRenderer, createPrintPreview } = await import('/index.js');
    const fixture = await (await fetch('/page.json')).json();
    const errors: string[] = [];
    try { await new CanvasRenderer().render(document.createElement('canvas'), { ...fixture, page: { ...fixture.page, schemaVersion: 99 } }, 1, new AbortController().signal); }
    catch (e) { errors.push(String(e)); }
    const preview = createPrintPreview(document.querySelector('#preview'), { source: {
      getManifest: async () => ({ schemaVersion: 1, documentId: 'fixture', revision: 'OTHER', pageCount: 1, pages: [fixture.page] }),
      getPage: async () => fixture
    } });
    try { await preview.ready; } catch (e) { errors.push(String(e)); }
    preview.destroy(); return errors;
  });
  expect(errors[0]).toContain('Unsupported'); expect(errors[1]).toContain('revision');
});

test('slow consecutive zooms keep the completed canvas visible until replacement', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  const result = await page.evaluate(async () => {
    const { CanvasRenderer, createPrintPreview } = await import('/index.js');
    const render = CanvasRenderer.prototype.render;
    CanvasRenderer.prototype.render = async function (...args) {
      // 遅い描画とキャンセルが重なっても、表示中のページを消さないことを確認します。
      await new Promise(resolve => setTimeout(resolve, 100));
      return render.apply(this, args);
    };
    const fixture = await (await fetch('/page.json')).json();
    const preview = createPrintPreview(document.querySelector('#preview'), { zoom: 1, source: {
      getManifest: async () => ({ schemaVersion: 1, documentId: 'fixture', revision: '1', pageCount: 1, pages: [fixture.page] }),
      getPage: async () => fixture
    } });
    await preview.ready;
    const canvas = document.querySelector('#preview')!.firstElementChild!.shadowRoot!.querySelector('canvas')!;
    let blankFrames = 0, frames = 0, monitoring = true;
    function sample() {
      if (!monitoring) return;
      frames++;
      if (canvas.hidden || getComputedStyle(canvas).display === 'none' || canvas.getBoundingClientRect().width === 0 || canvas.getContext('2d')!.getImageData(0, 0, 1, 1).data[3] === 0) blankFrames++;
      requestAnimationFrame(sample);
    }
    requestAnimationFrame(sample);
    for (const zoom of [1.25, 1.5, .75, 1.1]) {
      preview.setZoom(zoom);
      await new Promise(resolve => setTimeout(resolve, 30));
    }
    await preview.goToPage(1);
    monitoring = false;
    const width = parseFloat(canvas.style.width);
    preview.destroy();
    return { blankFrames, frames, width };
  });
  expect(result.frames).toBeGreaterThan(5);
  expect(result.blankFrames).toBe(0);
  expect(result.width).toBeCloseTo(400 * 96 / 72 * 1.1);
});
