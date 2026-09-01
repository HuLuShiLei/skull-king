<script setup lang="ts">
import { onMounted, ref } from 'vue'

import ConversationList from './ConversationList.vue'
import GuideDialog from './GuideDialog.vue'
import SettingsDialog from './SettingsDialog.vue'
import { useConnectionStore } from '@/stores/connection'
import { useLobbyStore } from '@/stores/lobby'
import { useSessionStore } from '@/stores/session'
import { useStealthStore } from '@/stores/stealth'

const connection = useConnectionStore()
const lobby = useLobbyStore()
const session = useSessionStore()
const stealth = useStealthStore()

const settingsOpen = ref(false)
const guideOpen = ref(false)

onMounted(async () => {
  await connection.ensureStarted()
  await connection.hub.subscribeLobby()
  await lobby.refresh()
})
</script>

<template>
  <div class="shell">
    <aside class="rail">
      <div class="avatar rail-avatar" :title="session.nickname">
        {{ session.nickname.slice(0, 1) }}
      </div>

      <button class="rail-btn active" title="消息">
        <span>消息</span>
      </button>

      <div class="rail-spacer" />

      <button class="rail-btn" title="使用帮助" @click="guideOpen = true">
        <span>帮助</span>
      </button>

      <button class="rail-btn" title="设置" @click="settingsOpen = true">
        <span>设置</span>
      </button>

      <div class="rail-status" :class="{ off: !connection.connected }">
        {{ connection.connected ? '在线' : '连接中' }}
      </div>
    </aside>

    <ConversationList />

    <main class="content">
      <slot />
    </main>

    <SettingsDialog v-if="settingsOpen" @close="settingsOpen = false" />
    <GuideDialog v-if="guideOpen" @close="guideOpen = false" />

    <!-- 唯一的「正在玩游戏」提示，做成状态栏文字而不是浮层 -->
    <div v-if="stealth.unread > 0" class="taskbar-hint">{{ stealth.unread }} 条未读</div>
  </div>
</template>

<style scoped>
.shell {
  display: grid;
  grid-template-columns: 56px var(--sidebar-width) 1fr;
  height: 100%;
  overflow: hidden;
  background: var(--bg-app);
}

.rail {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  padding: 12px 0;
  background: var(--bg-sidebar);
  border-right: 1px solid var(--line);
}

.rail-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  margin-bottom: 6px;
}

.rail-btn {
  width: 44px;
  padding: 6px 0;
  border: none;
  border-radius: var(--radius);
  background: transparent;
  color: var(--text-secondary);
  font-size: 12px;
}

.rail-btn:hover,
.rail-btn.active {
  background: var(--bg-panel);
  color: var(--accent);
}

.rail-spacer {
  flex: 1;
}

.rail-status {
  font-size: 11px;
  color: var(--success);
}

.rail-status.off {
  color: var(--warning);
}

.content {
  display: flex;
  min-width: 0;
  /* grid 项默认 min-height:auto，不写这行的话消息一多就把整页撑高 */
  min-height: 0;
  background: var(--bg-chat);
}

.taskbar-hint {
  position: fixed;
  right: 12px;
  bottom: 8px;
  font-size: 11px;
  color: var(--text-muted);
  pointer-events: none;
}
</style>
