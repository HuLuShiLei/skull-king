<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'

import CardChip from './CardChip.vue'
import { bidText, scoreText, WIN_REASON_TEXT } from './vocabulary'
import type { FeedItem } from '@/stores/feed'

// 只吃数据不认 store，这样实时房间和历史回放能共用同一套渲染。
const props = withDefaults(
  defineProps<{
    feed: FeedItem[]
    nicknameOf: (seat: number) => string
    yourPlayerId?: string | null
    yourSeat?: number
  }>(),
  { yourPlayerId: null, yourSeat: -1 },
)

const scroller = ref<HTMLElement | null>(null)
const mySeat = computed(() => props.yourSeat)

function clockOf(at: number): string {
  return new Date(at).toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
}

watch(
  () => props.feed.length,
  async () => {
    const element = scroller.value

    if (!element) {
      return
    }

    const nearBottom = element.scrollHeight - element.scrollTop - element.clientHeight < 160

    await nextTick()

    if (nearBottom) {
      element.scrollTop = element.scrollHeight
    }
  },
)
</script>

<template>
  <div ref="scroller" class="feed">
    <template v-for="item in feed" :key="item.id">
      <!-- 真聊天 -->
      <div
        v-if="item.kind === 'chat'"
        class="line"
        :class="{ self: item.message.playerId === yourPlayerId }"
      >
        <div v-if="item.message.playerId !== yourPlayerId" class="avatar sm">
          {{ item.message.nickname.slice(0, 1) }}
        </div>

        <div class="stack">
          <div v-if="item.message.playerId !== yourPlayerId" class="meta secondary">
            {{ item.message.nickname }} · {{ clockOf(item.at) }}
          </div>
          <div class="bubble">{{ item.message.text }}</div>
        </div>
      </div>

      <!-- 系统提示：做成 IM 里那种居中灰字，不抢眼 -->
      <p v-else-if="item.kind === 'notice'" class="notice">{{ item.text }}</p>

      <div v-else-if="item.kind === 'roundStart'" class="divider">
        <span>第 {{ item.round }} 项议程 · 每人 {{ item.cards }} 条任务</span>
      </div>

      <!-- 叫牌揭示：伪装成群接龙 -->
      <div v-else-if="item.kind === 'bids'" class="line">
        <div class="avatar sm">接</div>
        <div class="stack">
          <div class="meta secondary">群接龙 · 本周承接量</div>
          <div class="bubble card-block">
            <div v-for="(bid, seat) in item.bids" :key="seat" class="poll-row">
              <span class="poll-name ellipsis">{{ nicknameOf(seat) }}</span>
              <span class="poll-value">{{ bidText(bid) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 出牌 -->
      <div
        v-else-if="item.kind === 'play'"
        class="line"
        :class="{ self: item.seat === mySeat }"
      >
        <div v-if="item.seat !== mySeat" class="avatar sm">
          {{ item.nickname.slice(0, 1) }}
        </div>

        <div class="stack">
          <div v-if="item.seat !== mySeat" class="meta secondary">
            {{ item.nickname }} · {{ clockOf(item.at) }}
          </div>
          <div class="bubble tight">
            <CardChip :card="item.card" :tigress-mode="item.tigressMode" size="sm" />
          </div>
        </div>
      </div>

      <!-- 一墩结算 -->
      <div v-else-if="item.kind === 'trick'" class="result">
        <div class="result-head">
          <strong>{{ item.winnerName }}</strong>
          <span class="secondary">接下了这批任务</span>
          <span v-if="item.bonus > 0" class="bonus">额外 +{{ item.bonus }}</span>
        </div>
        <p class="result-reason muted">{{ WIN_REASON_TEXT[item.reason] }}</p>
        <div class="result-cards">
          <CardChip
            v-for="play in item.plays"
            :key="play.seat"
            :card="play.card"
            :tigress-mode="play.tigressMode"
            size="sm"
            :dimmed="play.seat !== item.winnerSeat"
          />
        </div>
      </div>

      <!-- 一轮记分 -->
      <div v-else-if="item.kind === 'round'" class="result">
        <div class="result-head">
          <strong>第 {{ item.round }} 项议程结算</strong>
        </div>
        <table class="score-table">
          <thead>
            <tr>
              <th>成员</th>
              <th>承接</th>
              <th>实际</th>
              <th>奖励</th>
              <th>绩效</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="score in item.scores" :key="score.seat">
              <td class="ellipsis">{{ nicknameOf(score.seat) }}</td>
              <td>{{ score.bid }}</td>
              <td>{{ score.tricksWon }}</td>
              <td>{{ score.bonus > 0 ? `+${score.bonus}` : '—' }}</td>
              <td :class="score.total >= 0 ? 'plus' : 'minus'">{{ scoreText(score.total) }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 全部结束 -->
      <div v-else-if="item.kind === 'gameEnd'" class="result final">
        <div class="result-head">
          <strong>本季度考核结束</strong>
        </div>
        <p class="winner">{{ item.winnerNames.join('、') }} 绩效最高</p>
        <div class="final-scores">
          <span v-for="(score, seat) in item.totalScores" :key="seat">
            {{ nicknameOf(seat) }} {{ score }}
          </span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.feed {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 16px 20px 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
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

.bubble.tight {
  padding: 5px 7px;
}

.card-block {
  min-width: 220px;
  white-space: normal;
}

.poll-row {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 3px 0;
  font-size: 13px;
}

.poll-row + .poll-row {
  border-top: 1px solid var(--line);
}

.poll-name {
  color: var(--text-secondary);
}

.notice {
  align-self: center;
  margin: 0;
  padding: 2px 10px;
  border-radius: 10px;
  background: rgba(31, 35, 41, 0.05);
  color: var(--text-muted);
  font-size: 12px;
}

.divider {
  display: flex;
  align-items: center;
  gap: 10px;
  color: var(--text-muted);
  font-size: 12px;
}

.divider::before,
.divider::after {
  content: '';
  flex: 1;
  height: 1px;
  background: var(--line);
}

.result {
  align-self: center;
  width: 100%;
  max-width: 520px;
  padding: 10px 14px;
  border: 1px solid var(--line);
  border-radius: var(--radius-lg);
  background: var(--bg-panel);
}

.result.final {
  border-color: var(--accent);
}

.result-head {
  display: flex;
  align-items: baseline;
  gap: 8px;
  font-size: 13px;
}

.bonus {
  margin-left: auto;
  color: var(--success);
  font-size: 12px;
}

.result-reason {
  margin: 4px 0 8px;
  font-size: 12px;
}

.result-cards {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.score-table {
  width: 100%;
  margin-top: 8px;
  border-collapse: collapse;
  font-size: 12px;
}

.score-table th {
  padding: 4px 6px;
  border-bottom: 1px solid var(--line);
  color: var(--text-muted);
  font-weight: 500;
  text-align: right;
}

.score-table th:first-child,
.score-table td:first-child {
  text-align: left;
}

.score-table td {
  padding: 4px 6px;
  text-align: right;
}

.plus {
  color: var(--success);
}

.minus {
  color: var(--danger);
}

.winner {
  margin: 6px 0;
  font-size: 13px;
}

.final-scores {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  font-size: 12px;
  color: var(--text-secondary);
}
</style>
