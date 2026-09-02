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

// 认这张牌是否还在手上，换轮之后残留的选中态会自己失效。
const tigressCard = computed(
  () => game.value?.myHand.find((card) => card.id === tigressFor.value) ?? null,
)

/**
 * 自己报了几、已经拿下几。这两个数原来只在右边成员列表和消息流里，出牌的时候
 * 眼睛全在手牌上，回头翻记录才发现早就超了——所以钉在手牌正上方。
 */
const tally = computed(() => {
  const view = game.value
  const seat = room.yourSeat

  if (!view || waiting.value || seat < 0 || view.phase === 'Finished') {
    return null
  }

  const won = view.tricksWon[seat] ?? 0
  const bid = view.bids[seat] ?? null
  const diff = (bid ?? 0) - won

  // 报数阶段还没开打，说「还差几项」会让人以为漏了什么。
  const bidding = view.phase === 'Bidding'

  const note = bidding
    ? bid === null
      ? '等你填承接量'
      : '已填，等其他人'
    : diff > 0
      ? `还差 ${diff} 项`
      : diff === 0
        ? '已达标'
        : `已超 ${-diff} 项`

  return {
    bid: bid === null ? '—' : String(bid),
    won,
    note,
    tone: bidding || diff > 0 ? '' : diff === 0 ? 'ok' : 'bad',
    round: bidding
      ? `议程 ${view.roundNumber}/${view.totalRounds}`
      : `议程 ${view.roundNumber}/${view.totalRounds} · 第 ${view.trickNumber}/${view.cardsPerPlayer} 项`,
  }
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
    return tigressCard.value ? '机动人力先选算哪种，选完才算发出' : '轮到你处理，点下方任务卡即可'
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

    <!-- 自己这一轮的承接量和完成数，出牌时抬头就能看见 -->
    <div v-if="tally" class="tally">
      <span class="cell">
        <span class="k">本周承接</span>
        <strong>{{ tally.bid }}</strong>
      </span>

      <span class="cell">
        <span class="k">已完成</span>
        <strong>{{ tally.won }}</strong>
      </span>

      <span class="note" :class="tally.tone">{{ tally.note }}</span>

      <span class="round muted">{{ tally.round }}</span>
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

    <template v-else>
      <!--
        机动人力要先定这次算哪种。原来做成浮在牌上方的小菜单，会被快捷回复条的
        横向滚动整个裁掉——看着像点了没反应，其实牌根本没出。摊成一行就不会。
      -->
      <div v-if="tigressCard" class="quick-row choose">
        <span class="choose-label">机动人力这次算：</span>
        <button class="quick-item strong" @click="playTigress('AsPirate')">外部顾问</button>
        <button class="quick-item strong" @click="playTigress('AsEscape')">本项跳过</button>
        <button class="quick-item" @click="tigressFor = null">取消</button>
      </div>

      <!-- 快捷回复条：手牌 -->
      <div v-if="game && game.myHand.length > 0" class="quick-row cards">
        <div v-for="card in game.myHand" :key="card.id" class="card-slot">
          <CardChip
            :card="card"
            size="md"
            interactive
            :dimmed="!canPlay(card)"
            :selected="tigressFor === card.id"
            @click="play(card)"
          />
        </div>
      </div>
    </template>

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

.tally {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px 12px;
  padding: 6px 20px;
  border-bottom: 1px solid var(--line);
  font-size: 12px;
}

.cell {
  display: flex;
  align-items: baseline;
  gap: 4px;
}

.k {
  color: var(--text-muted);
}

.cell strong {
  font-size: 14px;
  font-variant-numeric: tabular-nums;
}

.note {
  padding: 1px 7px;
  border-radius: 10px;
  background: var(--bg-hover);
  color: var(--text-secondary);
}

.note.ok {
  background: var(--success-soft);
  color: var(--success);
}

.note.bad {
  background: var(--danger-soft);
  color: var(--danger);
}

.round {
  margin-left: auto;
  font-variant-numeric: tabular-nums;
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
  flex: 0 0 auto;
}

.choose {
  align-items: center;
}

.choose-label {
  color: var(--text-secondary);
  font-size: 13px;
  white-space: nowrap;
}

.quick-item.strong {
  border-color: var(--accent);
  color: var(--accent);
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

  .tally {
    padding: 6px 12px;
  }

  /* 手机上这行容易被挤到换行，轮次是参考信息，让它自己排到后面去 */
  .round {
    margin-left: 0;
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

  /* 输入框和发送按钮并排。竖着摆的话按钮单独占一行，键盘一弹更没地方站 */
  .composer {
    display: flex;
    align-items: flex-end;
    gap: 8px;
    padding: 6px 12px calc(8px + env(safe-area-inset-bottom));
  }

  .editor {
    flex: 1;
    min-width: 0;
    font-size: 16px;
    min-height: 36px;
  }

  .send-row {
    flex: none;
  }

  .tip {
    display: none;
  }
}
</style>
