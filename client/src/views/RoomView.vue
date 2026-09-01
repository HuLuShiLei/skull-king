<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { activeSkin } from '@/skins/registry'
import { useRoomStore } from '@/stores/room'
import { useStealthStore } from '@/stores/stealth'

const props = defineProps<{ code: string }>()

const room = useRoomStore()
const stealth = useStealthStore()
const skin = activeSkin()
const route = useRoute()
const router = useRouter()

const copied = ref(false)
const failed = ref('')
const rosterOpen = ref(false)

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
  rosterOpen.value = false
  const password = typeof route.query.pw === 'string' ? route.query.pw : undefined
  const ok = await room.join(props.code, password)

  if (!ok) {
    failed.value = room.lastError || '无法加入这个群'
  } else {
    failed.value = ''
    void stealth.ensureNotifyPermission()

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
      <button class="btn btn-text back-btn" type="button" @click="router.push('/')">返回</button>

      <div class="title">
        <h1 class="ellipsis">{{ room.state?.settings.name ?? props.code }}</h1>
        <p class="secondary">
          群号 {{ props.code.toUpperCase() }} · {{ progress }}
        </p>
      </div>

      <div class="tools">
        <button class="btn btn-sm" @click="copyInvite">
          {{ copied ? '已复制' : '邀请' }}
        </button>
        <button class="btn btn-sm members-btn" type="button" @click="rosterOpen = !rosterOpen">
          成员
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

      <div class="roster-wrap" :class="{ open: rosterOpen }">
        <button
          class="roster-mask"
          type="button"
          aria-label="关闭成员列表"
          @click="rosterOpen = false"
        />
        <component :is="skin.roster" />
      </div>
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

.back-btn,
.members-btn,
.roster-mask {
  display: none;
}

.roster-wrap {
  display: flex;
  min-height: 0;
}

@media (max-width: 800px) {
  .topbar {
    padding: 8px 12px;
    gap: 8px;
  }

  .back-btn,
  .members-btn {
    display: inline-flex;
    flex: none;
  }

  .roster-wrap {
    display: block;
    position: fixed;
    inset: 0;
    z-index: 40;
    pointer-events: none;
  }

  .roster-wrap.open {
    pointer-events: auto;
  }

  .roster-mask {
    display: block;
    position: absolute;
    inset: 0;
    border: none;
    background: rgba(31, 35, 41, 0.28);
    opacity: 0;
    pointer-events: none;
  }

  .roster-wrap.open .roster-mask {
    opacity: 1;
    pointer-events: auto;
  }

  .roster-wrap :deep(.roster) {
    position: absolute;
    top: 0;
    right: 0;
    bottom: 0;
    width: min(300px, 86vw);
    padding-top: env(safe-area-inset-top);
    transform: translateX(100%);
    transition: transform 0.18s ease;
    box-shadow: var(--shadow-lg);
  }

  .roster-wrap.open :deep(.roster) {
    transform: translateX(0);
  }
}
</style>
