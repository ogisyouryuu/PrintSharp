import { TextSelectionLayer } from './selection.js';
import { PreviewSearch } from './search.js';
import { ThumbnailSidebar } from './thumbnails.js';
import { ContinuousViewport } from './continuous.js';
export class PreviewError extends Error {
    status;
    constructor(message, status) {
        super(message);
        this.status = status;
        this.name = 'PreviewError';
    }
}
export function createHttpPreviewSource(options) {
    const url = `${options.baseUrl.replace(/\/$/, '')}/${encodeURIComponent(options.documentId)}`;
    const request = options.fetch ?? globalThis.fetch.bind(globalThis);
    async function get(suffix, signal) {
        const response = await request(url + suffix, { ...options.requestInit, method: 'GET', signal });
        if (!response.ok) {
            let message = `Preview request failed (${response.status})`;
            try {
                message = (await response.json()).detail ?? message;
            }
            catch { /* 本文が JSON でない場合は状態コードを使用します。 */ }
            throw new PreviewError(message, response.status);
        }
        return response.json();
    }
    return { getTextIndex: signal => get('/text', signal), getManifest: signal => get('', signal), getPage: (page, signal) => get(`/pages/${page}`, signal) };
}
function finite(value) {
    if (!Number.isFinite(value))
        throw new PreviewError('Display list contains a non-finite coordinate');
    return value;
}
function positive(value) {
    if (finite(value) <= 0)
        throw new PreviewError('Display list contains an invalid dimension');
    return value;
}
/** 文字列を再組版せず、サーバーの字形輪郭を描画します。 */
export class CanvasRenderer {
    async render(canvas, response, zoom, signal, maxPixels = 16_000_000) {
        const { page, resources } = response;
        if (page.schemaVersion !== 1 || page.unit !== 'pt')
            throw new PreviewError('Unsupported display-list version or unit');
        const width = positive(page.width) * 96 / 72 * positive(zoom);
        const height = positive(page.height) * 96 / 72 * zoom;
        const ratio = Math.min(globalThis.devicePixelRatio || 1, Math.sqrt(positive(maxPixels) / (width * height)), 16384 / width, 16384 / height);
        const target = document.createElement('canvas');
        target.width = Math.max(1, Math.floor(width * ratio));
        target.height = Math.max(1, Math.floor(height * ratio));
        const context = target.getContext('2d');
        if (!context)
            throw new PreviewError('Canvas 2D is unavailable');
        const paths = new Map();
        const images = new Map();
        try {
            for (const command of page.commands) {
                signal.throwIfAborted();
                if (command.op === 'image' && !images.has(command.resourceId)) {
                    const resource = resources[command.resourceId];
                    if (!resource || resource.kind !== 'png')
                        throw new PreviewError('Missing image resource');
                    const bytes = Uint8Array.from(atob(resource.data), c => c.charCodeAt(0));
                    images.set(command.resourceId, await createImageBitmap(new Blob([bytes], { type: 'image/png' })));
                }
            }
            signal.throwIfAborted();
            context.fillStyle = '#fff';
            context.fillRect(0, 0, target.width, target.height);
            context.scale(target.width / page.width, target.height / page.height);
            let depth = 0;
            for (let index = 0; index < page.commands.length; index++) {
                if (index % 500 === 0) {
                    await new Promise(resolve => setTimeout(resolve, 0));
                    signal.throwIfAborted();
                }
                const command = page.commands[index];
                const values = command.values ?? [];
                values.forEach(finite);
                if (['clipRect', 'fillRect', 'line', 'image'].includes(command.op) && values.length !== 4)
                    throw new PreviewError('Invalid display command coordinates');
                const [x, y, w, h] = values;
                if (command.color != null && !/^#[0-9a-f]{8}$/i.test(command.color))
                    throw new PreviewError('Invalid display color');
                context.fillStyle = context.strokeStyle = command.color ?? '#000000ff';
                context.lineWidth = positive(command.strokeWidth ?? 1);
                context.lineCap = 'butt';
                context.lineJoin = 'miter';
                const dash = command.dash ?? [];
                dash.forEach(positive);
                context.setLineDash(dash);
                context.lineDashOffset = 0;
                switch (command.op) {
                    case 'save':
                        context.save();
                        depth++;
                        break;
                    case 'restore':
                        if (depth-- <= 0)
                            throw new PreviewError('Unbalanced display state');
                        context.restore();
                        break;
                    case 'clipRect':
                        context.beginPath();
                        context.rect(x, y, w, h);
                        context.clip();
                        break;
                    case 'fillRect':
                        context.fillRect(x, y, w, h);
                        break;
                    case 'line':
                        context.beginPath();
                        context.moveTo(x, y);
                        context.lineTo(w, h);
                        context.stroke();
                        break;
                    case 'image':
                        context.drawImage(images.get(command.resourceId), x, y, w, h);
                        break;
                    case 'glyphRun':
                        if (!command.glyphs)
                            throw new PreviewError('Missing glyphs');
                        let glyphIndex = 0;
                        for (const glyph of command.glyphs) {
                            if (++glyphIndex % 256 === 0) {
                                await new Promise(resolve => setTimeout(resolve, 0));
                                signal.throwIfAborted();
                            }
                            let path = paths.get(glyph.resourceId);
                            if (!path) {
                                const resource = resources[glyph.resourceId];
                                if (!resource || resource.kind !== 'path')
                                    throw new PreviewError('Missing glyph resource');
                                path = new Path2D(resource.data);
                                paths.set(glyph.resourceId, path);
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
            if (depth !== 0)
                throw new PreviewError('Unbalanced display state');
            signal.throwIfAborted();
            canvas.width = target.width;
            canvas.height = target.height;
            canvas.style.width = `${width}px`;
            canvas.style.height = `${height}px`;
            canvas.getContext('2d').drawImage(target, 0, 0);
        }
        finally {
            for (const bitmap of images.values())
                bitmap.close();
            target.width = target.height = 0;
        }
    }
}
const styles = `
.text-layer{position:absolute;inset:0;overflow:hidden;pointer-events:none;text-align:left;user-select:text}.text-region{position:absolute;overflow:hidden;pointer-events:none}.text-cluster{position:absolute;color:transparent;white-space:pre;font-family:sans-serif;transform-origin:top left;cursor:text;pointer-events:auto;user-select:text;unicode-bidi:isolate}.text-cluster::selection{background:#3185ff66;color:transparent}.search-overlay{z-index:1}
.single-page{position:relative;width:fit-content;margin:0 auto}.search-bar{display:flex;align-items:center;gap:6px;padding:8px;background:#fff;flex-wrap:wrap;border-bottom:1px solid #cbd1db}.search-bar input{width:180px}.search-status{font-size:13px}.search-overlay{position:absolute;inset:0;width:100%;height:100%;pointer-events:none;overflow:hidden}.search-hit{fill:#ffd740;fill-opacity:.22}.search-hit-current{fill:#ff9800;fill-opacity:.35;stroke:#e67e00;stroke-width:1.5;vector-effect:non-scaling-stroke}

:host{display:block;height:100%;min-height:200px;color:var(--printsharp-color,#172033);font:14px system-ui,sans-serif}
*{box-sizing:border-box}[hidden]{display:none!important}.preview{position:relative;height:100%;display:flex;flex-direction:column;background:var(--printsharp-background,#e8ebf0);border:1px solid #cbd1db;border-radius:6px;overflow:hidden}
.toolbar{display:flex;gap:6px;align-items:center;flex-wrap:wrap;padding:8px;background:var(--printsharp-toolbar,#fff);border-bottom:1px solid #cbd1db}
button,input{font:inherit;color:inherit;border:1px solid #cbd1db;border-radius:4px;padding:5px 8px;background:#fff}button{cursor:pointer}button:disabled{opacity:.4;cursor:default}input{width:65px}
.preview-content{display:flex;flex:1;min-height:0;overflow:hidden}.thumbnail-sidebar{width:164px;max-width:40%;flex-shrink:0;overflow:auto;background:var(--printsharp-toolbar,#fff);border-right:1px solid #cbd1db;scrollbar-gutter:stable;overflow-anchor:none}.thumbnail{display:flex;align-items:center;justify-content:center;padding:4px 4px 24px;background:#f3f5f9}.thumbnail[aria-current="page"]{border:2px solid #376ad6;background:#eaf0ff}.thumbnail canvas{display:block;box-shadow:0 1px 4px #0002}.thumbnail-number{position:absolute;bottom:3px;left:0;right:0;text-align:center;font-size:12px}.thumbnail-placeholder{color:#758096}.thumbnail[data-state="error"]{border-style:dashed}.viewport{flex:1;min-width:0;min-height:0;overflow:auto;scrollbar-gutter:stable;overflow-anchor:none;padding:16px;text-align:center;position:relative}.page{display:block;margin:auto;box-shadow:0 2px 10px #0002;background:white}.status,.copy-status{position:absolute;bottom:8px;left:8px;right:8px;z-index:1;padding:8px;background:#fff;overflow-wrap:anywhere}.status:empty,.copy-status:empty{display:none}button:focus-visible,input:focus-visible,.viewport:focus-visible{outline:2px solid #376ad6;outline-offset:2px}
`;
/** フレームワークに依存しないページプレビューコントロールです。 */
export class PrintPreview extends EventTarget {
    options;
    ready;
    host;
    sidebarHost;
    sidebar;
    thumbnailsOpen;
    foregroundBusy = true;
    selectionLayer;
    selectionEnabled;
    searchPanel;
    singlePage = document.createElement('div');
    viewport;
    canvas;
    status;
    copyStatus;
    copyStatusTimer;
    pageInput;
    total;
    zoomLabel;
    buttons = new Map();
    observer;
    removalObserver;
    renderer = new CanvasRenderer();
    cache = new Map();
    cacheSizes = new Map();
    cacheBytes = 0;
    continuous;
    mode;
    manifestAbort = new AbortController();
    active;
    manifest;
    current;
    destroyed = false;
    sequence = 0;
    pageNumber = 1;
    zoomMode;
    scale = 1;
    resizeTimer;
    labels;
    constructor(container, options) {
        super();
        this.options = options;
        if (options.cachePages !== undefined && (!Number.isInteger(options.cachePages) || options.cachePages < 1))
            throw new PreviewError('cachePages must be a positive integer');
        if (options.maxCanvasPixels !== undefined)
            positive(options.maxCanvasPixels);
        positive(options.maxCacheBytes ?? 16 * 1024 * 1024);
        positive(options.maxTotalCanvasPixels ?? 24_000_000);
        this.selectionEnabled = options.textSelection ?? true;
        this.thumbnailsOpen = options.thumbnails ?? false;
        this.mode = options.viewMode ?? 'single-page';
        if (!['single-page', 'continuous'].includes(this.mode))
            throw new PreviewError('Invalid view mode');
        this.zoomMode = options.zoom ?? 'fit-width';
        this.validateZoom(this.zoomMode);
        this.labels = { first: '首页', previous: '上一页', next: '下一页', last: '末页', zoomIn: '放大', zoomOut: '缩小', fitWidth: '适合宽度', fitPage: '整页', retry: '重试', loading: '正在加载…', empty: '没有可预览的页面', singlePage: '单页', continuous: '连续滚动', thumbnails: '缩略图', search: '搜索', textSelection: '文字选择', copy: '复制', ...options.labels };
        this.host = document.createElement('div');
        this.host.style.height = '100%';
        const root = this.host.attachShadow({ mode: 'open' });
        const style = document.createElement('style');
        style.textContent = styles;
        root.append(style);
        const shell = document.createElement('section');
        shell.className = 'preview';
        shell.setAttribute('aria-label', 'PrintSharp Preview');
        root.append(shell);
        const toolbar = document.createElement('div');
        toolbar.className = 'toolbar';
        toolbar.setAttribute('role', 'toolbar');
        toolbar.hidden = options.toolbar === false;
        if (options.toolbar !== false)
            shell.append(toolbar);
        const action = (name, handler) => {
            const button = document.createElement('button');
            button.type = 'button';
            button.textContent = this.labels[name];
            button.setAttribute('aria-label', this.labels[name]);
            button.addEventListener('click', handler);
            toolbar.append(button);
            this.buttons.set(name, button);
        };
        const go = (page) => { void this.goToPage(page).catch(() => { }); };
        action('first', () => go(1));
        action('previous', () => go(this.pageNumber - 1));
        this.pageInput = document.createElement('input');
        this.pageInput.type = 'number';
        this.pageInput.min = '1';
        this.pageInput.setAttribute('aria-label', '页码');
        toolbar.append(this.pageInput);
        this.pageInput.addEventListener('change', () => go(Number(this.pageInput.value)));
        this.total = document.createElement('span');
        toolbar.append(this.total);
        action('next', () => go(this.pageNumber + 1));
        action('last', () => go(this.pageCount));
        action('zoomOut', () => this.zoomOut());
        this.zoomLabel = document.createElement('span');
        this.zoomLabel.style.minWidth = '4em';
        toolbar.append(this.zoomLabel);
        action('zoomIn', () => this.zoomIn());
        action('fitWidth', () => this.fitWidth());
        action('fitPage', () => this.fitPage());
        action('singlePage', () => this.setViewMode('single-page'));
        action('continuous', () => this.setViewMode('continuous'));
        action('thumbnails', () => this.setThumbnailsVisible(!this.thumbnailsOpen));
        action('textSelection', () => this.setTextSelection(!this.selectionEnabled));
        action('copy', () => { void this.copySelection().catch(error => this.showError(error)); });
        this.buttons.get('copy').addEventListener('mousedown', event => event.preventDefault());
        action('search', () => this.searchPanel.toggle());
        action('retry', () => { void this.retry().catch(() => { }); });
        this.status = document.createElement('div');
        this.status.className = 'status';
        this.status.setAttribute('role', 'status');
        this.status.setAttribute('aria-live', 'polite');
        shell.append(this.status);
        this.copyStatus = document.createElement('div');
        this.copyStatus.className = 'copy-status';
        this.copyStatus.setAttribute('role', 'status');
        this.copyStatus.setAttribute('aria-live', 'polite');
        this.copyStatus.setAttribute('aria-atomic', 'true');
        this.copyStatus.style.cssText = 'max-height:120px;overflow:auto;white-space:pre-wrap;user-select:text';
        shell.append(this.copyStatus);
        const content = document.createElement('div');
        content.className = 'preview-content';
        shell.append(content);
        this.sidebarHost = document.createElement('aside');
        this.sidebarHost.className = 'thumbnail-sidebar';
        this.sidebarHost.setAttribute('aria-label', '页面缩略图');
        this.sidebarHost.hidden = !this.thumbnailsOpen;
        content.append(this.sidebarHost);
        this.viewport = document.createElement('div');
        this.viewport.className = 'viewport';
        this.viewport.tabIndex = 0;
        content.append(this.viewport);
        this.canvas = document.createElement('canvas');
        this.canvas.className = 'page';
        this.canvas.hidden = true;
        this.singlePage.className = 'single-page';
        this.singlePage.append(this.canvas);
        this.viewport.append(this.singlePage);
        const searchHost = document.createElement('div');
        shell.insertBefore(searchHost, content);
        this.searchPanel = new PreviewSearch({ host: searchHost, viewport: this.viewport, manifest: () => this.manifest,
            ready: () => this.ready, load: options.source.getTextIndex?.bind(options.source), navigate: page => this.goToPage(page), changed: state => this.emit('searchchange', state) });
        this.selectionLayer = new TextSelectionLayer(this.viewport, text => { this.emit('selectionchange', text); }, text => this.showCopyStatus(`已复制：${text}`));
        this.selectionLayer.setEnabled(this.selectionEnabled);
        this.viewport.addEventListener('keydown', event => {
            if (event.ctrlKey || event.metaKey || event.shiftKey)
                return;
            if (this.continuous && (event.key === 'PageDown' || event.key === 'PageUp'))
                return;
            if (event.key === 'PageDown' || event.key === 'ArrowRight') {
                event.preventDefault();
                if (this.pageNumber < this.pageCount)
                    go(this.pageNumber + 1);
            }
            if (event.key === 'PageUp' || event.key === 'ArrowLeft') {
                event.preventDefault();
                if (this.pageNumber > 1)
                    go(this.pageNumber - 1);
            }
            if (event.key === '+' || event.key === '=') {
                event.preventDefault();
                this.zoomIn();
            }
            if (event.key === '-') {
                event.preventDefault();
                this.zoomOut();
            }
        });
        container.append(this.host);
        let viewportWidth = this.viewport.offsetWidth, viewportHeight = this.viewport.offsetHeight;
        this.observer = new ResizeObserver(() => {
            // スクロールバーだけの変化で進行中の描画をキャンセルしないようにします。
            if (viewportWidth === this.viewport.offsetWidth && viewportHeight === this.viewport.offsetHeight)
                return;
            viewportWidth = this.viewport.offsetWidth;
            viewportHeight = this.viewport.offsetHeight;
            clearTimeout(this.resizeTimer);
            this.resizeTimer = setTimeout(() => { if (this.destroyed)
                return; if (this.continuous)
                void this.continuous.relayout();
            else if (this.current && typeof this.zoomMode !== 'number')
                void this.goToPage(this.pageNumber).catch(() => { }); }, 100);
        });
        this.observer.observe(this.viewport);
        this.removalObserver = new MutationObserver(() => { if (!container.contains(this.host) || !container.isConnected)
            this.destroy(); });
        this.removalObserver.observe(container.ownerDocument, { childList: true, subtree: true });
        this.updateControls();
        this.ready = this.initialize();
        void this.ready.catch(() => { });
    }
    get textSelection() { return this.selectionEnabled; }
    get selectedText() { return this.selectionLayer.selectedText; }
    async copySelection() {
        this.assertAlive();
        this.copyStatus.textContent = '';
        clearTimeout(this.copyStatusTimer);
        if (!this.selectedText) {
            this.showCopyStatus('请先选择要复制的文字');
            return;
        }
        await this.selectionLayer.copy();
    }
    showCopyStatus(message) {
        if (this.destroyed)
            return;
        clearTimeout(this.copyStatusTimer);
        // コピー結果は描画状態と分離し、ページや選択範囲を移動させません。
        this.copyStatus.textContent = message;
        this.copyStatusTimer = setTimeout(() => { this.copyStatus.textContent = ''; }, 5000);
    }
    clearSelection() { this.assertAlive(); this.selectionLayer.clear(); }
    setTextSelection(enabled) { this.assertAlive(); this.selectionEnabled = enabled; this.selectionLayer.setEnabled(enabled); this.updateControls(); this.emit('textselectionchange', enabled); }
    get searchState() { return this.searchPanel.state; }
    search(query) { this.assertAlive(); return this.searchPanel.search(query); }
    nextSearchResult() { this.assertAlive(); return this.searchPanel.move(1); }
    previousSearchResult() { this.assertAlive(); return this.searchPanel.move(-1); }
    closeSearch() { this.assertAlive(); this.searchPanel.close(); }
    get thumbnailsVisible() { return this.thumbnailsOpen; }
    get viewMode() { return this.mode; }
    get cacheUsage() { return { pages: this.cache.size, bytes: this.cacheBytes }; }
    get page() { return this.pageNumber; }
    get pageCount() { return this.manifest?.pageCount ?? 0; }
    get zoom() { return this.zoomMode; }
    get actualZoom() { return this.scale; }
    emit(name, detail) {
        if (name === 'loadingchange') {
            this.foregroundBusy = Boolean(detail);
            this.sidebar?.setPaused(this.foregroundBusy);
        }
        this.dispatchEvent(new CustomEvent(name, { detail }));
    }
    assertAlive() { if (this.destroyed)
        throw new PreviewError('Preview has been destroyed'); }
    async initialize() {
        this.assertAlive();
        this.status.textContent = this.labels.loading;
        try {
            const manifest = await this.options.source.getManifest(this.manifestAbort.signal);
            this.manifestAbort.signal.throwIfAborted();
            if (manifest.schemaVersion !== 1 || !Number.isInteger(manifest.pageCount) || manifest.pageCount < 0 || manifest.pages.length !== manifest.pageCount)
                throw new PreviewError('Invalid preview manifest');
            for (let i = 0; i < manifest.pages.length; i++) {
                const page = manifest.pages[i];
                if (page.pageNumber !== i + 1)
                    throw new PreviewError('Invalid manifest page order');
                positive(page.width);
                positive(page.height);
            }
            this.manifest = manifest;
            if (!manifest.pageCount) {
                this.status.textContent = this.labels.empty;
                this.updateControls();
                return;
            }
            if (this.thumbnailsOpen)
                this.mountThumbnails();
            if (this.mode === 'continuous')
                this.mountContinuous();
            await this.goToPage(this.options.initialPage ?? 1);
        }
        catch (error) {
            if (!this.destroyed)
                this.showError(error);
            throw error;
        }
    }
    async retry() { this.assertAlive(); this.sidebar?.retry(); if (!this.manifest)
        await this.initialize();
    else if (this.continuous) {
        this.status.textContent = '';
        await this.continuous.retry();
    }
    else
        await this.goToPage(this.pageNumber); }
    async goToPage(pageNumber) {
        this.assertAlive();
        if (!this.manifest) {
            await this.ready;
            this.assertAlive();
        }
        if (!Number.isInteger(pageNumber) || pageNumber < 1 || pageNumber > this.pageCount) {
            const error = new PreviewError('Page number is outside the document');
            this.showError(error);
            throw error;
        }
        if (Date.parse(this.manifest.expiresAt) <= Date.now()) {
            const error = new PreviewError('Preview expired. Create it again.', 410);
            this.showError(error);
            throw error;
        }
        if (this.continuous) {
            await this.continuous.goToPage(pageNumber);
            if (!this.destroyed)
                this.emit('pagechange', this.pageNumber);
            return;
        }
        const sequence = ++this.sequence;
        this.active?.abort();
        this.active = new AbortController();
        const signal = this.active.signal;
        const redraw = this.current?.page.pageNumber === pageNumber;
        const anchor = redraw ? { x: (this.viewport.scrollLeft + this.viewport.clientWidth / 2) / Math.max(1, parseFloat(this.canvas.style.width)),
            y: (this.viewport.scrollTop + this.viewport.clientHeight / 2) / Math.max(1, parseFloat(this.canvas.style.height)) } : undefined;
        this.pageNumber = pageNumber;
        this.updateControls();
        // 同じページの再描画では完成済みの画面を維持し、裏側の描画完了後に置き換えます。
        this.status.textContent = redraw ? '' : this.labels.loading;
        this.canvas.hidden = !redraw;
        this.emit('loadingchange', true);
        try {
            const response = await this.loadPage(pageNumber, signal);
            this.status.textContent = '';
            const size = response.page;
            const fitWidth = Math.max(1, this.viewport.clientWidth - 32) / (positive(size.width) * 96 / 72);
            const fitHeight = Math.max(1, this.viewport.clientHeight - 32) / (positive(size.height) * 96 / 72);
            this.scale = this.zoomMode === 'fit-width' ? fitWidth : this.zoomMode === 'fit-page' ? Math.min(fitWidth, fitHeight) : this.zoomMode;
            await this.renderer.render(this.canvas, response, this.scale, signal, this.options.maxCanvasPixels);
            if (sequence !== this.sequence || this.destroyed)
                return;
            this.current = response;
            this.canvas.hidden = false;
            this.canvas.dataset.searchPage = String(pageNumber);
            this.selectionLayer.attach(this.canvas, response.page);
            if (anchor) {
                this.viewport.scrollLeft = anchor.x * parseFloat(this.canvas.style.width) - this.viewport.clientWidth / 2;
                this.viewport.scrollTop = anchor.y * parseFloat(this.canvas.style.height) - this.viewport.clientHeight / 2;
            }
            this.canvas.setAttribute('aria-label', `Page ${pageNumber} / ${this.pageCount}`);
            this.updateControls();
            this.emit('pagechange', pageNumber);
            this.emit('zoomchange', { zoom: this.zoomMode, scale: this.scale });
        }
        catch (error) {
            if (signal.aborted || this.destroyed)
                return;
            this.showError(error);
            throw error;
        }
        finally {
            if (sequence === this.sequence && !this.destroyed)
                this.emit('loadingchange', false);
        }
    }
    async loadPage(pageNumber, signal, cache = true) {
        if (Date.parse(this.manifest.expiresAt) <= Date.now())
            throw new PreviewError('Preview expired. Create it again.', 410);
        let response = this.cache.get(pageNumber);
        if (!response)
            response = await this.options.source.getPage(pageNumber, signal);
        signal.throwIfAborted();
        const size = this.manifest.pages[pageNumber - 1];
        if (response.documentId !== this.manifest.documentId || response.revision !== this.manifest.revision || response.page.pageNumber !== pageNumber || response.page.width !== size.width || response.page.height !== size.height)
            throw new PreviewError('Page does not belong to the active document revision or dimensions');
        if (!cache)
            return response;
        const bytes = this.cacheSizes.get(pageNumber) ?? new TextEncoder().encode(JSON.stringify(response)).byteLength;
        if (this.cache.delete(pageNumber))
            this.cacheBytes -= this.cacheSizes.get(pageNumber);
        this.cacheSizes.delete(pageNumber);
        if (bytes <= (this.options.maxCacheBytes ?? 16 * 1024 * 1024)) {
            this.cache.set(pageNumber, response);
            this.cacheSizes.set(pageNumber, bytes);
            this.cacheBytes += bytes;
        }
        while (this.cache.size > (this.options.cachePages ?? 3) || this.cacheBytes > (this.options.maxCacheBytes ?? 16 * 1024 * 1024)) {
            const oldest = this.cache.keys().next().value;
            this.cacheBytes -= this.cacheSizes.get(oldest);
            this.cache.delete(oldest);
            this.cacheSizes.delete(oldest);
        }
        return response;
    }
    mountThumbnails() {
        this.sidebar?.destroy();
        this.sidebar = new ThumbnailSidebar({ host: this.sidebarHost, manifest: this.manifest, page: this.pageNumber,
            select: page => { void this.goToPage(page).catch(() => { }); },
            render: async (canvas, page, scale, signal) => {
                // 縮略図は本文の LRU を追い出さず、表示中の小さな画布だけを保持します。
                const response = await this.loadPage(page, signal, false);
                await this.renderer.render(canvas, response, scale, signal, 60_000);
            } });
        this.sidebar.setPaused(this.foregroundBusy);
    }
    setThumbnailsVisible(visible) {
        this.assertAlive();
        if (visible === this.thumbnailsOpen)
            return;
        this.thumbnailsOpen = visible;
        this.sidebarHost.hidden = !visible;
        if (!visible) {
            this.sidebar?.destroy();
            this.sidebar = undefined;
        }
        else if (this.manifest?.pageCount)
            this.mountThumbnails();
        this.updateControls();
        this.emit('thumbnailschange', visible);
    }
    mountContinuous() {
        this.active?.abort();
        this.sequence++;
        this.singlePage.remove();
        this.canvas.width = this.canvas.height = 0;
        this.current = undefined;
        this.status.textContent = '';
        this.continuous = new ContinuousViewport({ viewport: this.viewport, manifest: this.manifest, zoom: () => this.zoomMode,
            maxPixels: this.options.maxTotalCanvasPixels ?? 24_000_000,
            render: async (canvas, page, scale, signal, budget) => {
                const response = await this.loadPage(page, signal);
                await this.renderer.render(canvas, response, scale, signal, Math.min(budget, this.options.maxCanvasPixels ?? 16_000_000));
                canvas.dataset.searchPage = String(page);
                this.selectionLayer.attach(canvas, response.page);
            },
            changed: (page, scale) => {
                const changed = this.pageNumber !== page;
                this.pageNumber = page;
                this.scale = scale;
                this.updateControls();
                if (changed)
                    this.emit('pagechange', page);
                this.emit('zoomchange', { zoom: this.zoomMode, scale });
            },
            loading: busy => { this.emit('loadingchange', busy); },
            error: error => this.showError(error) });
    }
    setViewMode(mode) {
        this.assertAlive();
        if (!['single-page', 'continuous'].includes(mode))
            throw new PreviewError('Invalid view mode');
        if (mode === this.mode)
            return;
        this.mode = mode;
        const page = this.pageNumber;
        if (this.manifest?.pageCount) {
            if (mode === 'continuous')
                this.mountContinuous();
            else {
                this.continuous?.destroy();
                this.continuous = undefined;
                this.viewport.append(this.singlePage);
                this.canvas.hidden = true;
                this.viewport.scrollTop = 0;
            }
            void this.goToPage(page).catch(() => { });
        }
        this.updateControls();
        this.emit('viewmodechange', mode);
    }
    showError(error) { this.status.textContent = error instanceof Error ? error.message : String(error); this.emit('error', error); }
    validateZoom(zoom) {
        if (zoom !== 'fit-width' && zoom !== 'fit-page' && (typeof zoom !== 'number' || !Number.isFinite(zoom) || zoom < .1 || zoom > 8))
            throw new PreviewError('Zoom must be fit-width, fit-page, or a number between 0.1 and 8');
    }
    setZoom(zoom) {
        this.assertAlive();
        this.validateZoom(zoom);
        this.zoomMode = zoom;
        if (this.continuous) {
            void this.continuous.relayout();
            return;
        }
        if (this.manifest?.pageCount)
            void this.goToPage(this.pageNumber).catch(() => { });
    }
    zoomIn() { this.setZoom(Math.min(8, Math.max(.1, this.scale * 1.25))); }
    zoomOut() { this.setZoom(Math.min(8, Math.max(.1, this.scale / 1.25))); }
    fitWidth() { this.setZoom('fit-width'); }
    fitPage() { this.setZoom('fit-page'); }
    updateControls() {
        this.searchPanel?.refresh();
        this.selectionLayer?.refresh();
        this.buttons.get('textSelection')?.setAttribute('aria-pressed', String(this.selectionEnabled));
        this.sidebar?.setPage(this.pageNumber);
        this.buttons.get('thumbnails')?.setAttribute('aria-pressed', String(this.thumbnailsOpen));
        this.buttons.get('thumbnails')?.setAttribute('aria-expanded', String(this.thumbnailsOpen));
        this.buttons.get('singlePage')?.setAttribute('aria-pressed', String(this.mode === 'single-page'));
        this.buttons.get('continuous')?.setAttribute('aria-pressed', String(this.mode === 'continuous'));
        this.pageInput.value = String(this.pageNumber);
        this.pageInput.max = String(this.pageCount);
        this.pageInput.disabled = !this.pageCount;
        this.total.textContent = `/ ${this.pageCount}`;
        this.zoomLabel.textContent = `${Math.round(this.scale * 100)}%`;
        for (const [key, button] of this.buttons)
            button.disabled = key === 'retry' ? false : !this.pageCount || ((key === 'first' || key === 'previous') && this.pageNumber <= 1) || ((key === 'next' || key === 'last') && this.pageNumber >= this.pageCount);
    }
    destroy() {
        if (this.destroyed)
            return;
        this.destroyed = true;
        clearTimeout(this.copyStatusTimer);
        this.searchPanel.destroy();
        this.selectionLayer.destroy();
        this.sequence++;
        this.manifestAbort.abort();
        this.active?.abort();
        this.sidebar?.destroy();
        this.sidebar = undefined;
        this.continuous?.destroy();
        this.continuous = undefined;
        this.observer.disconnect();
        this.removalObserver.disconnect();
        clearTimeout(this.resizeTimer);
        this.cache.clear();
        this.cacheSizes.clear();
        this.cacheBytes = 0;
        this.current = undefined;
        this.canvas.width = this.canvas.height = 0;
        this.host.remove();
    }
}
export function createPrintPreview(container, options) { return new PrintPreview(container, options); }
/** Blazor には描画結果のイベントだけを返します。 */
export function mountBlazor(container, baseUrl, documentId, initialPage, zoom, callback, viewMode = 'single-page', thumbnails = false, textSelection = true) {
    const preview = createPrintPreview(container, { source: createHttpPreviewSource({ baseUrl, documentId }), initialPage, zoom, viewMode, thumbnails, textSelection });
    preview.addEventListener('selectionchange', event => { void callback.invokeMethodAsync('OnSelectionChanged', event.detail).catch(() => { }); });
    preview.addEventListener('textselectionchange', event => { void callback.invokeMethodAsync('OnTextSelectionChanged', event.detail).catch(() => { }); });
    preview.addEventListener('searchchange', event => { void callback.invokeMethodAsync('OnSearchChanged', event.detail).catch(() => { }); });
    preview.addEventListener('pagechange', event => { void callback.invokeMethodAsync('OnPageChanged', event.detail).catch(() => { }); });
    preview.addEventListener('thumbnailschange', event => { void callback.invokeMethodAsync('OnThumbnailsChanged', event.detail).catch(() => { }); });
    preview.addEventListener('viewmodechange', event => { void callback.invokeMethodAsync('OnViewModeChanged', event.detail).catch(() => { }); });
    preview.addEventListener('error', event => { void callback.invokeMethodAsync('OnPreviewError', String(event.detail)).catch(() => { }); });
    return preview;
}
