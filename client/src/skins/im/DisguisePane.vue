<script setup lang="ts">
import { computed, ref } from 'vue'

import { DECOY_THREADS } from './decoyConversation'

const active = ref(0)
const draft = ref('')

const thread = computed(() => DECOY_THREADS[active.value])

const now = Date.now()

function clockOf(minutesAgo: number): string {
  return new Date(now - minutesAgo * 60_000).toLocaleTimeString('zh-CN', {
    hour: '2-digit',
    minute: '2-digit',
  })
}
</script>

<template>
  <!-- 伪装态：结构和真界面完全一致，但没有任何牌桌痕迹，也不显示未读角标 -->
  <div class="shell">
    <aside class="rail">
      <div class="avatar rail-avatar">我</div>
      <button class="rail-btn active"><span>消息</span></button>
      <div class="rail-spacer" />
      <button class="rail-btn"><span>设置</span></button>
    </aside>

    <nav class="list">
      <header class="head">
        <input class="input search" placeholder="搜索" readonly />
      </header>

      <div class="scroll">
        <button
          v-for="(item, index) in DECOY_THREADS"
          :key="item.name"
          class="item"
          :class="{ active: index === active }"
          @click="active = index"
        >
          <div class="avatar">{{ item.name.slice(0, 1) }}</div>
          <div class="body">
            <div class="row"><span class="name ellipsis">{{ item.name }}</span></div>
            <div class="row"><span class="preview ellipsis secondary">{{ item.preview }}</span></div>
          </div>
        </button>
      </div>
    </nav>

    <main class="content">
      <header class="topbar">
        <div>
          <h1>{{ thread.name }}</h1>
          <p class="secondary">{{ thread.messages.length }} 条消息</p>
        </div>
      </header>

      <div class="feed">
        <div
          v-for="(message, index) in thread.messages"
          :key="index"
          class="line"
          :class="{ self: message.self }"
        >
          <div v-if="!message.self" class="avatar sm">{{ message.from.slice(0, 1) }}</div>

          <div class="stack">
            <div v-if="!message.self" class="meta secondary">
              {{ message.from }} · {{ clockOf(message.minutesAgo) }}
            </div>
            <div class="bubble">{{ message.text }}</div>
            <div v-if="message.self" class="meta self-meta secondary">
              {{ clockOf(message.minutesAgo) }}
            </div>
          </div>
        </div>
      </div>

      <footer class="composer">
        <textarea v-model="draft" class="editor" rows="3" placeholder="输入消息…" />
        <div class="send-row">
          <button class="btn btn-primary btn-sm" disabled>发送</button>
        </div>
      </footer>
    </main>
  </div>
</template>

<style scoped>
.shell {
  display: grid;
  grid-template-columns: 56px var(--sidebar-width) 1fr;
  height: 100%;
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

.rail-spacer {
  flex: 1;
}

.list {
  display: flex;
  flex-direction: column;
  background: var(--bg-panel);
  border-right: 1px solid var(--line);
}

.head {
  padding: 10px 12px;
  border-bottom: 1px solid var(--line);
}

.search {
  height: 30px;
  background: var(--bg-hover);
  border-color: transparent;
}

.scroll {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}

.item {
  display: flex;
  gap: 10px;
  width: 100%;
  padding: 9px 12px;
  border: none;
  background: transparent;
  text-align: left;
}

.item:hover {
  background: var(--bg-hover);
}

.item.active {
  background: var(--bg-active);
}

.body {
  flex: 1;
  min-width: 0;
}

.row {
  display: flex;
  gap: 8px;
}

.name {
  flex: 1;
  font-size: 14px;
}

.preview {
  flex: 1;
  font-size: 12px;
}

.content {
  display: flex;
  flex-direction: column;
  min-width: 0;
  background: var(--bg-chat);
}

.topbar {
  padding: 10px 20px;
  background: var(--bg-panel);
  border-bottom: 1px solid var(--line);
}

h1 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.topbar p {
  margin: 2px 0 0;
  font-size: 12px;
}

.feed {
  flex: 1;
  overflow-y: auto;
  padding: 16px 20px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.line {
  display: flex;
  gap: 8px;
  max-width: 620px;
}

.line.self {
  align-self: flex-end;
  flex-direction: row-reverse;
}

.avatar.sm {
  width: 30px;
  height: 30px;
  font-size: 12px;
}

.stack {
  min-width: 0;
}

.meta {
  font-size: 12px;
  margin-bottom: 3px;
}

.self-meta {
  margin: 3px 0 0;
  text-align: right;
}

.bubble {
  display: inline-block;
  padding: 8px 12px;
  border-radius: var(--radius-lg);
  background: var(--bubble-other);
  box-shadow: var(--shadow);
  white-space: pre-wrap;
  word-break: break-word;
}

.line.self .bubble {
  background: var(--bubble-self);
}

.composer {
  border-top: 1px solid var(--line);
  background: var(--bg-panel);
  padding: 10px 20px 14px;
}

.editor {
  width: 100%;
  border: none;
  outline: none;
  resize: none;
  background: transparent;
}

.send-row {
  display: flex;
  justify-content: flex-end;
}
</style>
