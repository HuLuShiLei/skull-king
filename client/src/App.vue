<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'

import { activeSkin } from '@/skins/registry'
import { useSessionStore } from '@/stores/session'
import { useStealthStore } from '@/stores/stealth'

const session = useSessionStore()
const stealth = useStealthStore()
const failed = ref('')
const skin = activeSkin()

onMounted(async () => {
  stealth.install()

  try {
    await session.ensure()
  } catch (error) {
    failed.value = error instanceof Error ? error.message : '无法连接服务'
  }
})

onBeforeUnmount(() => stealth.uninstall())

function reload() {
  window.location.reload()
}
</script>

<template>
  <component :is="skin.disguise" v-if="stealth.disguised" />

  <div v-else-if="failed" class="boot">
    <p>{{ failed }}</p>
    <button class="btn" @click="reload">重新加载</button>
  </div>

  <div v-else-if="!session.ready" class="boot muted">正在连接…</div>

  <component :is="skin.layout" v-else>
    <RouterView />
  </component>
</template>

<style scoped>
.boot {
  display: flex;
  flex-direction: column;
  gap: 12px;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
