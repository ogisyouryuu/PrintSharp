export type Zoom = number | 'fit-width' | 'fit-page';
export interface PageSize { pageNumber: number; width: number; height: number }
export interface Manifest {
  schemaVersion: 1; documentId: string; revision: string; expiresAt: string;
  pageCount: number; pages: PageSize[];
}
export interface Glyph { resourceId: string; x: number; y: number; size: number }
export interface Command {
  op: 'save' | 'restore' | 'clipRect' | 'fillRect' | 'line' | 'image' | 'glyphRun';
  values?: number[]; color?: string; strokeWidth?: number; dash?: number[];
  resourceId?: string; glyphs?: Glyph[];
}
export interface DisplayPage extends PageSize { schemaVersion: 1; unit: 'pt'; commands: Command[] }
export interface Resource { kind: 'path' | 'png'; data: string }
export interface PageResponse {
  documentId: string; revision: string; page: DisplayPage; resources: Record<string, Resource>;
}
export interface PreviewSource {
  getManifest(signal: AbortSignal): Promise<Manifest>;
  getPage(pageNumber: number, signal: AbortSignal): Promise<PageResponse>;
}
export class PreviewError extends Error {
  constructor(message: string, public readonly status?: number) { super(message); this.name = 'PreviewError'; }
}
export interface HttpSourceOptions {
  baseUrl: string; documentId: string;
  fetch?: typeof fetch;
  requestInit?: Omit<RequestInit, 'signal' | 'method' | 'body'>;
}
export function createHttpPreviewSource(options: HttpSourceOptions): PreviewSource {
  const url = `${options.baseUrl.replace(/\/$/, '')}/${encodeURIComponent(options.documentId)}`;
  const request = options.fetch ?? globalThis.fetch.bind(globalThis);
  async function get<T>(suffix: string, signal: AbortSignal): Promise<T> {
    const response = await request(url + suffix, { ...options.requestInit, method: 'GET', signal });
    if (!response.ok) {
      let message = `Preview request failed (${response.status})`;
      try { message = (await response.json()).detail ?? message; } catch { /* 本文が JSON でない場合は状態コードを使用します。 */ }
      throw new PreviewError(message, response.status);
    }
    return response.json() as Promise<T>;
  }
  return { getManifest: signal => get('', signal), getPage: (page, signal) => get(`/pages/${page}`, signal) };
}

function finite(value: number): number {
  if (!Number.isFinite(value)) throw new PreviewError('Display list contains a non-finite coordinate');
  return value;
}
function positive(value: number): number {
  if (finite(value) <= 0) throw new PreviewError('Display list contains an invalid dimension');
  return value;
}

