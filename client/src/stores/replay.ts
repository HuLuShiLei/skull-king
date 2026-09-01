import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { api } from '@/api/http'
import type { GameReplayDto } from '@/api/types'
import type { FeedItem } from './feed'
import { pauseAfter, toFeedItem } from './eventFeed'

const sleep = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms))

/**
 * 历史对局回放。事件由服务端重放命令日志得来，这里只负责把它变成消息流，
 * 渲染完全复用房间里那套组件，所以看起来就是在翻一段旧的聊天记录。
 */
export const useReplayStore = defineStore('replay', () => {
  const data = ref<GameReplayDto | null>(null)
  const feed = ref<FeedItem[]>([])
  const loading = ref(false)
  const error = ref('')
  const playing = ref(false)

  // 每次重播都换一个令牌，旧的循环发现令牌变了就自己退出。
  let runToken = 0

  const seats = computed(() => data.value?.seats ?? [])

  function nicknameOf(seat: number): string {
    return seats.value.find((s) => s.seat === seat)?.nickname ?? `${seat + 1} 号位`
  }

  /** 事件本身没有时间戳，按开局时间往后摊，界面上的时间才不会全都一样。 */
  function timeAt(index: number): number {
    const start = Date.parse(data.value?.startedAt ?? '')

    return (Number.isNaN(start) ? Date.now() : start) + index * 1000
  }

  function expandAll() {
    const replay = data.value

    if (!replay) {
      return
    }

    feed.value = replay.events
      .map((event, index) => toFeedItem(event, nicknameOf, timeAt(index)))
      .filter((item): item is FeedItem => item !== null)
  }

  async function load(gameId: string) {
    stop()

    loading.value = true
    error.value = ''
    data.value = null
    feed.value = []

    try {
      data.value = await api.replay(gameId)
      expandAll()
    } catch (loadError: unknown) {
      error.value = loadError instanceof Error ? loadError.message : '记录读取失败'
    } finally {
      loading.value = false
    }
  }

  function stop() {
    runToken += 1
    playing.value = false
  }

  /** 从头以原速重播一遍，中途可以停。 */
  async function play() {
    const replay = data.value

    if (!replay || playing.value) {
      return
    }

    const token = ++runToken

    playing.value = true
    feed.value = []

    try {
      for (const [index, event] of replay.events.entries()) {
        if (token !== runToken) {
          return
        }

        const item = toFeedItem(event, nicknameOf, timeAt(index))

        if (item) {
          feed.value.push(item)
        }

        const pause = pauseAfter(event)

        if (pause > 0) {
          await sleep(pause)
        }
      }
    } finally {
      if (token === runToken) {
        playing.value = false
      }
    }
  }

  function reset() {
    stop()
    data.value = null
    feed.value = []
    error.value = ''
  }

  return { data, feed, seats, loading, error, playing, nicknameOf, load, play, stop, expandAll, reset }
})
