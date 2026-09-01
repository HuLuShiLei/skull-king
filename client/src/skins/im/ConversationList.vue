<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import RoomEntryDialog from './RoomEntryDialog.vue'
import type { RoomSummaryDto } from '@/api/types'
import { useLobbyStore } from '@/stores/lobby'
import { useRoomStore } from '@/stores/room'

const lobby = useLobbyStore()
const room = useRoomStore()
const route = useRoute()
const router = useRouter()

const keyword = ref('')
const dialogTab = ref<'create' | 'join' | null>(null)

const visible = computed(() => {
  const text = keyword.value.trim().toLowerCase()

  if (!text) {
    return lobby.rooms
  }

  return lobby.rooms.filter(
    (r) => r.name.toLowerCase().includes(text) || r.code.toLowerCase().includes(text),
  )
})

function statusText(item: RoomSummaryDto): string {
  const base = `${item.playerCount}/${item.maxPlayers} 人`
  return item.status === 1 ? `${base} · 讨论进行中` : `${base} · 等待成员加入`
}

function open(item: RoomSummaryDto) {
  void router.push({ name: 'room', params: { code: item.code } })
}
</script>

<template>
  <nav class="list">
    <header class="head">
      <input v-model="keyword" class="input search" placeholder="搜索" />
      <button class="btn btn-text add" title="新建或加入" @click="dialogTab = 'create'">＋</button>
    </header>

    <div class="scroll">
      <p v-if="lobby.rooms.length === 0" class="empty muted">
        暂无会话，点右上角 ＋ 新建一个
      </p>

      <button
        v-for="item in visible"
        :key="item.code"
        class="item"
        :class="{ active: route.params.code === item.code }"
        @click="open(item)"
      >
        <div class="avatar">{{ item.name.slice(0, 1) }}</div>

        <div class="body">
          <div class="row">
            <span class="name ellipsis">{{ item.name }}</span>
            <span class="code">{{ item.code }}</span>
          </div>
          <div class="row">
            <span class="preview ellipsis secondary">{{ statusText(item) }}</span>
            <span v-if="item.hasPassword" class="lock muted" title="需要口令">锁</span>
          </div>
        </div>
      </button>
    </div>

    <footer v-if="room.code" class="foot">
      <span class="ellipsis secondary">当前：{{ room.state?.settings.name ?? room.code }}</span>
      <button class="btn btn-text btn-sm" @click="room.leave().then(() => router.push('/'))">
        退出
      </button>
    </footer>

    <RoomEntryDialog v-if="dialogTab" :tab="dialogTab" @close="dialogTab = null" />
  </nav>
</template>

<style scoped>
.list {
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: var(--bg-panel);
  border-right: 1px solid var(--line);
}

.head {
  display: flex;
  gap: 6px;
  align-items: center;
  padding: 10px 12px;
  border-bottom: 1px solid var(--line);
}

.search {
  height: 30px;
  background: var(--bg-hover);
  border-color: transparent;
}

.add {
  font-size: 18px;
  line-height: 1;
  padding: 2px 8px;
}

.scroll {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}

.empty {
  padding: 24px 16px;
  text-align: center;
  font-size: 13px;
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
  align-items: baseline;
  gap: 8px;
}

.name {
  flex: 1;
  font-size: 14px;
}

.code,
.lock {
  font-size: 11px;
  color: var(--text-muted);
}

.preview {
  flex: 1;
  font-size: 12px;
}

.foot {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-top: 1px solid var(--line);
  font-size: 12px;
}

.foot > span {
  flex: 1;
}
</style>
