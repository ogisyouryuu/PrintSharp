/** 可視ページだけに透明な選択文字を配置し、コピー時は元の論理順を使用します。 */
export class TextSelectionLayer {
    viewport;
    changed;
    copied;
    entries = new Map();
    measure = document.createElement('canvas').getContext('2d');
    lastText = '';
    enabled = true;
    drag;
    caret(element, x) {
        const rect = element.getBoundingClientRect();
        const after = (x >= rect.left + rect.width / 2) !== (element.dir === 'rtl');
        return { node: element.firstChild, offset: after ? element.textContent.length : 0 };
    }
    onPointerDown = (event) => {
        if (!this.enabled || event.pointerType !== 'mouse' || event.button !== 0)
            return;
        const element = this.nearest(event.clientX, event.clientY);
        if (!element)
            return;
        const bounds = element.getBoundingClientRect();
        if (event.clientX < bounds.left - 2 || event.clientX > bounds.right + 2 || event.clientY < bounds.top || event.clientY > bounds.bottom)
            return;
        // 変形された絶対配置 span のネイティブ hit-test を使用せず、表示矩形から境界を決めます。
        event.preventDefault();
        const caret = this.caret(element, event.clientX);
        this.drag = { pointer: event.pointerId, ...caret };
        this.viewport.focus({ preventScroll: true });
        this.viewport.setPointerCapture(event.pointerId);
        this.selection()?.setBaseAndExtent(caret.node, caret.offset, caret.node, caret.offset);
        this.onChange();
    };
    onPointerMove = (event) => {
        if (!this.drag || this.drag.pointer !== event.pointerId)
            return;
        if (!this.viewport.contains(this.drag.node)) {
            this.drag = undefined;
            return;
        }
        event.preventDefault();
        const nearest = this.nearest(event.clientX, event.clientY);
        if (!nearest)
            return;
        const caret = this.caret(nearest, event.clientX);
        this.selection()?.setBaseAndExtent(this.drag.node, this.drag.offset, caret.node, caret.offset);
        this.onChange();
    };
    nearest(x, y) {
        let nearest, distance = Infinity;
        for (const entry of this.entries.values())
            if (!entry.layer.hidden)
                for (const span of entry.spans) {
                    const rect = span.element.getBoundingClientRect(), region = span.element.parentElement.getBoundingClientRect();
                    const left = Math.max(rect.left, region.left), right = Math.min(rect.right, region.right);
                    const top = Math.max(rect.top, region.top), bottom = Math.min(rect.bottom, region.bottom);
                    if (left >= right || top >= bottom)
                        continue;
                    const dx = Math.max(left - x, 0, x - right);
                    const dy = Math.max(top - y, 0, y - bottom);
                    const score = dy * 10000 + dx;
                    if (score < distance) {
                        distance = score;
                        nearest = span.element;
                    }
                }
        return nearest;
    }
    onPointerUp = (event) => {
        if (this.drag?.pointer !== event.pointerId)
            return;
        this.drag = undefined;
        if (this.viewport.hasPointerCapture(event.pointerId))
            this.viewport.releasePointerCapture(event.pointerId);
    };
    onChange = () => { const text = this.selectedText; if (text !== this.lastText) {
        this.lastText = text;
        this.changed(text);
    } };
    onCopy = (event) => {
        const text = this.selectedText;
        if (text && event.clipboardData) {
            event.preventDefault();
            event.clipboardData.setData('text/plain', text);
            this.copied(text);
        }
    };
    constructor(viewport, changed, copied = () => { }) {
        this.viewport = viewport;
        this.changed = changed;
        this.copied = copied;
        document.addEventListener('selectionchange', this.onChange);
        document.addEventListener('copy', this.onCopy);
        viewport.addEventListener('pointerdown', this.onPointerDown);
        viewport.addEventListener('pointermove', this.onPointerMove);
        viewport.addEventListener('pointerup', this.onPointerUp);
        viewport.addEventListener('pointercancel', this.onPointerUp);
    }
    selection() {
        const root = this.viewport.getRootNode();
        return root.getSelection?.() ?? document.getSelection();
    }
    get selectedText() {
        const selection = this.selection();
        if (!this.enabled || !selection || selection.isCollapsed || !selection.rangeCount)
            return '';
        const range = selection.getRangeAt(0), parts = [];
        // 範囲の両端がこのプレビュー内にある場合だけコピーを扱います。
        if (!this.viewport.contains(range.startContainer) || !this.viewport.contains(range.endContainer))
            return '';
        for (const entry of this.entries.values())
            if (entry.layer.isConnected && !entry.canvas.hidden)
                for (const span of entry.spans) {
                    const node = span.element.firstChild;
                    if (!range.intersectsNode(node) || (range.startContainer === node && range.startOffset === node.textContent.length) || (range.endContainer === node && range.endOffset === 0))
                        continue;
                    parts.push({ page: entry.page.pageNumber, cell: span.cell, region: span.region, start: span.box.start, end: span.box.start + span.box.length });
                }
        parts.sort((a, b) => a.page - b.page || a.cell - b.cell || a.start - b.start);
        let text = '', previous;
        for (const part of parts) {
            if (previous && previous.page === part.page && previous.cell === part.cell) {
                if (part.end <= previous.end)
                    continue;
                const gap = part.region.text.slice(previous.end, part.start);
                text += /^\s*$/.test(gap) ? gap : '\n';
                text += part.region.text.slice(Math.max(previous.end, part.start), part.end);
            }
            else
                text += (previous ? '\n' : '') + part.region.text.slice(part.start, part.end);
            previous = part;
        }
        return text;
    }
    clear() {
        if (this.drag && this.viewport.hasPointerCapture(this.drag.pointer))
            this.viewport.releasePointerCapture(this.drag.pointer);
        this.drag = undefined;
        const selection = this.selection();
        if (selection?.anchorNode && this.viewport.contains(selection.anchorNode))
            selection.removeAllRanges();
        this.onChange();
    }
    async copy() {
        const text = this.selectedText;
        if (!text)
            return;
        if (!navigator.clipboard?.writeText)
            throw new Error('当前环境无法访问剪贴板，请使用 Ctrl+C / Command+C');
        await navigator.clipboard.writeText(text);
        this.copied(text);
    }
    setEnabled(enabled) {
        if (enabled === this.enabled)
            return;
        this.clear();
        this.enabled = enabled;
        for (const entry of this.entries.values())
            entry.layer.hidden = !enabled;
        this.onChange();
    }
    attach(canvas, page) {
        const existing = this.entries.get(canvas);
        if (existing?.page === page) {
            this.refresh();
            return;
        }
        if (existing) {
            this.clear();
            existing.layer.remove();
            this.entries.delete(canvas);
        }
        if (!page.textRegions?.some(region => region.boxes?.length))
            return;
        const layer = document.createElement('div');
        layer.className = 'text-layer';
        layer.hidden = !this.enabled;
        layer.setAttribute('aria-label', `第 ${page.pageNumber} 页文字`);
        const entry = { canvas, page, layer, spans: [] };
        page.textRegions.forEach((region, cell) => {
            if (!region.boxes?.length)
                return;
            if (!Number.isFinite(region.width) || !Number.isFinite(region.height) || region.width <= 0 || region.height <= 0)
                throw new Error('Invalid text region');
            const container = document.createElement('div');
            container.className = 'text-region';
            container.style.cssText = `left:${region.x / page.width * 100}%;top:${region.y / page.height * 100}%;width:${region.width / page.width * 100}%;height:${region.height / page.height * 100}%`;
            const seen = new Set();
            for (const box of [...region.boxes].sort((a, b) => a.start - b.start)) {
                if (![box.x, box.y, box.width, box.height].every(Number.isFinite) || box.width <= 0 || box.height <= 0 || !Number.isInteger(box.start) || !Number.isInteger(box.length) || box.start < 0 || box.length < 1 || box.start + box.length > region.text.length)
                    throw new Error('Invalid text selection box');
                if (seen.has(box.start))
                    continue;
                seen.add(box.start);
                const span = document.createElement('span');
                span.className = 'text-cluster';
                span.textContent = region.text.slice(box.start, box.start + box.length);
                span.dir = box.rtl ? 'rtl' : 'ltr';
                span.style.left = `${(box.x - region.x) / region.width * 100}%`;
                span.style.top = `${(box.y - region.y) / region.height * 100}%`;
                span.dataset.start = String(box.start);
                container.append(span);
                entry.spans.push({ element: span, box, region, cell });
            }
            layer.append(container);
        });
        this.entries.set(canvas, entry);
        canvas.parentElement.append(layer);
        this.refresh();
    }
    refresh() {
        for (const [canvas, entry] of this.entries) {
            if (!this.viewport.contains(canvas)) {
                entry.layer.remove();
                this.entries.delete(canvas);
                continue;
            }
            entry.layer.hidden = !this.enabled || canvas.hidden;
            const scale = parseFloat(canvas.style.width) / entry.page.width;
            if (entry.scale === scale)
                continue;
            entry.scale = scale;
            for (const { element, box } of entry.spans) {
                const size = box.height * scale;
                this.measure.font = `${size}px sans-serif`;
                const width = this.measure.measureText(element.textContent).width;
                element.style.fontSize = `${size}px`;
                element.style.lineHeight = '1';
                element.style.transform = `scaleX(${width > 0 ? box.width * scale / width : 1})`;
            }
        }
        this.onChange();
    }
    destroy() {
        this.clear();
        document.removeEventListener('selectionchange', this.onChange);
        document.removeEventListener('copy', this.onCopy);
        this.viewport.removeEventListener('pointerdown', this.onPointerDown);
        this.viewport.removeEventListener('pointermove', this.onPointerMove);
        this.viewport.removeEventListener('pointerup', this.onPointerUp);
        this.viewport.removeEventListener('pointercancel', this.onPointerUp);
        for (const entry of this.entries.values())
            entry.layer.remove();
        this.entries.clear();
    }
}
