<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import ModalShell from './ModalShell.vue'
import RoomEntryDialog from './RoomEntryDialog.vue'
import { RoomStatusValues, type RoomSummaryDto } from '@/api/types'
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

/**
 * 一个人不能同时坐两张牌桌，所以点开别的群等于离开手上这个。
 * 后果不小（等人中的群会退群、只剩自己的话直接解散），先问一句。
 */
const pending = ref<RoomSummaryDto | null>(null)

const leavingPlaying = computed(() => room.state?.status === RoomStatusValues.Playing)

const alone = computed(() => (room.state?.members.length ?? 0) <= 1)

function open(item: RoomSummaryDto) {
  if (room.code && room.code !== item.code) {
    pending.value = item
    return
  }

  void go(item)
}

function go(item: RoomSummaryDto) {
  pending.value = null

  return router.push({ name: 'room', params: { code: item.code } })
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

      <section v-if="lobby.history.length > 0" class="history-pane">
        <h3>最近记录</h3>
        <button
          v-for="item in lobby.history"
          :key="item.gameId"
          class="item"
          @click="router.push({ name: 'replay', params: { gameId: item.gameId } })"
        >
          <div class="body">
            <div class="row">
              <span class="name ellipsis">{{ item.roomName || item.roomCode }}</span>
              <span class="code">{{ item.nicknames.length }} 人</span>
            </div>
            <div class="row">
              <span class="preview ellipsis secondary">
                {{ item.yourScore }} 分{{ item.youWon ? ' · 第一' : '' }}
              </span>
            </div>
          </div>
        </button>
      </section>
    </div>

    <footer v-if="room.code" class="foot">
      <span class="ellipsis secondary">当前：{{ room.state?.settings.name ?? room.code }}</span>
      <button class="btn btn-text btn-sm" @click="room.leave().then(() => router.push('/'))">
        退出
      </button>
    </footer>

    <RoomEntryDialog v-if="dialogTab" :tab="dialogTab" @close="dialogTab = null" />

    <ModalShell v-if="pending" title="切换会话" :width="400" @close="pending = null">
      <div class="confirm">
        <p>
          要离开「{{ room.state?.settings.name ?? room.code }}」，去「{{ pending.name }}」吗？
        </p>

        <p v-if="leavingPlaying" class="secondary">
          讨论未结束。切走后由系统代为处理，随时可回来。
        </p>
        <p v-else-if="alone" class="secondary">群里只剩你，切过去这个群会解散。</p>
        <p v-else class="secondary">等于退出本群。没人了会自动解散。</p>

        <div class="actions">
          <button class="btn" @click="pending = null">留在这里</button>
          <button class="btn btn-primary" @click="go(pending)">切过去</button>
        </div>
      </div>
    </ModalShell>
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

.item.active {
  background: var(--bg-active);
}

@media (hover: hover) {
  .item:hover {
    background: var(--bg-hover);
  }
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

.confirm {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.confirm p {
  margin: 0;
  font-size: 13px;
  line-height: 1.6;
}

.actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 4px;
}

.history-pane {
  display: none;
}

@media (max-width: 800px) {
  .item {
    min-height: 56px;
    padding: 12px 16px;
  }

  .add {
    min-width: 36px;
    min-height: 36px;
    font-size: 22px;
  }

  .history-pane {
    display: block;
    margin-top: 4px;
    padding-top: 8px;
    border-top: 1px solid var(--line);
  }

  .history-pane h3 {
    margin: 0;
    padding: 8px 16px 4px;
    font-size: 12px;
    font-weight: 500;
    color: var(--text-muted);
  }

  .foot {
    padding-bottom: calc(8px + env(safe-area-inset-bottom));
  }
}
</style>
