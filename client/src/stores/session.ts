import { defineStore } from 'pinia'
import { ref } from 'vue'

import { api, ApiError, clearToken, readToken, writeToken } from '@/api/http'

const NICKNAME_POOL = ['产品小李', '前端老王', '测试小张', '后端阿强', '设计师小美', '实习生小陈']

const NAME_KEY = 'sk.nickname'

export const useSessionStore = defineStore('session', () => {
  const playerId = ref('')
  const nickname = ref('')
  const ready = ref(false)

  /** 第一次来、还没有身份。界面要先把人拦在填名字那一步。 */
  const needsProfile = ref(false)

  function apply(auth: { playerId: string; nickname: string; token: string }) {
    playerId.value = auth.playerId
    nickname.value = auth.nickname
    writeToken(auth.token)

    // 名字单独留一份。凭证过期之后靠它把人静默接回来，别让填过一次的人
    // 再对着「完善资料」填一遍。
    localStorage.setItem(NAME_KEY, auth.nickname)

    ready.value = true
  }

  function suggest(): string {
    return NICKNAME_POOL[Math.floor(Math.random() * NICKNAME_POOL.length)]
  }

  /** 填完名字才真的建身份，省掉「先随机再改名」两次请求。 */
  async function register(name: string) {
    apply(await api.loginAnonymous(name.trim() || suggest()))
    needsProfile.value = false
  }

  /** 有旧凭证就沿用，认不回来就当新人。凭证同时决定能不能回到原座位。 */
  async function ensure() {
    if (readToken()) {
      try {
        apply(await api.me())
        return
      } catch (cause) {
        // 只有服务端明确说这张凭证不认了才丢掉它。断网或者服务端 500 时也清的话，
        // 用户等于被踢出原座位——凭证是唯一能认回去的东西。抛出去让界面提示重试。
        if (!(cause instanceof ApiError) || cause.status >= 500) {
          throw cause
        }

        clearToken()
      }
    }

    const remembered = localStorage.getItem(NAME_KEY)?.trim()

    if (remembered) {
      await register(remembered)
      return
    }

    // 真正的新人才拦。以前是随机分一个名字直接放进来，结果没人想得起去设置里改，
    // 一桌人全是词表里那六个名字，报承接量时根本认不出谁是谁。
    needsProfile.value = true
  }

  async function rename(next: string) {
    apply(await api.rename(next))
  }

  return { playerId, nickname, ready, needsProfile, suggest, ensure, register, rename }
})
