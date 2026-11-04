import type { Manifest } from './index.js';

interface ThumbnailSlot { button: HTMLButtonElement; canvas: HTMLCanvasElement; ready: boolean; failed: boolean }
interface ThumbnailOptions {
  host: HTMLElement; manifest: Manifest; page: number;
  select: (page: number) => void;
  render: (canvas: HTMLCanvasElement, page: number, scale: number, signal: AbortSignal) => Promise<void>;
}

/** 本文が待機中のときだけ小さな画布を作成する仮想サイドバーです。 */
export class ThumbnailSidebar {
  private readonly stage = document.createElement('div');
  private readonly slots = new Map<number, ThumbnailSlot>();
  private readonly observer: ResizeObserver;
  private active?: { page: number; abort: AbortController };
  private timer?: ReturnType<typeof setTimeout>;
  private running = false;
  private paused = true;
  private disposed = false;
  private current: number;
  private readonly rowHeight = 172;
  private readonly onScroll = () => { this.updateWindow(); this.schedule(); };

  constructor(private readonly options: ThumbnailOptions) {
    this.current = options.page;
    this.stage.style.cssText = `height:${options.manifest.pageCount * this.rowHeight}px;position:relative`;
    options.host.append(this.stage);
    options.host.addEventListener('scroll', this.onScroll, { passive: true });
    this.observer = new ResizeObserver(this.onScroll); this.observer.observe(options.host);
    this.setPage(options.page);
  }
  setPaused(paused: boolean): void {
    this.paused = paused;
    if (paused) { clearTimeout(this.timer); this.active?.abort.abort(); }
    else this.schedule();
  }
  setPage(page: number): void {
    const changed = page !== this.current;
    this.current = page;
    const top = (page - 1) * this.rowHeight;
    if (changed || this.slots.size === 0) {
      if (top < this.options.host.scrollTop || top + this.rowHeight > this.options.host.scrollTop + this.options.host.clientHeight)
        this.options.host.scrollTop = top;
    }
    this.updateWindow(); this.schedule();
  }
  retry(): void { for (const slot of this.slots.values()) slot.failed = false; this.schedule(); }
  private updateWindow(): void {
    if (this.disposed) return;
    const first = Math.max(1, Math.floor(this.options.host.scrollTop / this.rowHeight));
    const last = Math.min(this.options.manifest.pageCount, Math.ceil((this.options.host.scrollTop + this.options.host.clientHeight) / this.rowHeight) + 1);
    if (this.active && (this.active.page < first || this.active.page > last)) this.active.abort.abort();
    for (const page of this.slots.keys()) if (page < first || page > last) this.release(page);
    for (let page = first; page <= last; page++) {
      let slot = this.slots.get(page);
      if (!slot) {
        const button = document.createElement('button'); button.type = 'button'; button.className = 'thumbnail';
        button.dataset.thumbnailPage = String(page); button.setAttribute('aria-label', `第 ${page} 页缩略图`);
        button.style.cssText = `position:absolute;top:${(page - 1) * this.rowHeight + 6}px;left:8px;right:8px;height:${this.rowHeight - 12}px`;
        const canvas = document.createElement('canvas'); canvas.width = canvas.height = 0; canvas.hidden = true;
        canvas.setAttribute('aria-hidden', 'true');
        const placeholder = document.createElement('span'); placeholder.className = 'thumbnail-placeholder'; placeholder.textContent = String(page);
        const label = document.createElement('span'); label.className = 'thumbnail-number'; label.textContent = String(page);
        button.append(placeholder, canvas, label); button.addEventListener('click', () => this.options.select(page));
        button.addEventListener('keydown', event => {
          const next = event.key === 'ArrowDown' ? page + 1 : event.key === 'ArrowUp' ? page - 1 : 0;
          if (next >= 1 && next <= this.options.manifest.pageCount) {
            event.preventDefault(); this.setPage(next); this.slots.get(next)?.button.focus(); this.options.select(next);
          }
        });
        slot = { button, canvas, ready: false, failed: false }; this.slots.set(page, slot); this.stage.append(button);
      }
      slot.button.setAttribute('aria-current', page === this.current ? 'page' : 'false');
      slot.button.tabIndex = page === this.current ? 0 : -1;
    }
  }
  private schedule(): void {
    clearTimeout(this.timer);
    if (!this.paused && !this.disposed && !this.running)
      this.timer = setTimeout(() => { void this.run(); }, 120);
  }
  private async run(): Promise<void> {
    if (this.disposed || this.paused || this.running) return;
    this.running = true;
    try {
      while (!this.disposed && !this.paused) {
        const next = [...this.slots].filter(([, slot]) => !slot.ready && !slot.failed)
          .sort(([a], [b]) => Math.abs(a - this.current) - Math.abs(b - this.current))[0];
        if (!next) break;
        const [page, slot] = next;
        const abort = new AbortController(); this.active = { page, abort };
        const size = this.options.manifest.pages[page - 1];
        const scale = Math.min(120 / size.width, 124 / size.height) * 72 / 96;
        try {
          await this.options.render(slot.canvas, page, scale, abort.signal);
          abort.signal.throwIfAborted();
          if (this.disposed || this.slots.get(page) !== slot) continue;
          slot.ready = true; slot.canvas.hidden = false;
          slot.button.querySelector<HTMLElement>('.thumbnail-placeholder')!.hidden = true;
          slot.button.removeAttribute('title'); slot.button.dataset.state = 'ready';
        } catch (error) {
          if (!abort.signal.aborted && !this.disposed) {
            slot.failed = true; slot.button.dataset.state = 'error';
            slot.button.title = error instanceof Error ? error.message : String(error);
          }
        } finally { this.active = undefined; }
        // 縮略図の間にイベントループを空け、本文の操作を先に処理します。
        await new Promise(resolve => setTimeout(resolve, 0));
      }
    } finally { this.running = false; }
  }
  private release(page: number): void {
    const slot = this.slots.get(page)!;
    slot.canvas.width = slot.canvas.height = 0; slot.button.remove(); this.slots.delete(page);
  }
  destroy(): void {
    this.disposed = true; clearTimeout(this.timer); this.active?.abort.abort(); this.observer.disconnect();
    this.options.host.removeEventListener('scroll', this.onScroll);
    for (const page of this.slots.keys()) this.release(page);
    this.stage.remove();
  }
}
