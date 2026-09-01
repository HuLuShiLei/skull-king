<script setup lang="ts">
import { computed, ref } from 'vue'

import RoomSettingsDialog from './RoomSettingsDialog.vue'
import type { RoomMemberDto } from '@/api/types'
import { useRoomStore } from '@/stores/room'

const room = useRoomStore()
const menuFor = ref('')
const settingsOpen = ref(false)

const game = computed(() => room.game)

function bidOf(member: RoomMemberDto): string {
  const view = game.value

  if (!view || member.isSpectator) {
    return ''
  }

  const won = view.tricksWon[member.seat] ?? 0

  if (!view.bidsRevealed) {
    return view.hasBid[member.seat] ? '已接龙' : '待接龙'
  }

  return `${won}/${view.bids[member.seat] ?? 0}`
}

function toggleMenu(playerId: string) {
  menuFor.value = menuFor.value === playerId ? '' : playerId
}

async function kick(playerId: string) {
  menuFor.value = ''
  await room.kick(playerId)
}

async function transfer(playerId: string) {
  menuFor.value = ''
  await room.transferHost(playerId)
}
</script>

<template>
  <aside class="roster">
    <header>
      <span>群成员</span>

      <span class="head-right">
        <span class="muted">{{ room.seated.length }}/{{ room.state?.settings.maxPlayers ?? 0 }}</span>
        <button v-if="room.isHost" class="btn btn-text btn-sm" @click="settingsOpen = true">
          设置
        </button>
      </span>
    </header>

    <RoomSettingsDialog v-if="settingsOpen" @close="settingsOpen = false" />

    <div class="scroll">
      <div
        v-for="member in room.seated"
        :key="member.playerId"
        class="member"
        :class="{ acting: game?.currentSeat === member.seat && game?.phase === 'Playing' }"
      >
        <div class="avatar sm" :class="{ off: !member.isConnected }">
          {{ member.nickname.slice(0, 1) }}
        </div>

        <div class="info">
          <div class="name-row">
            <span class="name ellipsis">{{ member.nickname }}</span>
            <span v-if="member.isHost" class="badge">群主</span>
            <span v-else-if="!member.isConnected" class="badge off">离线</span>
            <span v-else-if="member.isReady && room.state?.status === 0" class="badge ok">已确认</span>
          </div>
          <div v-if="bidOf(member)" class="sub muted">{{ bidOf(member) }}</div>
        </div>

        <span class="score">{{ member.totalScore }}</span>

        <!-- 房主视角下每一行都占住这个槽位，自己那行虽然没有菜单，
             分数列也要跟别人对齐。 -->
        <span v-if="room.isHost" class="more-slot">
          <button
            v-if="member.playerId !== room.state?.yourPlayerId"
            class="btn btn-text more"
            @click="toggleMenu(member.playerId)"
          >
            ⋯
          </button>
        </span>

        <div v-if="menuFor === member.playerId" class="menu">
          <button @click="transfer(member.playerId)">设为群主</button>
          <button class="danger" @click="kick(member.playerId)">移出群聊</button>
        </div>
      </div>

      <template v-if="room.spectators.length > 0">
        <div class="group-title muted">旁观 {{ room.spectators.length }}</div>

        <div v-for="member in room.spectators" :key="member.playerId" class="member">
          <div class="avatar sm" :class="{ off: !member.isConnected }">
            {{ member.nickname.slice(0, 1) }}
          </div>
          <div class="info">
            <span class="name ellipsis">{{ member.nickname }}</span>
          </div>
        </div>
      </template>
    </div>
  </aside>
</template>

<style scoped>
.roster {
  display: flex;
  flex-direction: column;
  width: var(--roster-width);
  flex: 0 0 auto;
  min-height: 0;
  background: var(--bg-panel);
  border-left: 1px solid var(--line);
}

header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 11px 14px;
  border-bottom: 1px solid var(--line);
  font-size: 13px;
  color: var(--text-secondary);
}

.head-right {
  display: flex;
  align-items: center;
  gap: 6px;
}

.scroll {
  flex: 1;
  overflow-y: auto;
  padding: 6px 0;
}

.member {
  position: relative;
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 7px 14px;
}

.member.acting {
  background: var(--accent-soft);
}

.avatar.sm {
  width: 30px;
  height: 30px;
  font-size: 12px;
}

.avatar.off {
  background: var(--bg-hover);
  color: var(--text-muted);
}

.info {
  flex: 1;
  min-width: 0;
}

.name-row {
  display: flex;
  align-items: center;
  gap: 5px;
}

.name {
  font-size: 13px;
}

.badge {
  padding: 0 4px;
  border-radius: 3px;
  background: var(--bg-hover);
  color: var(--text-muted);
  font-size: 10px;
  white-space: nowrap;
}

.badge.ok {
  background: #e6f4ea;
  color: var(--success);
}

.badge.off {
  background: #fdeceb;
  color: var(--danger);
}

.sub {
  font-size: 11px;
}

.score {
  min-width: 28px;
  text-align: right;
  font-size: 12px;
  color: var(--text-secondary);
  font-variant-numeric: tabular-nums;
}

.more-slot {
  flex: none;
  width: 16px;
  text-align: center;
}

.more {
  padding: 0 4px;
  line-height: 1;
}

.menu {
  position: absolute;
  right: 12px;
  top: 34px;
  z-index: 10;
  display: flex;
  flex-direction: column;
  min-width: 108px;
  padding: 4px;
  border: 1px solid var(--line);
  border-radius: var(--radius);
  background: var(--bg-panel);
  box-shadow: var(--shadow-lg);
}

.menu button {
  padding: 6px 10px;
  border: none;
  border-radius: 4px;
  background: transparent;
  text-align: left;
  font-size: 13px;
}

.menu button:hover {
  background: var(--bg-hover);
}

.menu .danger {
  color: var(--danger);
}

.group-title {
  padding: 10px 14px 4px;
  font-size: 11px;
}
</style>
