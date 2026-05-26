<template>
  <main class="nextcloudshot">
    <header class="toolbar">
      <div>
        <h2>Скриншоты</h2>
        <p>Screenshots from the desktop client</p>
      </div>
      <div class="settings">
        <input v-model="folder" aria-label="Screenshots folder" placeholder="/Screenshots">
        <NcButton type="secondary" @click="saveFolder">Save folder</NcButton>
        <NcButton type="primary" @click="load">Refresh</NcButton>
      </div>
    </header>

    <NcEmptyContent v-if="!loading && items.length === 0" name="No screenshots yet">
      <template #description>The client will save screenshots into {{ folder }}.</template>
    </NcEmptyContent>

    <div v-else class="gallery">
      <article v-for="item in items" :key="item.id" class="shot">
        <img :src="item.previewUrl" :alt="item.name">
        <div class="meta">
          <strong :title="item.name">{{ item.name }}</strong>
          <span>{{ formatDate(item.modifiedAt) }} - {{ formatSize(item.size) }}</span>
        </div>
      </article>
    </div>

    <p v-if="error" class="error">{{ error }}</p>
  </main>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import axios from '@nextcloud/axios'
import { generateUrl } from '@nextcloud/router'
import NcButton from '@nextcloud/vue/components/NcButton'
import NcEmptyContent from '@nextcloud/vue/components/NcEmptyContent'

const items = ref([])
const folder = ref('/Screenshots')
const loading = ref(false)
const error = ref('')

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

async function saveFolder() {
  error.value = ''
  try {
    await axios.put(generateUrl('/apps/nextcloudshot/api/settings'), { folder: folder.value })
    await load()
  } catch (exception) {
    error.value = exception.message ?? 'Unable to save folder.'
  }
}

function formatDate(value) { return new Date(value).toLocaleString() }
function formatSize(bytes) { return bytes < 1024 * 1024 ? `${Math.round(bytes / 1024)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB` }

onMounted(load)
</script>

<style scoped lang="scss">
.nextcloudshot { padding: 28px; max-width: 1440px; margin: 0 auto; }
.toolbar { display: flex; justify-content: space-between; gap: 24px; align-items: end; margin-bottom: 28px; }
h2 { margin: 0 0 4px; font-size: 26px; }
p { margin: 0; color: var(--color-text-maxcontrast); }
.settings { display: flex; align-items: center; gap: 10px; }
.settings input { width: 280px; padding: 9px 12px; border: 1px solid var(--color-border); border-radius: var(--border-radius-large); }
.gallery { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 18px; }
.shot { background: var(--color-background-dark); border-radius: var(--border-radius-large); overflow: hidden; border: 1px solid var(--color-border); }
.shot img { display: block; width: 100%; height: 178px; object-fit: cover; background: #131820; }
.meta { padding: 11px 13px; display: flex; flex-direction: column; gap: 5px; }
.meta strong { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.meta span { color: var(--color-text-maxcontrast); font-size: 13px; }
.error { margin-top: 20px; color: var(--color-error); }
</style>
