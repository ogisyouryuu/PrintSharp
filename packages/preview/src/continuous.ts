import type { Manifest, Zoom } from './index.js';

interface Slot { element: HTMLDivElement; canvas: HTMLCanvasElement; renderedScale: number; failed: boolean; error?: unknown }
interface Anchor { page: number; x: number; y: number }
interface ContinuousOptions {
  viewport: HTMLElement; manifest: Manifest; zoom: () => Zoom; maxPixels: number;
  render: (canvas: HTMLCanvasElement, page: number, scale: number, signal: AbortSignal, budget: number) => Promise<void>;
  changed: (page: number, scale: number) => void;
  loading: (busy: boolean) => void;
  error: (error: unknown) => void;
}

/** ページ寸法だけでスクロール領域を作り、可視範囲の DOM と画布のみ保持します。 */
export class ContinuousViewport {
  private readonly stage = document.createElement('div');
  private readonly slots = new Map<number, Slot>();
  private offsets: number[] = [];
  private widths: number[] = [];
  private heights: number[] = [];
  private stageWidth = 0;
  private measuredWidth = 0;
  private measuredHeight = 0;
  private scale = 1;
  private current = 1;
  private disposed = false;
  private active?: { page: number; abort: AbortController };
  private running?: Promise<void>;
  private frame = 0;
  private generation = 0;
  private readonly onScroll = () => {
    if (!this.frame) this.frame = requestAnimationFrame(() => {
      this.frame = 0; this.updateWindow(); void this.pump();
    });
  };

  constructor(private readonly options: ContinuousOptions) {
    this.stage.className = 'continuous-stage';
    this.stage.style.cssText = 'position:relative;margin:0 auto;overflow-anchor:none';
    options.viewport.append(this.stage);
    options.viewport.addEventListener('scroll', this.onScroll, { passive: true });
    this.measure();
  }

  private measure(): void {
    const { viewport, manifest } = this.options;
    this.measuredWidth = viewport.clientWidth; this.measuredHeight = viewport.clientHeight;
    const zoom = this.options.zoom();
    const maxWidth = manifest.pages.reduce((max, p) => Math.max(max, p.width), 0);
    const maxHeight = manifest.pages.reduce((max, p) => Math.max(max, p.height), 0);
    const availableWidth = Math.max(1, viewport.clientWidth - 32);
    const fitWidth = availableWidth / (maxWidth * 96 / 72);
    this.scale = typeof zoom === 'number' ? zoom : zoom === 'fit-width' ? fitWidth
      : Math.min(fitWidth, Math.max(1, viewport.clientHeight - 32) / (maxHeight * 96 / 72));
    this.widths = manifest.pages.map(p => p.width * 96 / 72 * this.scale);
    this.heights = manifest.pages.map(p => p.height * 96 / 72 * this.scale);
    this.stageWidth = this.widths.reduce((max, width) => Math.max(max, width), availableWidth);
    let top = 0;
    this.offsets = this.heights.map(height => { const offset = top; top += height + 24; return offset; });
    this.stage.style.width = `${this.stageWidth}px`;
    this.stage.style.height = `${Math.max(0, top - 24)}px`;
    for (const [page, slot] of this.slots) this.position(page, slot);
  }

  private indexAt(y: number): number {
    let low = 0, high = this.offsets.length - 1;
    while (low < high) { const mid = Math.ceil((low + high) / 2); if (this.offsets[mid] <= y) low = mid; else high = mid - 1; }
    return low;
  }
  private anchor(): Anchor {
    const view = this.options.viewport;
    const y = view.scrollTop + this.measuredHeight / 2 - 16;
    const index = this.indexAt(y);
    return { page: index + 1, y: (y - this.offsets[index]) / this.heights[index],
      x: (view.scrollLeft + this.measuredWidth / 2 - 16 - (this.stageWidth - this.widths[index]) / 2) / this.widths[index] };
  }
  private restore(anchor: Anchor): void {
    const view = this.options.viewport, i = anchor.page - 1;
    view.scrollTop = 16 + this.offsets[i] + anchor.y * this.heights[i] - view.clientHeight / 2;
    view.scrollLeft = 16 + (this.stageWidth - this.widths[i]) / 2 + anchor.x * this.widths[i] - view.clientWidth / 2;
  }
  async relayout(): Promise<void> {
    const anchor = this.anchor();
    this.generation++; this.active?.abort.abort();
    this.measure(); this.restore(anchor); this.updateWindow();
    await this.pump();
  }
  async goToPage(page: number): Promise<void> {
    const index = page - 1;
    this.options.viewport.scrollTop = this.offsets[index];
    this.updateWindow(); await this.pump();
    const slot = this.slots.get(page);
    if (slot?.failed) throw slot.error;
  }
  async retry(): Promise<void> {
    for (const slot of this.slots.values()) slot.failed = false;
    await this.pump();
    const slot = this.slots.get(this.current);
    if (slot?.failed) throw slot.error;
  }

