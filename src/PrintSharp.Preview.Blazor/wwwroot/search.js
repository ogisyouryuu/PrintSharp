/** 描画データを読み直さず、論理文字列を検索してセル領域を重ねます。 */
export class PreviewSearch {
    options;
    input = document.createElement('input');
    status = document.createElement('span');
    previous = document.createElement('button');
    next = document.createElement('button');
    index;
    hits = [];
    abort;
    timer;
    generation = 0;
    disposed = false;
    value = { query: '', total: 0, current: 0, busy: false, truncated: false };
    constructor(options) {
        this.options = options;
        options.host.className = 'search-bar';
        options.host.hidden = true;
        this.input.type = 'search';
        this.input.maxLength = 256;
        this.input.placeholder = '输入关键词';
        this.input.setAttribute('aria-label', '搜索关键词');
        this.status.setAttribute('role', 'status');
        this.status.setAttribute('aria-live', 'polite');
        this.status.className = 'search-status';
        const button = (element, label, click) => {
            element.type = 'button';
            element.textContent = label;
            element.addEventListener('click', click);
            return element;
        };
        options.host.append(this.input, button(this.previous, '上一处', () => { void this.move(-1).catch(() => { }); }), button(this.next, '下一处', () => { void this.move(1).catch(() => { }); }), this.status, button(document.createElement('button'), '关闭搜索', () => this.close()));
        this.input.addEventListener('input', () => {
            const query = this.input.value;
            this.reset(query);
            this.timer = setTimeout(() => { void this.search(query).catch(() => { }); }, 250);
        });
        this.input.addEventListener('keydown', event => {
            if (event.key === 'Escape') {
                event.preventDefault();
                this.close();
            }
            if (event.key === 'Enter') {
                event.preventDefault();
                if (this.value.query === this.input.value && this.hits.length)
                    void this.move(event.shiftKey ? -1 : 1).catch(() => { });
                else
                    void this.search(this.input.value).catch(() => { });
            }
        });
        this.update();
    }
    get state() { return { ...this.value }; }
    open() { this.options.host.hidden = false; this.input.focus(); }
    toggle() { if (this.options.host.hidden)
        this.open();
    else
        this.close(); }
    close() { this.reset(''); this.input.value = ''; this.options.host.hidden = true; }
    reset(query) {
        clearTimeout(this.timer);
        this.abort?.abort();
        this.generation++;
        this.hits = [];
        this.value = { query, total: 0, current: 0, busy: false, truncated: false };
        this.refresh();
        this.update();
        return this.generation;
    }
    update() {
        this.previous.disabled = this.next.disabled = this.value.busy || !this.hits.length;
        this.status.textContent = this.value.error ?? (this.value.busy ? '正在搜索…' : this.value.query ? this.hits.length ? `${this.value.current} / ${this.value.total}${this.value.truncated ? '（已达上限）' : ''}` : '无匹配结果' : '');
        this.options.changed(this.state);
    }
    async search(query) {
        if (this.disposed)
            return;
        this.open();
        this.input.value = query;
        const generation = this.reset(query);
        if (!query)
            return;
        const abort = new AbortController();
        this.abort = abort;
        this.value.busy = true;
        this.update();
        try {
            if (query.length > 256)
                throw new Error('关键词不能超过 256 个字符');
            await this.options.ready();
            abort.signal.throwIfAborted();
            if (Date.parse(this.options.manifest().expiresAt) <= Date.now())
                throw new Error('预览已过期，请重新生成');
            if (!this.options.load)
                throw new Error('当前数据源不支持文字搜索');
            if (!this.index) {
                const index = await this.options.load(abort.signal);
                abort.signal.throwIfAborted();
                const manifest = this.options.manifest();
                if (index.schemaVersion !== 1 || index.documentId !== manifest.documentId || index.revision !== manifest.revision || index.pages.length !== manifest.pageCount)
                    throw new Error('文字索引不属于当前文档版本');
                for (let i = 0; i < index.pages.length; i++) {
                    const page = index.pages[i];
                    if (page.pageNumber !== i + 1 || !Array.isArray(page.regions))
                        throw new Error('文字索引页码无效');
                    for (const r of page.regions)
                        if (typeof r.text !== 'string' || ![r.x, r.y, r.width, r.height].every(Number.isFinite) || r.width <= 0 || r.height <= 0)
                            throw new Error('文字索引区域无效');
                }
                this.index = index;
            }
            const expression = new RegExp(query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'giu');
            let visited = 0;
            outer: for (const page of this.index.pages)
                for (const region of page.regions) {
                    if (++visited % 100 === 0) {
                        await new Promise(resolve => setTimeout(resolve, 0));
                        abort.signal.throwIfAborted();
                    }
                    for (const match of region.text.matchAll(expression)) {
                        if (this.hits.length === 1000) {
                            this.value.truncated = true;
                            break outer;
                        }
                        this.hits.push({ page: page.pageNumber, region, start: match.index });
                    }
                }
            abort.signal.throwIfAborted();
            this.value.total = this.hits.length;
            this.value.busy = false;
            if (this.hits.length)
                await this.move(1);
            if (generation === this.generation) {
                this.refresh();
                this.update();
            }
        }
        catch (error) {
            if (generation !== this.generation || this.disposed || abort.signal.aborted)
                return;
            this.hits = [];
            this.value = { ...this.value, total: 0, current: 0, busy: false, error: String(error) };
            this.refresh();
            this.update();
            throw error;
        }
    }
    async move(direction) {
        if (this.disposed || !this.hits.length || this.value.busy)
            return;
        const generation = this.generation;
        const index = (this.value.current - 1 + direction + this.hits.length) % this.hits.length;
        this.value.error = undefined;
        this.value.current = index + 1;
        this.refresh();
        this.update();
        try {
            await this.options.navigate(this.hits[index].page);
            if (generation !== this.generation || this.disposed || this.value.current !== index + 1)
                return;
            this.refresh();
            this.options.viewport.querySelector('.search-hit-current')?.scrollIntoView({ block: 'center', inline: 'nearest' });
        }
        catch (error) {
            if (generation === this.generation && !this.disposed) {
                this.value.error = String(error);
                this.update();
            }
            throw error;
        }
    }
    refresh() {
        this.options.viewport.querySelectorAll('.search-overlay').forEach(element => element.remove());
        if (!this.hits.length || this.disposed)
            return;
        const manifest = this.options.manifest();
        if (!manifest)
            return;
        for (const canvas of this.options.viewport.querySelectorAll('canvas[data-search-page]')) {
            if (canvas.hidden)
                continue;
            const page = Number(canvas.dataset.searchPage), size = manifest.pages[page - 1];
            const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.classList.add('search-overlay');
            svg.setAttribute('viewBox', `0 0 ${size.width} ${size.height}`);
            svg.setAttribute('preserveAspectRatio', 'none');
            svg.setAttribute('aria-hidden', 'true');
            const regions = new Map();
            this.hits.forEach((hit, index) => { if (hit.page === page)
                regions.set(hit.region, regions.get(hit.region) === true || index + 1 === this.value.current); });
            regions.forEach((current, region) => {
                const matches = this.hits.map((hit, index) => ({ hit, current: index + 1 === this.value.current })).filter(item => item.hit.page === page && item.hit.region === region);
                const boxes = region.boxes === undefined || region.boxes === null ? [{ ...region, current }] : region.boxes.filter(box => matches.some(({ hit }) => box.start < hit.start + this.value.query.length && box.start + box.length > hit.start)).map(box => ({ ...box, current: matches.some(({ hit, current }) => current && box.start < hit.start + this.value.query.length && box.start + box.length > hit.start) }));
                const rectangles = [];
                for (const box of boxes.sort((a, b) => a.y - b.y || a.x - b.x)) {
                    const x = Math.max(region.x, box.x), y = Math.max(region.y, box.y);
                    const width = Math.min(region.x + region.width, box.x + box.width) - x;
                    const height = Math.min(region.y + region.height, box.y + box.height) - y;
                    if (width <= 0 || height <= 0)
                        continue;
                    const previous = rectangles.at(-1);
                    if (previous && previous.current === box.current && Math.abs(previous.y - y) < .05 && Math.abs(previous.height - height) < .05 && x <= previous.x + previous.width + .05)
                        previous.width = Math.max(previous.width, x + width - previous.x);
                    else
                        rectangles.push({ x, y, width, height, current: box.current });
                }
                for (const { current, ...rectangle } of rectangles) {
                    const rect = document.createElementNS(svg.namespaceURI, 'rect');
                    for (const [key, value] of Object.entries(rectangle))
                        rect.setAttribute(key, String(value));
                    rect.setAttribute('class', current ? 'search-hit-current' : 'search-hit');
                    svg.append(rect);
                }
            });
            canvas.parentElement.append(svg);
        }
    }
    destroy() { this.disposed = true; clearTimeout(this.timer); this.abort?.abort(); this.generation++; this.hits = []; this.index = undefined; this.refresh(); this.options.host.remove(); }
}
