import { defineComponent, h, onBeforeUnmount, onMounted, ref, watch, type PropType } from 'vue';
import { createPrintPreview, type PreviewSource, type PrintPreview, type Zoom, type ViewMode } from './index.js';

export const PrintSharpPreview = defineComponent({
  name: 'PrintSharpPreview',
  props: {
    source: { type: Object as PropType<PreviewSource>, required: true },
    page: { type: Number, default: 1 },
    zoom: { type: [Number, String] as PropType<Zoom>, default: 'fit-width' },
    viewMode: { type: String as PropType<ViewMode>, default: 'single-page' },
    textSelection: { type: Boolean, default: true },
    thumbnails: { type: Boolean, default: false },
    toolbar: { type: Boolean, default: true }
  },
  emits: ['update:page', 'update:zoom', 'update:viewMode', 'update:thumbnails', 'searchchange', 'selectionchange', 'update:textSelection', 'error', 'loadingchange'],
  setup(props, { emit, expose }) {
    const element = ref<HTMLElement>();
    let preview: PrintPreview | undefined;
    function mount() {
      preview?.destroy();
      if (!element.value) return;
      preview = createPrintPreview(element.value, { source: props.source, initialPage: props.page, zoom: props.zoom, viewMode: props.viewMode, thumbnails: props.thumbnails, toolbar: props.toolbar, textSelection: props.textSelection });
      preview.addEventListener('selectionchange', event => emit('selectionchange', (event as CustomEvent).detail));
      preview.addEventListener('textselectionchange', event => emit('update:textSelection', (event as CustomEvent).detail));
      preview.addEventListener('searchchange', event => emit('searchchange', (event as CustomEvent).detail));
      preview.addEventListener('pagechange', event => emit('update:page', (event as CustomEvent).detail));
      preview.addEventListener('zoomchange', event => emit('update:zoom', (event as CustomEvent).detail.zoom));
      preview.addEventListener('thumbnailschange', event => emit('update:thumbnails', (event as CustomEvent).detail));
      preview.addEventListener('viewmodechange', event => emit('update:viewMode', (event as CustomEvent).detail));
      preview.addEventListener('error', event => emit('error', (event as CustomEvent).detail));
      preview.addEventListener('loadingchange', event => emit('loadingchange', (event as CustomEvent).detail));
    }
    onMounted(mount); onBeforeUnmount(() => preview?.destroy());
    watch(() => [props.source, props.toolbar], mount);
    watch(() => props.page, page => { if (preview && preview.page !== page) void preview.goToPage(page).catch(() => {}); });
    watch(() => props.zoom, zoom => { if (preview && preview.zoom !== zoom) preview.setZoom(zoom); });
    watch(() => props.viewMode, mode => { if (preview && preview.viewMode !== mode) preview.setViewMode(mode); });
    watch(() => props.thumbnails, visible => { if (preview && preview.thumbnailsVisible !== visible) preview.setThumbnailsVisible(visible); });
    watch(() => props.textSelection, enabled => { if (preview && preview.textSelection !== enabled) preview.setTextSelection(enabled); });
    expose({ getPreview: () => preview });
    return () => h('div', { ref: element, style: { height: '100%', minHeight: '200px' } });
  }
});
