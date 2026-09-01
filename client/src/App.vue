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

  <!--
    伪装期间真实界面只是藏起来，绝不能销毁：RoomView 卸载会触发退房，
    那样按一下老板键就等于退出了对局。display:none 一样选不中、搜不到，
    藏得够彻底。
  -->
  <div v-show="!stealth.disguised" class="app-root">
    <div v-if="failed" class="boot">
      <p>{{ failed }}</p>
      <button class="btn" @click="reload">重新加载</button>
    </div>

    <component :is="skin.onboarding" v-else-if="session.needsProfile" />

    <div v-else-if="!session.ready" class="boot muted">正在连接…</div>

    <component :is="skin.layout" v-else>
      <RouterView />
    </component>
  </div>
</template>

<style scoped>
/* 不生成盒子，撑满高度的活儿还是留给里面的布局组件。
   v-show 关掉时内联的 display:none 会盖过它。 */
.app-root {
  display: contents;
}

.boot {
  display: flex;
  flex-direction: column;
  gap: 12px;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
