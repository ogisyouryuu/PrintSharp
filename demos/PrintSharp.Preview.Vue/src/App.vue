<script setup lang="ts">
import { ref, shallowRef, onBeforeUnmount } from 'vue';
import { createHttpPreviewSource, type SearchState, type Manifest, type PreviewSource, type ViewMode, type Zoom } from '@printsharp/preview';
import { PrintSharpPreview } from '@printsharp/preview/vue';

const data = ref('{"tables":[{"name":"T","columns":[{"name":"Value","type":"string"}],"rows":[{"Value":"中文 日本語 한국어 العربية مرحبًا / Hello PrintSharp"}]}]}');
const file = shallowRef<File>();
const manifest = shallowRef<Manifest>();
const source = shallowRef<PreviewSource>();
const page = ref(1);
const zoom = ref<Zoom>('fit-width');
const viewMode = ref<ViewMode>('single-page');
const thumbnails = ref(false);
const textSelection = ref(true);
const selectedText = ref('');
const searchState = ref<SearchState>();
const busy = ref(false);
const error = ref('');
const loaded = ref(false);

function selectFile(event: Event) { file.value = (event.target as HTMLInputElement).files?.[0]; }
async function create(useSample: boolean) {
  busy.value = true; error.value = '';
  try {
    JSON.parse(data.value);
    const template = useSample ? await (await fetch('/template.xlsx')).blob() : file.value;
    if (!template) throw new Error('请选择 Excel 模板，或使用内置示例');
    const form = new FormData(); form.append('template', template, 'template.xlsx'); form.append('data', data.value);
    const response = await fetch('/api/v1/previews/upload', { method: 'POST', body: form });
    const next = await response.json();
    if (!response.ok) throw new Error(next.detail ?? `HTTP ${response.status}`);
    const old = manifest.value;
    manifest.value = next; page.value = 1; zoom.value = 'fit-width'; loaded.value = false; searchState.value = undefined; selectedText.value = '';
    source.value = createHttpPreviewSource({ baseUrl: '/api/v1/previews', documentId: next.documentId });
    if (old) void fetch(`/api/v1/previews/${old.documentId}`, { method: 'DELETE' });
  } catch (exception) { error.value = String(exception); }
  finally { busy.value = false; }
}
onBeforeUnmount(() => { source.value = undefined; });
</script>

<template>
  <main>
    <header><span class="badge">VUE 3 · TYPESCRIPT</span><h1>PrintSharp 预览验收</h1><p>后端统一排版，Canvas 按页绘制；下载 PDF 使用同一个文档快照。</p></header>
    <section class="inputs">
      <label>自定义 Excel 模板 <input type="file" accept=".xlsx" @change="selectFile"></label>
      <label for="data">数据 JSON</label><textarea id="data" v-model="data" spellcheck="false"></textarea>
      <div class="actions">
        <button :disabled="busy" @click="create(true)">使用内置两页示例</button>
        <button :disabled="busy || !file" @click="create(false)">上传并预览</button>
        <a href="/template.xlsx" download>下载示例模板</a>
        <a v-if="manifest" :href="`/api/v1/previews/${manifest.documentId}/pdf`" download>下载同一快照 PDF</a>
      </div>
    </section>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="manifest" class="summary" data-testid="summary">{{ manifest.pageCount }} 页 · 当前第 {{ page }} 页 · {{ viewMode === 'continuous' ? '连续滚动' : '单页' }} · 缩略图{{ thumbnails ? '已打开' : '已关闭' }} · 缩放 {{ zoom }} · {{ loaded ? '绘制完成' : '加载中' }}</p>
    <p v-if="searchState?.query" data-testid="search-summary">搜索 {{ searchState.query }}：{{ searchState.current }} / {{ searchState.total }}</p>
    <p v-if="manifest" class="selection-summary" data-testid="selection-summary">{{ selectedText ? `已选择：${selectedText}` : '未选择文字' }}</p>
    <div v-if="source" class="preview">
      <PrintSharpPreview :source="source" v-model:page="page" v-model:zoom="zoom" v-model:view-mode="viewMode" v-model:thumbnails="thumbnails" v-model:text-selection="textSelection" @selectionchange="selectedText = $event" @searchchange="searchState = $event" @loadingchange="loaded = !$event" @error="error = String($event)" />
    </div>
    <section v-else class="empty">点击“使用内置两页示例”即可验收，无需准备文件。</section>
  </main>
</template>

<style>
.selection-summary{height:1.5em;line-height:1.5;white-space:pre;overflow:hidden;text-overflow:ellipsis}
body{margin:0;background:#f4f6fa;color:#172033;font:15px system-ui,sans-serif}main{max-width:1100px;margin:32px auto;padding:0 24px}.badge{font-size:12px;letter-spacing:2px;color:#315ac1}h1{font-size:30px;margin:10px 0}header p{color:#59657a}.inputs{display:grid;gap:10px;background:#fff;border:1px solid #d9e0eb;border-radius:10px;padding:18px;margin-top:24px}textarea{min-height:85px;resize:vertical;padding:10px;font:14px monospace;border:1px solid #bdc8da;border-radius:5px}.actions{display:flex;align-items:center;gap:15px;flex-wrap:wrap}button{padding:9px 14px;border:1px solid #b7c7e5;border-radius:5px;background:#edf3ff;color:#244ea6;cursor:pointer}button:disabled{opacity:.5;cursor:default}a{color:#244ea6}.preview{height:600px}.summary{font-variant-numeric:tabular-nums;color:#38537c}.empty{margin-top:20px;padding:70px;text-align:center;border:1px dashed #b7c7e5;border-radius:10px}.error{color:#a92727;white-space:pre-wrap}
</style>
