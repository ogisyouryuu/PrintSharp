import { test, expect } from '@playwright/test';

test('shared resources are cached across pages and DELETE uses authentication exactly once', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  const result = await page.evaluate(async () => {
    const { createHttpPreviewSource } = await import('/index.js');
    const calls: { url: string; method: string; headers: unknown; credentials: string }[] = [];
    const source = createHttpPreviewSource({ baseUrl: '/previews', documentId: 'demo', deleteOnDispose: true,
      requestInit: { headers: { Authorization: 'Bearer demo' }, credentials: 'include' },
      fetch: async (url, init) => {
        calls.push({ url: String(url), method: init.method, headers: init.headers, credentials: init.credentials });
        const path = new URL(String(url), location.href);
        if (init.method === 'DELETE') return new Response(null, { status: 204 });
        let data;
        if (path.pathname.endsWith('/resources')) data = { documentId: 'demo', revision: 'r1', resources: { shared: { kind: 'path', data: 'M0 0L1 1' } } };
        else if (path.pathname.includes('/pages/')) data = { documentId: 'demo', revision: 'r1', resourceIds: ['shared'], page: { schemaVersion: 2, pageNumber: Number(path.pathname.split('/').at(-1)), width: 200, height: 200, commands: [], unit: 'pt' } };
        else data = { schemaVersion: 2, documentId: 'demo', revision: 'r1', expiresAt: '2099-01-01', pageCount: 2, pages: [] };
        return new Response(JSON.stringify(data));
      } });
    const signal = new AbortController().signal;
    const manifest = await source.getManifest(signal);
    const first = await source.getPage(1, signal);
    const second = await source.getPage(2, signal);
    source.dispose(); source.dispose();
    return { calls, version: manifest.schemaVersion, sameResource: first.resources.shared === second.resources.shared };
  });
  expect(result.version).toBe(1);
  expect(result.sameResource).toBe(true);
  expect(result.calls.filter(c => c.url.includes('/resources'))).toHaveLength(1);
  expect(result.calls.filter(c => c.method === 'DELETE')).toHaveLength(1);
  for (const call of result.calls) { expect(call.headers).toEqual({ Authorization: 'Bearer demo' }); expect(call.credentials).toBe('include'); }
});

test('language packs cover all labels and unknown languages use English', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  const result = await page.evaluate(async () => {
    const { resolveLabels, en, ko, zhTW } = await import('/labels.js');
    const labels = [];
    for (const language of ['ko-KR', 'zh-Hant', 'zh-TW', 'fr-FR']) {
      document.documentElement.lang = language; labels.push(resolveLabels().loading);
    }
    return { labels, complete: [ko, zhTW].every(pack => Object.keys(en).every(key => !!pack[key])) };
  });
  expect(result.complete).toBe(true);
  expect(result.labels).toEqual(['불러오는 중…', '載入中…', '載入中…', 'Loading…']);
});

test('destroy(false) preserves the session when remounting the same document', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  const deleted = await page.evaluate(async () => {
    const { mountBlazor } = await import('/index.js');
    let deletes = 0;
    const original = globalThis.fetch;
    globalThis.fetch = async (_url, init) => {
      if (init.method === 'DELETE') { deletes++; return new Response(null, { status: 204 }); }
      return new Response(JSON.stringify({ schemaVersion: 1, documentId: 'demo', revision: 'r', expiresAt: '2099-01-01', pageCount: 0, pages: [] }));
    };
    try {
      const preview = mountBlazor(document.querySelector('#preview'), '/previews', 'demo', 1, 1,
        { invokeMethodAsync: async () => {} }, 'single-page', false, true, true, undefined, undefined, 'same-origin', true);
      await preview.ready; preview.destroy(false); preview.destroy();
      return deletes;
    } finally { globalThis.fetch = original; }
  });
  expect(deleted).toBe(0);
});

test('removing a mounted component deletes its session once with the original credentials', async ({ page }) => {
  await page.goto('http://127.0.0.1:5197');
  await page.evaluate(async () => {
    const { mountBlazor } = await import('/index.js');
    const calls: any[] = [];
    (window as any).deletionCalls = calls;
    globalThis.fetch = async (url, init) => {
      if (init.method === 'DELETE') { calls.push({ url, headers: init.headers, credentials: init.credentials }); return new Response(null, { status: 204 }); }
      return new Response(JSON.stringify({ schemaVersion: 1, documentId: 'old', revision: 'r', expiresAt: '2099-01-01', pageCount: 0, pages: [] }));
    };
    const preview = mountBlazor(document.querySelector('#preview'), '/previews', 'old', 1, 1,
      { invokeMethodAsync: async () => {} }, 'single-page', false, true, true, undefined,
      { Authorization: 'Bearer old-session' }, 'include', true);
    await preview.ready;
    document.querySelector('#preview').remove();
    (window as any).removedPreview = preview;
  });
  await expect.poll(() => page.evaluate(() => (window as any).deletionCalls.length)).toBe(1);
  await page.evaluate(() => (window as any).removedPreview.destroy());
  expect(await page.evaluate(() => (window as any).deletionCalls)).toEqual([
    { url: '/previews/old', headers: { Authorization: 'Bearer old-session' }, credentials: 'include' }
  ]);
});
