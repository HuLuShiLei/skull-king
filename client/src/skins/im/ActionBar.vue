<script setup lang="ts">
import { computed, ref } from 'vue'

import CardChip from './CardChip.vue'
import { bidText } from './vocabulary'
import type { CardDto } from '@/api/types'
import { useRoomStore } from '@/stores/room'

const room = useRoomStore()

const draft = ref('')
const tigressFor = ref<string | null>(null)

const game = computed(() => room.game)
const finished = computed(() => room.state?.status === 2)

// 上一局结束后房间会回到可准备状态，这里和「从没开过局」一视同仁。
const waiting = computed(() => room.state?.status !== 1)

const bidOptions = computed(() => {
  const max = game.value?.cardsPerPlayer ?? 0
  return Array.from({ length: max + 1 }, (_, index) => index)
})

const hint = computed(() => {
  const view = game.value

  if (finished.value) {
    return room.isHost ? '已结束，可以再发起一轮' : '已结束，等待群主再次发起'
  }

  if (waiting.value) {
    return room.isHost ? '所有人确认后即可发起' : '等待群主发起'
  }

  if (!view) {
    return ''
  }

  if (view.phase === 'Bidding') {
    return room.needsBid ? '请填写本周承接量' : '等待其他成员填写'
  }

  if (room.isYourTurn) {
    return '轮到你处理，点下方任务卡即可'
  }

  return `等待 ${room.nicknameOf(view.currentSeat)} 处理`
})

const canPlay = (card: CardDto) => game.value?.playableCardIds.includes(card.id) ?? false

async function play(card: CardDto) {
  if (!canPlay(card)) {
    return
  }

  // Tigress 出牌前要先定形态，这是规则里唯一需要玩家二选一的地方。
  if (card.kind === 'Tigress') {
    tigressFor.value = tigressFor.value === card.id ? null : card.id
    return
  }

  await room.playCard(card.id)
}

async function playTigress(mode: 'AsPirate' | 'AsEscape') {
  const cardId = tigressFor.value

  if (!cardId) {
    return
  }

  tigressFor.value = null
  await room.playCard(cardId, mode)
}

async function send() {
  const text = draft.value.trim()

  if (!text) {
    return
  }

  draft.value = ''
  await room.sendChat(text)
}
</script>

<template>
  <footer class="bar">
    <div class="status">
      <span class="hint">{{ hint }}</span>

      <span v-if="room.secondsLeft !== null && !waiting" class="timer">{{ room.secondsLeft }}s</span>

      <span v-if="room.lastError" class="error">{{ room.lastError }}</span>

      <div class="status-actions">
        <template v-if="waiting">
          <button
            v-if="room.yourSeat < 0"
            class="btn btn-sm"
            @click="room.sitDown()"
          >
            加入讨论
          </button>
          <template v-else>
            <button
              v-if="!room.isHost"
              class="btn btn-sm"
              :class="{ 'btn-primary': !room.you?.isReady }"
              @click="room.setReady(!room.you?.isReady)"
            >
              {{ room.you?.isReady ? '取消确认' : '确认参加' }}
            </button>
            <button v-if="room.isHost" class="btn btn-sm btn-primary" @click="room.startGame()">
              {{ finished ? '再发起一轮' : '发起议程' }}
            </button>
          </template>
        </template>
      </div>
    </div>

    <!-- 叫牌：选本周承接量 -->
    <div v-if="room.needsBid" class="quick-row">
      <button
        v-for="option in bidOptions"
        :key="option"
        class="quick-item"
        @click="room.placeBid(option)"
      >
        {{ bidText(option) }}
      </button>
    </div>

    <!-- 快捷回复条：手牌 -->
    <div v-else-if="game && game.myHand.length > 0" class="quick-row cards">
      <div v-for="card in game.myHand" :key="card.id" class="card-slot">
        <CardChip
          :card="card"
          size="md"
          interactive
          :dimmed="!canPlay(card)"
          :selected="tigressFor === card.id"
          @click="play(card)"
        />

        <div v-if="tigressFor === card.id" class="tigress-menu">
          <button @click="playTigress('AsPirate')">当作外部顾问</button>
          <button @click="playTigress('AsEscape')">当作本项跳过</button>
        </div>
      </div>
    </div>

    <div class="composer">
      <textarea
        v-model="draft"
        class="editor"
        rows="1"
        placeholder="输入消息，回车发送"
        @keydown.enter.exact.prevent="send"
      />
      <div class="send-row">
        <span class="muted tip">Enter 发送 · Shift+Enter 换行</span>
        <button class="btn btn-sm btn-primary" :disabled="!draft.trim()" @click="send">发送</button>
      </div>
    </div>
  </footer>
</template>

<style scoped>
.bar {
  /* 输入区不参与收缩，空间不够时让上面的消息区自己滚 */
  flex: none;
  border-top: 1px solid var(--line);
  background: var(--bg-panel);
}

.status {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 7px 20px;
  border-bottom: 1px solid var(--line);
  font-size: 12px;
}

.hint {
  color: var(--text-secondary);
}

.timer {
  font-variant-numeric: tabular-nums;
  color: var(--text-muted);
}

.error {
  color: var(--danger);
}

.status-actions {
  margin-left: auto;
  display: flex;
  gap: 6px;
}

.quick-row {
  display: flex;
  gap: 8px;
  padding: 9px 20px;
  overflow-x: auto;
  border-bottom: 1px solid var(--line);
}

.quick-item {
  padding: 5px 12px;
  border: 1px solid var(--line-strong);
  border-radius: 14px;
  background: var(--bg-panel);
  color: var(--text-secondary);
  font-size: 13px;
  white-space: nowrap;
}

@media (hover: hover) {
  .quick-item:hover {
    border-color: var(--accent);
    color: var(--accent);
  }
}

.card-slot {
  position: relative;
  flex: 0 0 auto;
}

.tigress-menu {
  position: absolute;
  bottom: calc(100% + 6px);
  left: 0;
  z-index: 20;
  display: flex;
  flex-direction: column;
  min-width: 140px;
  padding: 4px;
  border: 1px solid var(--line);
  border-radius: var(--radius);
  background: var(--bg-panel);
  box-shadow: var(--shadow-lg);
}

.tigress-menu button {
  padding: 6px 10px;
  border: none;
  border-radius: 4px;
  background: transparent;
  text-align: left;
  font-size: 13px;
}

.tigress-menu button:hover {
  background: var(--bg-hover);
}

.composer {
  padding: 8px 20px 12px;
}

.editor {
  width: 100%;
  min-height: 44px;
  border: none;
  outline: none;
  resize: none;
  background: transparent;
}

.send-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.tip {
  font-size: 11px;
}

@media (max-width: 800px) {
  .status {
    flex-wrap: wrap;
    padding: 8px 12px;
    gap: 6px;
  }

  .hint {
    flex: 1 1 140px;
    min-width: 0;
  }

  .quick-row {
    padding: 8px 12px;
    -webkit-overflow-scrolling: touch;
  }

  .quick-row:not(.cards) {
    flex-wrap: wrap;
    overflow-x: visible;
  }

  .quick-item {
    min-height: 36px;
  }

  .composer {
    padding: 6px 12px calc(8px + env(safe-area-inset-bottom));
  }

  .editor {
    font-size: 16px;
    min-height: 24px;
  }

  .tip {
    display: none;
  }
}
</style>
