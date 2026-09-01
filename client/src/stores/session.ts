import { defineStore } from 'pinia'
import { ref } from 'vue'

import { api, clearToken, readToken, writeToken } from '@/api/http'

const NICKNAME_POOL = ['产品小李', '前端老王', '测试小张', '后端阿强', '设计师小美', '实习生小陈']

export const useSessionStore = defineStore('session', () => {
  const playerId = ref('')
  const nickname = ref('')
  const ready = ref(false)

  function apply(auth: { playerId: string; nickname: string; token: string }) {
    playerId.value = auth.playerId
    nickname.value = auth.nickname
    writeToken(auth.token)
    ready.value = true
  }

  /** 有旧凭证就沿用，认不回来再发一个新身份。凭证同时决定能不能回到原座位。 */
  async function ensure(preferredNickname?: string) {
    if (readToken()) {
      try {
        apply(await api.me())
        return
      } catch {
        clearToken()
      }
    }

    const name =
      preferredNickname?.trim() ||
      NICKNAME_POOL[Math.floor(Math.random() * NICKNAME_POOL.length)]

    apply(await api.loginAnonymous(name))
  }

  async function rename(next: string) {
    apply(await api.rename(next))
  }

  return { playerId, nickname, ready, ensure, rename }
})
