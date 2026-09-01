import { defineStore } from 'pinia'
import { ref } from 'vue'

import { GameHubClient } from '@/api/hub'
import { useLobbyStore } from './lobby'
import { useRoomStore } from './room'

/**
 * 唯一的 Hub 连接。所有下行消息都在这里分发给对应的 store，
 * 这样各 store 之间不需要互相知道消息格式。
 */
export const useConnectionStore = defineStore('connection', () => {
  const connected = ref(false)

  // 用普通变量而不是 ref：这是一次性的连接闭包，不需要参与响应式。
  let starting: Promise<void> | null = null

  const hub = new GameHubClient({
    onRoomState: (state) => useRoomStore().onRoomState(state),
    onGameEvent: (event) => useRoomStore().onGameEvent(event),
    onChat: (message) => useRoomStore().onChat(message),
    onRemoved: (code, reason) => {
      const room = useRoomStore()

      if (room.code === code) {
        room.reset()
        room.removedReason = reason
      }
    },
    onLobbyChanged: () => void useLobbyStore().refresh(),
    onConnectionChanged: (value) => {
      connected.value = value

      // 重连成功后重新入房，服务端会凭 token 认回原座位并补发快照。
      if (value) {
        const room = useRoomStore()

        if (room.code) {
          void hub.joinRoom(room.code)
        }
      }
    },
  })

  function ensureStarted(): Promise<void> {
    starting ??= hub.start().catch((error: unknown) => {
      starting = null
      throw error
    })

    return starting
  }

  async function stop() {
    await hub.stop()
    starting = null
    connected.value = false
  }

  return { hub, connected, ensureStarted, stop }
})