/** 文字列を再組版せず、サーバーの字形輪郭を描画します。 */
export class CanvasRenderer {
  async render(canvas: HTMLCanvasElement, response: PageResponse, zoom: number, signal: AbortSignal, maxPixels = 16_000_000): Promise<void> {
    const { page, resources } = response;
    if (page.schemaVersion !== 1 || page.unit !== 'pt') throw new PreviewError('Unsupported display-list version or unit');
    const width = positive(page.width) * 96 / 72 * positive(zoom);
    const height = positive(page.height) * 96 / 72 * zoom;
    const ratio = Math.min(globalThis.devicePixelRatio || 1, Math.sqrt(positive(maxPixels) / (width * height)), 16384 / width, 16384 / height);
    const target = document.createElement('canvas');
    target.width = Math.max(1, Math.floor(width * ratio));
    target.height = Math.max(1, Math.floor(height * ratio));
    const context = target.getContext('2d');
    if (!context) throw new PreviewError('Canvas 2D is unavailable');
    const paths = new Map<string, Path2D>();
    const images = new Map<string, ImageBitmap>();
    try {
      for (const command of page.commands) {
        signal.throwIfAborted();
        if (command.op === 'image' && !images.has(command.resourceId!)) {
          const resource = resources[command.resourceId!];
          if (!resource || resource.kind !== 'png') throw new PreviewError('Missing image resource');
          const bytes = Uint8Array.from(atob(resource.data), c => c.charCodeAt(0));
          images.set(command.resourceId!, await createImageBitmap(new Blob([bytes], { type: 'image/png' })));
        }
      }
      signal.throwIfAborted();
      context.fillStyle = '#fff';
      context.fillRect(0, 0, target.width, target.height);
      context.scale(target.width / page.width, target.height / page.height);
      let depth = 0;
      for (let index = 0; index < page.commands.length; index++) {
        if (index % 500 === 0) {
          await new Promise<void>(resolve => setTimeout(resolve, 0));
          signal.throwIfAborted();
        }
        const command = page.commands[index];
        const values = command.values ?? [];
        values.forEach(finite);
        if (['clipRect', 'fillRect', 'line', 'image'].includes(command.op) && values.length !== 4)
          throw new PreviewError('Invalid display command coordinates');
        const [x, y, w, h] = values;
        if (command.color != null && !/^#[0-9a-f]{8}$/i.test(command.color)) throw new PreviewError('Invalid display color');
        context.fillStyle = context.strokeStyle = command.color ?? '#000000ff';
        context.lineWidth = positive(command.strokeWidth ?? 1);
        context.lineCap = 'butt';
        context.lineJoin = 'miter';
        const dash = command.dash ?? [];
        dash.forEach(positive);
        context.setLineDash(dash);
        context.lineDashOffset = 0;
        switch (command.op) {
          case 'save': context.save(); depth++; break;
          case 'restore':
            if (depth-- <= 0) throw new PreviewError('Unbalanced display state');
            context.restore(); break;
          case 'clipRect': context.beginPath(); context.rect(x, y, w, h); context.clip(); break;
          case 'fillRect': context.fillRect(x, y, w, h); break;
          case 'line': context.beginPath(); context.moveTo(x, y); context.lineTo(w, h); context.stroke(); break;
          case 'image': context.drawImage(images.get(command.resourceId!)!, x, y, w, h); break;
          case 'glyphRun':
            if (!command.glyphs) throw new PreviewError('Missing glyphs');
            for (const glyph of command.glyphs) {
              let path = paths.get(glyph.resourceId);
              if (!path) {
                const resource = resources[glyph.resourceId];
                if (!resource || resource.kind !== 'path') throw new PreviewError('Missing glyph resource');
                path = new Path2D(resource.data); paths.set(glyph.resourceId, path);
              }
              context.save();
              context.translate(finite(glyph.x), finite(glyph.y));
              context.scale(positive(glyph.size), glyph.size);
              context.fill(path, 'nonzero');
              context.restore();
            }
            break;
          default: throw new PreviewError(`Unknown display operation: ${command.op}`);
        }
      }
      if (depth !== 0) throw new PreviewError('Unbalanced display state');
      signal.throwIfAborted();
      canvas.width = target.width; canvas.height = target.height;
      canvas.style.width = `${width}px`; canvas.style.height = `${height}px`;
      canvas.getContext('2d')!.drawImage(target, 0, 0);
    } finally {
      for (const bitmap of images.values()) bitmap.close();
      target.width = target.height = 0;
    }
  }
}

export interface PreviewOptions {
  source: PreviewSource; initialPage?: number; zoom?: Zoom; toolbar?: boolean;
  cachePages?: number; maxCanvasPixels?: number;
  labels?: Partial<Record<'first' | 'previous' | 'next' | 'last' | 'zoomIn' | 'zoomOut' | 'fitWidth' | 'fitPage' | 'retry' | 'loading' | 'empty', string>>;
}
const styles = `
:host{display:block;height:100%;min-height:200px;color:var(--printsharp-color,#172033);font:14px system-ui,sans-serif}
*{box-sizing:border-box}[hidden]{display:none!important}.preview{position:relative;height:100%;display:flex;flex-direction:column;background:var(--printsharp-background,#e8ebf0);border:1px solid #cbd1db;border-radius:6px;overflow:hidden}
.toolbar{display:flex;gap:6px;align-items:center;flex-wrap:wrap;padding:8px;background:var(--printsharp-toolbar,#fff);border-bottom:1px solid #cbd1db}
button,input{font:inherit;color:inherit;border:1px solid #cbd1db;border-radius:4px;padding:5px 8px;background:#fff}button{cursor:pointer}button:disabled{opacity:.4;cursor:default}input{width:65px}
.viewport{flex:1;min-height:0;overflow:auto;padding:16px;text-align:center;position:relative}.page{display:block;margin:auto;box-shadow:0 2px 10px #0002;background:white}.status{position:absolute;bottom:8px;left:8px;right:8px;z-index:1;padding:8px;background:#fff;overflow-wrap:anywhere}.status:empty{display:none}button:focus-visible,input:focus-visible,.viewport:focus-visible{outline:2px solid #376ad6;outline-offset:2px}
`;

/** フレームワークに依存しない単一ページのプレビューコントロールです。 */
export class PrintPreview extends EventTarget {
  readonly ready: Promise<void>;
  private readonly host: HTMLDivElement;
  private readonly viewport: HTMLDivElement;
  private readonly canvas: HTMLCanvasElement;
  private readonly status: HTMLDivElement;
  private readonly pageInput: HTMLInputElement;
  private readonly total: HTMLSpanElement;
  private readonly zoomLabel: HTMLSpanElement;
  private readonly buttons = new Map<string, HTMLButtonElement>();
  private readonly observer: ResizeObserver;
  private readonly removalObserver: MutationObserver;
  private readonly renderer = new CanvasRenderer();
  private readonly cache = new Map<number, PageResponse>();
  private readonly manifestAbort = new AbortController();
  private active?: AbortController;
  private manifest?: Manifest;
  private current?: PageResponse;
  private destroyed = false;
  private sequence = 0;
  private pageNumber = 1;
  private zoomMode: Zoom;
  private scale = 1;
  private resizeTimer?: ReturnType<typeof setTimeout>;
  private readonly labels;

