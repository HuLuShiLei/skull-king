<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { activeSkin } from '@/skins/registry'
import { useRoomStore } from '@/stores/room'

const props = defineProps<{ code: string }>()

const room = useRoomStore()
const skin = activeSkin()
const route = useRoute()
const router = useRouter()

const copied = ref(false)
const failed = ref('')

// 带上口令，对方点开就能直接进，不用你再单独把口令发一遍。
const inviteLink = computed(() => {
  const url = `${location.origin}/j/${props.code.toUpperCase()}`
  const password = room.state?.settings.password

  return password ? `${url}?pw=${encodeURIComponent(password)}` : url
})

const progress = computed(() => {
  const view = room.game

  if (!view) {
    return '尚未开始'
  }

  if (view.phase === 'Finished') {
    return '已结束'
  }

  return `议程 ${view.roundNumber}/${view.totalRounds} · 第 ${view.trickNumber} 项`
})

async function enter() {
  const password = typeof route.query.pw === 'string' ? route.query.pw : undefined
  const ok = await room.join(props.code, password)

  if (!ok) {
    failed.value = room.lastError || '无法加入这个群'
  } else {
    failed.value = ''

    // 口令留在地址栏里既不安全也容易被瞥见，进房后立刻抹掉。
    if (route.query.pw) {
      await router.replace({ name: 'room', params: { code: props.code } })
    }
  }
}

async function copyInvite() {
  try {
    await navigator.clipboard.writeText(inviteLink.value)
    copied.value = true
    setTimeout(() => (copied.value = false), 1800)
  } catch {
    failed.value = '复制失败，请手动复制链接'
  }
}

onMounted(enter)

watch(() => props.code, enter)

watch(
  () => room.removedReason,
  (reason) => {
    if (reason) {
      failed.value = reason
    }
  },
)

onBeforeUnmount(() => {
  void room.leave()
})
</script>

<template>
  <div class="room">
    <header class="topbar">
      <div class="title">
        <h1 class="ellipsis">{{ room.state?.settings.name ?? props.code }}</h1>
        <p class="secondary">
          群号 {{ props.code.toUpperCase() }} · {{ progress }}
        </p>
      </div>

      <div class="tools">
        <button class="btn btn-sm" @click="copyInvite">
          {{ copied ? '链接已复制' : '邀请' }}
        </button>
      </div>
    </header>

    <p v-if="failed" class="failed">
      {{ failed }}
      <button class="btn btn-text btn-sm" @click="router.push('/')">返回</button>
    </p>

    <div v-else class="body">
      <section class="chat">
        <component
          :is="skin.feed"
          :feed="room.feed"
          :nickname-of="room.nicknameOf"
          :your-player-id="room.state?.yourPlayerId"
          :your-seat="room.yourSeat"
        />
        <component :is="skin.actions" />
      </section>

      <component :is="skin.roster" />
    </div>
  </div>
</template>

<style scoped>
.room {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  min-height: 0;
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

.failed {
  margin: 40px auto;
  color: var(--text-secondary);
}

.body {
  display: flex;
  flex: 1;
  min-height: 0;
}

.chat {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  min-height: 0;
}
</style>
