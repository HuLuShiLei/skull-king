<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'

import { activeSkin } from '@/skins/registry'
import { useReplayStore } from '@/stores/replay'

const props = defineProps<{ gameId: string }>()

const replay = useReplayStore()
const router = useRouter()
const skin = activeSkin()

const subtitle = computed(() => {
  const data = replay.data

  if (!data) {
    return '正在读取历史消息'
  }

  const when = new Date(data.startedAt).toLocaleString('zh-CN', {
    month: 'numeric',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })

  return `${when} · ${data.playerCount} 人 · ${data.totalRounds} 项议程`
})

onMounted(() => void replay.load(props.gameId))

watch(() => props.gameId, (gameId) => void replay.load(gameId))

onBeforeUnmount(() => replay.reset())
</script>

<template>
  <div class="replay">
    <header class="topbar">
      <div class="title">
        <h1 class="ellipsis">{{ replay.data?.roomName || '历史记录' }}</h1>
        <p class="secondary">{{ subtitle }}</p>
      </div>

      <div class="tools">
        <button
          v-if="replay.data"
          class="btn btn-sm"
          @click="replay.playing ? replay.stop() : replay.play()"
        >
          {{ replay.playing ? '停止' : '逐条重播' }}
        </button>
        <button v-if="replay.playing === false && replay.data" class="btn btn-sm" @click="replay.expandAll()">
          全部展开
        </button>
        <button class="btn btn-text btn-sm" @click="router.push('/')">返回</button>
      </div>
    </header>

    <p v-if="replay.loading" class="hint secondary">读取中…</p>
    <p v-else-if="replay.error" class="hint secondary">{{ replay.error }}</p>

    <component :is="skin.feed" v-else :feed="replay.feed" :nickname-of="replay.nicknameOf" />
  </div>
</template>

<style scoped>
.replay {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.topbar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 9px 20px;
  background: var(--bg-panel);
  border-bottom: 1px solid var(--line);
}

.title {
  min-width: 0;
  flex: 1;
}

h1 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.title p {
  margin: 1px 0 0;
  font-size: 12px;
}

.tools {
  display: flex;
  gap: 6px;
}

.hint {
  margin: 40px auto;
}
</style>