  private position(page: number, slot: Slot): void {
    const i = page - 1;
    slot.element.style.cssText = `position:absolute;top:${this.offsets[i]}px;left:${(this.stageWidth - this.widths[i]) / 2}px;width:${this.widths[i]}px;height:${this.heights[i]}px;background:white;box-shadow:0 2px 10px #0002`;
    slot.canvas.style.width = `${this.widths[i]}px`; slot.canvas.style.height = `${this.heights[i]}px`;
  }
  private release(page: number): void {
    const slot = this.slots.get(page)!;
    slot.canvas.width = slot.canvas.height = 0; slot.element.remove(); this.slots.delete(page);
  }
  private updateWindow(): void {
    if (this.disposed) return;
    const { viewport } = this.options;
    const first = Math.max(0, this.indexAt(viewport.scrollTop - 16) - 1);
    const last = Math.min(this.offsets.length - 1, this.indexAt(viewport.scrollTop + viewport.clientHeight - 16) + 1);
    this.current = this.indexAt(viewport.scrollTop + viewport.clientHeight / 2 - 16) + 1;
    if (this.active && (this.active.page < first + 1 || this.active.page > last + 1)) this.active.abort.abort();
    for (const page of this.slots.keys()) if (page < first + 1 || page > last + 1) this.release(page);
    for (let i = first; i <= last; i++) if (!this.slots.has(i + 1)) {
      const element = document.createElement('div'), canvas = document.createElement('canvas');
      element.dataset.page = String(i + 1); element.setAttribute('aria-label', `Page ${i + 1} / ${this.offsets.length}`);
      canvas.className = 'page'; canvas.width = canvas.height = 0; canvas.hidden = true;
      canvas.setAttribute('aria-label', `Page ${i + 1} / ${this.offsets.length}`);
      element.append(canvas);
      const next = [...this.stage.children].find(child => Number((child as HTMLElement).dataset.page) > i + 1);
      this.stage.insertBefore(element, next ?? null);
      const slot = { element, canvas, renderedScale: 0, failed: false }; this.slots.set(i + 1, slot); this.position(i + 1, slot);
    }
    // 可視ページ数が増えた場合は既存画布も縮小し、全体のピクセル予算を維持します。
    const budget = this.budget();
    for (const slot of this.slots.values()) if (slot.canvas.width * slot.canvas.height > budget) {
      const ratio = Math.sqrt(budget / (slot.canvas.width * slot.canvas.height));
      const temporary = document.createElement('canvas');
      temporary.width = Math.max(1, Math.floor(slot.canvas.width * ratio));
      temporary.height = Math.max(1, Math.floor(slot.canvas.height * ratio));
      temporary.getContext('2d')!.drawImage(slot.canvas, 0, 0, temporary.width, temporary.height);
      slot.canvas.width = temporary.width; slot.canvas.height = temporary.height;
      slot.canvas.getContext('2d')!.drawImage(temporary, 0, 0); temporary.width = temporary.height = 0;
    }
    this.options.changed(this.current, this.scale);
  }
  private budget(): number { return Math.max(1, Math.floor(this.options.maxPixels / (this.slots.size + 2))); }
  private pump(): Promise<void> {
    if (this.running) return this.running;
    this.running = this.run().finally(() => { this.running = undefined; });
    return this.running;
  }
  private async run(): Promise<void> {
    this.options.loading(true);
    try {
      while (!this.disposed) {
        const next = [...this.slots].filter(([, s]) => !s.failed && s.renderedScale !== this.scale)
          .sort(([a], [b]) => Math.abs(a - this.current) - Math.abs(b - this.current))[0];
        if (!next) break;
        const [page, slot] = next, generation = this.generation, scale = this.scale;
        const abort = new AbortController(); this.active = { page, abort };
        try {
          await this.options.render(slot.canvas, page, scale, abort.signal, this.budget());
          if (abort.signal.aborted || generation !== this.generation || this.disposed) continue;
          slot.renderedScale = scale; slot.canvas.hidden = false; this.position(page, slot); this.updateWindow();
        } catch (error) {
          if (abort.signal.aborted || this.disposed) continue;
          slot.failed = true; slot.error = error; this.options.error(error);
        } finally { this.active = undefined; }
      }
    } finally { if (!this.disposed) this.options.loading(false); }
  }
  destroy(): void {
    this.disposed = true; this.active?.abort.abort(); cancelAnimationFrame(this.frame);
    this.options.viewport.removeEventListener('scroll', this.onScroll);
    for (const page of this.slots.keys()) this.release(page);
    this.stage.remove();
  }
}
