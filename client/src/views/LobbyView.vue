<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'

import { useLobbyStore } from '@/stores/lobby'
import { useStealthStore } from '@/stores/stealth'

const lobby = useLobbyStore()
const stealth = useStealthStore()
const router = useRouter()

onMounted(() => {
  void lobby.loadHistory()
})
</script>

<template>
  <div class="empty">
    <div class="card">
      <h2>选择左侧的一个群，或新建一个</h2>
      <p class="secondary">
        建群后把 6 位群号或邀请链接发给同事即可，对方打开链接直接进来。
      </p>
      <p class="secondary tip">
        随时按 <kbd>{{ stealth.bossKeyLabel }}</kbd> 可在纯对话视图之间切换。
      </p>
      <p class="secondary tip">
        第一次来、或者看不懂界面上那些黑话，点左下角的「帮助」。
      </p>
    </div>

    <section v-if="lobby.history.length > 0" class="history">
      <h3>最近记录</h3>
      <ul>
        <li v-for="item in lobby.history" :key="item.gameId">
          <button class="row" @click="router.push({ name: 'replay', params: { gameId: item.gameId } })">
            <span class="ellipsis">{{ item.roomName || item.roomCode }}</span>
            <span class="secondary">{{ item.nicknames.length }} 人</span>
            <span :class="item.youWon ? 'win' : 'secondary'">
              {{ item.yourScore }} 分{{ item.youWon ? ' · 第一' : '' }}
            </span>
          </button>
        </li>
      </ul>
      <p class="secondary tip">点任意一条可以翻回当时的完整聊天记录。</p>
    </section>
  </div>
</template>

<style scoped>
.empty {
  display: flex;
  flex-direction: column;
  gap: 20px;
  align-items: center;
  justify-content: center;
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 40px;
}

.card {
  max-width: 420px;
  text-align: center;
}

h2 {
  margin: 0 0 8px;
  font-size: 15px;
  font-weight: 600;
  color: var(--text-secondary);
}

.card p {
  margin: 4px 0;
  font-size: 13px;
}

.tip {
  font-size: 12px;
}

kbd {
  padding: 1px 5px;
  border: 1px solid var(--line-strong);
  border-radius: 3px;
  background: var(--bg-panel);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 11px;
}

.history {
  width: 100%;
  max-width: 420px;
}

h3 {
  margin: 0 0 8px;
  font-size: 12px;
  font-weight: 500;
  color: var(--text-muted);
}

ul {
  margin: 0;
  padding: 0;
  list-style: none;
  border: 1px solid var(--line);
  border-radius: var(--radius);
  background: var(--bg-panel);
}

li + li {
  border-top: 1px solid var(--line);
}

.row {
  display: flex;
  gap: 12px;
  width: 100%;
  padding: 7px 12px;
  border: 0;
  background: none;
  font: inherit;
  font-size: 13px;
  color: inherit;
  text-align: left;
  cursor: pointer;
}

.row:hover {
  background: var(--bg-hover);
}

.row > span:first-child {
  flex: 1;
}

.history .tip {
  margin: 6px 0 0;
  text-align: center;
}

.win {
  color: var(--success);
}
</style>