  constructor(container: HTMLElement, private readonly options: PreviewOptions) {
    super();
    if (options.cachePages !== undefined && (!Number.isInteger(options.cachePages) || options.cachePages < 1)) throw new PreviewError('cachePages must be a positive integer');
    if (options.maxCanvasPixels !== undefined) positive(options.maxCanvasPixels);
    this.zoomMode = options.zoom ?? 'fit-width';
    this.validateZoom(this.zoomMode);
    this.labels = { first: '首页', previous: '上一页', next: '下一页', last: '末页', zoomIn: '放大', zoomOut: '缩小', fitWidth: '适合宽度', fitPage: '整页', retry: '重试', loading: '正在加载…', empty: '没有可预览的页面', ...options.labels };
    this.host = document.createElement('div'); this.host.style.height = '100%';
    const root = this.host.attachShadow({ mode: 'open' });
    const style = document.createElement('style'); style.textContent = styles; root.append(style);
    const shell = document.createElement('section'); shell.className = 'preview'; shell.setAttribute('aria-label', 'PrintSharp Preview'); root.append(shell);
    const toolbar = document.createElement('div'); toolbar.className = 'toolbar'; toolbar.setAttribute('role', 'toolbar');
    toolbar.hidden = options.toolbar === false;
    if (options.toolbar !== false) shell.append(toolbar);
    const action = (name: keyof typeof this.labels, handler: () => void) => {
      const button = document.createElement('button'); button.type = 'button'; button.textContent = this.labels[name];
      button.setAttribute('aria-label', this.labels[name]); button.addEventListener('click', handler); toolbar.append(button); this.buttons.set(name, button);
    };
    const go = (page: number) => { void this.goToPage(page).catch(() => {}); };
    action('first', () => go(1)); action('previous', () => go(this.pageNumber - 1));
    this.pageInput = document.createElement('input'); this.pageInput.type = 'number'; this.pageInput.min = '1'; this.pageInput.setAttribute('aria-label', '页码'); toolbar.append(this.pageInput);
    this.pageInput.addEventListener('change', () => go(Number(this.pageInput.value)));
    this.total = document.createElement('span'); toolbar.append(this.total);
    action('next', () => go(this.pageNumber + 1)); action('last', () => go(this.pageCount));
    action('zoomOut', () => this.zoomOut()); this.zoomLabel = document.createElement('span'); toolbar.append(this.zoomLabel);
    action('zoomIn', () => this.zoomIn()); action('fitWidth', () => this.fitWidth()); action('fitPage', () => this.fitPage());
    action('retry', () => { void this.retry().catch(() => {}); });
    this.status = document.createElement('div'); this.status.className = 'status'; this.status.setAttribute('role', 'status'); this.status.setAttribute('aria-live', 'polite'); shell.append(this.status);
    this.viewport = document.createElement('div'); this.viewport.className = 'viewport'; this.viewport.tabIndex = 0; shell.append(this.viewport);
    this.canvas = document.createElement('canvas'); this.canvas.className = 'page'; this.canvas.hidden = true; this.viewport.append(this.canvas);
    this.viewport.addEventListener('keydown', event => {
      if (event.key === 'PageDown' || event.key === 'ArrowRight') { event.preventDefault(); if (this.pageNumber < this.pageCount) go(this.pageNumber + 1); }
      if (event.key === 'PageUp' || event.key === 'ArrowLeft') { event.preventDefault(); if (this.pageNumber > 1) go(this.pageNumber - 1); }
      if (event.key === '+' || event.key === '=') { event.preventDefault(); this.zoomIn(); }
      if (event.key === '-') { event.preventDefault(); this.zoomOut(); }
    });
    container.append(this.host);
    this.observer = new ResizeObserver(() => {
      clearTimeout(this.resizeTimer);
      this.resizeTimer = setTimeout(() => { if (this.current && !this.destroyed) void this.goToPage(this.pageNumber).catch(() => {}); }, 100);
    });
    this.observer.observe(this.viewport);
    this.removalObserver = new MutationObserver(() => { if (!container.contains(this.host) || !container.isConnected) this.destroy(); });
    this.removalObserver.observe(container.ownerDocument, { childList: true, subtree: true });
    this.updateControls();
    this.ready = this.initialize();
    void this.ready.catch(() => {});
  }
  get page(): number { return this.pageNumber; }
  get pageCount(): number { return this.manifest?.pageCount ?? 0; }
  get zoom(): Zoom { return this.zoomMode; }
  get actualZoom(): number { return this.scale; }
  private emit(name: string, detail: unknown): void { this.dispatchEvent(new CustomEvent(name, { detail })); }
  private assertAlive(): void { if (this.destroyed) throw new PreviewError('Preview has been destroyed'); }
  private async initialize(): Promise<void> {
    this.assertAlive(); this.status.textContent = this.labels.loading;
    try {
      const manifest = await this.options.source.getManifest(this.manifestAbort.signal);
      this.manifestAbort.signal.throwIfAborted();
      if (manifest.schemaVersion !== 1 || !Number.isInteger(manifest.pageCount) || manifest.pageCount < 0 || manifest.pages.length !== manifest.pageCount)
        throw new PreviewError('Invalid preview manifest');
      this.manifest = manifest;
      if (!manifest.pageCount) { this.status.textContent = this.labels.empty; this.updateControls(); return; }
      await this.goToPage(this.options.initialPage ?? 1);
    } catch (error) { if (!this.destroyed) this.showError(error); throw error; }
  }
  async retry(): Promise<void> { this.assertAlive(); if (!this.manifest) await this.initialize(); else await this.goToPage(this.pageNumber); }
  async goToPage(pageNumber: number): Promise<void> {
    this.assertAlive();
    if (!this.manifest) { await this.ready; this.assertAlive(); }
    if (!Number.isInteger(pageNumber) || pageNumber < 1 || pageNumber > this.pageCount) {
      const error = new PreviewError('Page number is outside the document'); this.showError(error); throw error;
    }
    if (Date.parse(this.manifest!.expiresAt) <= Date.now()) {
      const error = new PreviewError('Preview expired. Create it again.', 410); this.showError(error); throw error;
    }
    const sequence = ++this.sequence;
    this.active?.abort(); this.active = new AbortController();
    const signal = this.active.signal;
    const redraw = this.current?.page.pageNumber === pageNumber;
    this.pageNumber = pageNumber; this.updateControls();
    // 同じページの再描画では完成済みの画面を維持し、裏側の描画完了後に置き換えます。
    this.status.textContent = redraw ? '' : this.labels.loading;
    this.canvas.hidden = !redraw;
    this.emit('loadingchange', true);
    try {
      let response = this.cache.get(pageNumber);
      if (!response) response = await this.options.source.getPage(pageNumber, signal);
      signal.throwIfAborted();
      if (response.documentId !== this.manifest!.documentId || response.revision !== this.manifest!.revision || response.page.pageNumber !== pageNumber)
        throw new PreviewError('Page does not belong to the active document revision');
      this.status.textContent = '';
      const size = response.page;
      const fitWidth = Math.max(1, this.viewport.clientWidth - 32) / (positive(size.width) * 96 / 72);
      const fitHeight = Math.max(1, this.viewport.clientHeight - 32) / (positive(size.height) * 96 / 72);
      this.scale = this.zoomMode === 'fit-width' ? fitWidth : this.zoomMode === 'fit-page' ? Math.min(fitWidth, fitHeight) : this.zoomMode;
      await this.renderer.render(this.canvas, response, this.scale, signal, this.options.maxCanvasPixels);
      if (sequence !== this.sequence || this.destroyed) return;
      this.cache.delete(pageNumber); this.cache.set(pageNumber, response);
      while (this.cache.size > (this.options.cachePages ?? 3)) this.cache.delete(this.cache.keys().next().value!);
      this.current = response; this.canvas.hidden = false;
      this.canvas.setAttribute('aria-label', `Page ${pageNumber} / ${this.pageCount}`);
      this.updateControls(); this.emit('pagechange', pageNumber); this.emit('zoomchange', { zoom: this.zoomMode, scale: this.scale });
    } catch (error) {
      if (signal.aborted || this.destroyed) return;
      this.showError(error); throw error;
    } finally { if (sequence === this.sequence && !this.destroyed) this.emit('loadingchange', false); }
  }
  private showError(error: unknown): void { this.status.textContent = error instanceof Error ? error.message : String(error); this.emit('error', error); }
  private validateZoom(zoom: Zoom): void {
    if (zoom !== 'fit-width' && zoom !== 'fit-page' && (typeof zoom !== 'number' || !Number.isFinite(zoom) || zoom < .1 || zoom > 8))
      throw new PreviewError('Zoom must be fit-width, fit-page, or a number between 0.1 and 8');
  }
  setZoom(zoom: Zoom): void {
    this.assertAlive(); this.validateZoom(zoom); this.zoomMode = zoom;
    if (this.manifest?.pageCount) void this.goToPage(this.pageNumber).catch(() => {});
  }
  zoomIn(): void { this.setZoom(Math.min(8, Math.max(.1, this.scale * 1.25))); }
  zoomOut(): void { this.setZoom(Math.min(8, Math.max(.1, this.scale / 1.25))); }
  fitWidth(): void { this.setZoom('fit-width'); }
  fitPage(): void { this.setZoom('fit-page'); }
  private updateControls(): void {
    this.pageInput.value = String(this.pageNumber); this.pageInput.max = String(this.pageCount); this.pageInput.disabled = !this.pageCount;
    this.total.textContent = `/ ${this.pageCount}`; this.zoomLabel.textContent = `${Math.round(this.scale * 100)}%`;
    for (const [key, button] of this.buttons) button.disabled = key === 'retry' ? false : !this.pageCount || ((key === 'first' || key === 'previous') && this.pageNumber <= 1) || ((key === 'next' || key === 'last') && this.pageNumber >= this.pageCount);
  }
  destroy(): void {
    if (this.destroyed) return;
    this.destroyed = true; this.sequence++; this.manifestAbort.abort(); this.active?.abort();
    this.observer.disconnect(); this.removalObserver.disconnect(); clearTimeout(this.resizeTimer);
    this.cache.clear(); this.current = undefined; this.canvas.width = this.canvas.height = 0; this.host.remove();
  }
}
export function createPrintPreview(container: HTMLElement, options: PreviewOptions): PrintPreview { return new PrintPreview(container, options); }

/** Blazor には描画結果のイベントだけを返します。 */
export function mountBlazor(container: HTMLElement, baseUrl: string, documentId: string, initialPage: number, zoom: Zoom, callback: { invokeMethodAsync(method: string, value: unknown): Promise<unknown> }): PrintPreview {
  const preview = createPrintPreview(container, { source: createHttpPreviewSource({ baseUrl, documentId }), initialPage, zoom });
  preview.addEventListener('pagechange', event => { void callback.invokeMethodAsync('OnPageChanged', (event as CustomEvent).detail).catch(() => {}); });
  preview.addEventListener('error', event => { void callback.invokeMethodAsync('OnPreviewError', String((event as CustomEvent).detail)).catch(() => {}); });
  return preview;
}
