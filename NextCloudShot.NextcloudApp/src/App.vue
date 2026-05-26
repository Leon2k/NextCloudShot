<template>
  <main class="nextcloudshot">
    <header class="toolbar">
      <div>
        <h2>Скриншоты</h2>
        <p>Screenshots from the desktop client</p>
      </div>
      <NcButton type="primary" @click="load">Refresh</NcButton>
    </header>

    <NcEmptyContent v-if="!loading && items.length === 0" name="No screenshots yet">
      <template #description>The client will save screenshots into {{ folder }}.</template>
    </NcEmptyContent>

    <div v-else class="gallery">
      <button v-for="item in items" :key="item.id" type="button" class="shot" @click="selected = item">
        <img :src="item.previewUrl" :alt="item.name">
        <div class="meta">
          <strong :title="item.name">{{ item.name }}</strong>
          <span>{{ formatDate(item.modifiedAt) }} - {{ formatSize(item.size) }}</span>
        </div>
      </button>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="selected" class="viewer" role="dialog" aria-modal="true" @click.self="selected = null">
      <button type="button" class="viewer-close" aria-label="Close preview" @click="selected = null">×</button>
      <figure>
        <img :src="selected.fullPreviewUrl ?? selected.previewUrl" :alt="selected.name">
        <figcaption>
          <strong>{{ selected.name }}</strong>
          <span>{{ formatDate(selected.modifiedAt) }} - {{ formatSize(selected.size) }}</span>
        </figcaption>
      </figure>
    </div>
  </main>
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'
import axios from '@nextcloud/axios'
import { generateUrl } from '@nextcloud/router'
import NcButton from '@nextcloud/vue/components/NcButton'
import NcEmptyContent from '@nextcloud/vue/components/NcEmptyContent'

const items = ref([])
const folder = ref('/Screenshots')
const loading = ref(false)
const error = ref('')
const selected = ref(null)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const { data } = await axios.get(generateUrl('/apps/nextcloudshot/api/screenshots'))
    folder.value = data.folder
    items.value = data.screenshots
  } catch (exception) {
    error.value = exception.message ?? 'Unable to load gallery.'
  } finally {
    loading.value = false
  }
}

function formatDate(value) { return new Date(value).toLocaleString() }
function formatSize(bytes) { return bytes < 1024 * 1024 ? `${Math.round(bytes / 1024)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB` }
function closeOnEscape(event) {
  if (event.key === 'Escape') {
    selected.value = null
  }
}

onMounted(() => {
  load()
  window.addEventListener('keydown', closeOnEscape)
})
onBeforeUnmount(() => window.removeEventListener('keydown', closeOnEscape))
</script>

<style scoped lang="scss">
.nextcloudshot { padding: 28px; max-width: 1440px; margin: 0 auto; }
.toolbar { display: flex; justify-content: space-between; gap: 24px; align-items: end; margin-bottom: 28px; }
h2 { margin: 0 0 4px; font-size: 26px; }
p { margin: 0; color: var(--color-text-maxcontrast); }
.gallery { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 18px; }
.shot { background: var(--color-background-dark); border-radius: var(--border-radius-large); overflow: hidden; border: 1px solid var(--color-border); color: inherit; cursor: zoom-in; padding: 0; text-align: left; }
.shot:hover,
.shot:focus-visible { border-color: var(--color-primary-element); box-shadow: 0 0 0 2px var(--color-primary-element-light); outline: none; }
.shot img { display: block; width: 100%; height: 178px; object-fit: cover; background: #131820; }
.meta { padding: 11px 13px; display: flex; flex-direction: column; gap: 5px; }
.meta strong { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.meta span { color: var(--color-text-maxcontrast); font-size: 13px; }
.error { margin-top: 20px; color: var(--color-error); }
.viewer { position: fixed; inset: 0; z-index: 10000; display: flex; align-items: center; justify-content: center; padding: 48px; background: rgba(0, 0, 0, .82); }
.viewer figure { margin: 0; max-width: min(1200px, 94vw); max-height: 92vh; display: flex; flex-direction: column; gap: 14px; }
.viewer img { max-width: 100%; max-height: calc(92vh - 58px); object-fit: contain; border-radius: var(--border-radius-large); background: #111; }
.viewer figcaption { display: flex; justify-content: space-between; gap: 24px; color: white; }
.viewer figcaption span { color: rgba(255, 255, 255, .72); }
.viewer-close { position: fixed; top: 18px; right: 22px; min-width: 44px; min-height: 44px; border: 0; border-radius: 50%; background: rgba(255, 255, 255, .14); color: white; font-size: 32px; line-height: 1; cursor: pointer; }
.viewer-close:hover,
.viewer-close:focus-visible { background: rgba(255, 255, 255, .24); outline: none; }
</style>
