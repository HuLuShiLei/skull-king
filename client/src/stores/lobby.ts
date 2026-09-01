import { defineStore } from 'pinia'
import { ref } from 'vue'

import { api } from '@/api/http'
import type { CreateRoomRequest, GameHistoryEntry, RoomSummaryDto } from '@/api/types'

export const useLobbyStore = defineStore('lobby', () => {
  const rooms = ref<RoomSummaryDto[]>([])
  const history = ref<GameHistoryEntry[]>([])
  const loading = ref(false)
  const error = ref('')

  async function refresh() {
    loading.value = true

    try {
      rooms.value = await api.listRooms()
      error.value = ''
    } catch (e) {
      error.value = e instanceof Error ? e.message : '拉取列表失败'
    } finally {
      loading.value = false
    }
  }

  async function loadHistory() {
    try {
      history.value = await api.history()
    } catch {
      history.value = []
    }
  }

  async function create(request: CreateRoomRequest) {
    const { code } = await api.createRoom(request)
    return code
  }

  return { rooms, history, loading, error, refresh, loadHistory, create }
})
