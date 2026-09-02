<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

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
const route = useRoute()

const settingsOpen = ref(false)
const guideOpen = ref(false)

// 大厅时列表占满；进了群 / 历史 / 邀请落地页就只留聊天窗。
const focused = computed(() => route.name !== 'lobby')

onMounted(async () => {
  await connection.ensureStarted()
  await connection.hub.subscribeLobby()
  await lobby.refresh()
  await lobby.loadHistory()
})
</script>

<template>
  <div class="shell" :class="{ focused }">
    <aside class="rail">
      <div class="avatar rail-avatar" :title="session.nickname">
        {{ session.nickname.slice(0, 1) }}
      </div>

      <button class="rail-btn" :class="{ active: !focused }" title="消息">
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

.rail-btn.active {
  background: var(--bg-panel);
  color: var(--accent);
}

@media (hover: hover) {
  .rail-btn:hover {
    background: var(--bg-panel);
    color: var(--accent);
  }
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

@media (max-width: 800px) {
  .shell {
    grid-template-columns: 1fr;
    grid-template-rows: auto 1fr;
  }

  .rail {
    flex-direction: row;
    justify-content: flex-start;
    gap: 6px;
    padding: 6px 10px;
    padding-top: calc(6px + env(safe-area-inset-top));
    padding-left: calc(10px + env(safe-area-inset-left));
    padding-right: calc(10px + env(safe-area-inset-right));
    border-right: none;
    border-bottom: 1px solid var(--line);
  }

  .rail-avatar {
    margin-bottom: 0;
  }

  .rail-btn {
    width: auto;
    min-height: 36px;
    padding: 6px 10px;
  }

  .content {
    display: none;
  }

  .shell.focused :deep(.list) {
    display: none;
  }

  .shell.focused .content {
    display: flex;
  }

  .taskbar-hint {
    bottom: calc(8px + env(safe-area-inset-bottom));
  }
}
</style>
