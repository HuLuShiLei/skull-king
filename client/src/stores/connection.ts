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

      if (value) {
        void rejoin()
      }
    },
  })

  /**
   * 重连成功后认回原来的房间。服务端凭 token 找回座位并补发快照，
   * 失败通常意味着房间在断线期间已经被回收，这时得让用户看见，
   * 不然界面会停在一个早就不存在的房间上。
   */
  async function rejoin() {
    const room = useRoomStore()

    if (!room.code) {
      return
    }

    try {
      const result = await hub.joinRoom(room.code, room.passwordUsed)

      if (!result.ok) {
        room.removedReason = result.error ?? '这个群已经解散了'
      }
    } catch {
      // 还没连稳，等下一次 onreconnected 再试。
    }
  }

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
