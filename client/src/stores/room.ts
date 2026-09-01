import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import type {
  ChatMessageDto,
  GameEventDto,
  RoomStateDto,
  UpdateRoomSettingsRequest,
} from '@/api/types'
import { nextFeedId, type FeedItem } from './feed'
import { pauseAfter, toFeedItem } from './eventFeed'
import { useConnectionStore } from './connection'
import { useStealthStore } from './stealth'

const FEED_LIMIT = 300

type QueueItem =
  | { type: 'event'; payload: GameEventDto }
  | { type: 'state'; payload: RoomStateDto }

const sleep = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms))

export const useRoomStore = defineStore('room', () => {
  const state = ref<RoomStateDto | null>(null)
  const feed = ref<FeedItem[]>([])
  const code = ref('')
  const lastError = ref('')
  const secondsLeft = ref<number | null>(null)
  const joining = ref(false)
  const removedReason = ref('')

  /**
   * 进这个房间时用的口令。重连要重新报一次 JoinRoom，正常情况下服务端认
   * playerId 就放行，但如果掉线太久已经被清出成员名单，就会走新人那条路重新验
   * 口令——不带着的话会收到「房间密码不正确」，明明是超时被清出去了。
   */
  const passwordUsed = ref<string | undefined>(undefined)

  const queue: QueueItem[] = []
  let draining = false
  let countdownTimer: number | null = null

  /**
   * 已经画进消息流的事件序号和聊天 id。每份快照都带着最近的流水，靠这两个集合
   * 把「已经看过的」筛掉，剩下的就是断线期间漏掉的——不然重连回来这段是空白。
   */
  const seenEvents = new Set<number>()
  const seenChat = new Set<string>()

  const game = computed(() => state.value?.game ?? null)
  const members = computed(() => state.value?.members ?? [])
  const seated = computed(() => members.value.filter((m) => !m.isSpectator))
  const spectators = computed(() => members.value.filter((m) => m.isSpectator))
  const you = computed(() => members.value.find((m) => m.playerId === state.value?.yourPlayerId))
  const isHost = computed(() => state.value?.hostPlayerId === state.value?.yourPlayerId)
  const yourSeat = computed(() => state.value?.yourSeat ?? -1)

  const isYourTurn = computed(() => {
    const view = game.value
    return !!view && view.phase === 'Playing' && view.currentSeat === yourSeat.value
  })

  const needsBid = computed(() => {
    const view = game.value
    return !!view && view.phase === 'Bidding' && yourSeat.value >= 0 && !view.hasBid[yourSeat.value]
  })

  const waitingOnYou = computed(() => isYourTurn.value || needsBid.value)

  function nicknameOf(seat: number): string {
    return members.value.find((m) => m.seat === seat)?.nickname ?? `${seat + 1} 号位`
  }

  function push(item: FeedItem) {
    feed.value.push(item)

    if (feed.value.length > FEED_LIMIT) {
      feed.value.splice(0, feed.value.length - FEED_LIMIT)
    }
  }

  function applyEvent(event: GameEventDto): number {
    // 首份快照还没到：这些事件都会在快照的流水里再来一遍，现在画上去只会让
    // 顺序错乱——历史是后到的，却应该排在它们前面。
    if (!state.value) {
      return 0
    }

    seenEvents.add(event.seq)

    const item = toFeedItem(event, nicknameOf)

    if (item) {
      push(item)
    }

    return pauseAfter(event)
  }

  async function drain() {
    if (draining) {
      return
    }

    draining = true

    try {
      while (queue.length > 0) {
        const item = queue.shift()!

        if (item.type === 'state') {
          const wasWaiting = waitingOnYou.value

          state.value = item.payload
          syncCountdown()
          seedHistory(item.payload)

          // 只在「刚轮到你」的那一刻提示一次，重复推送的快照不该反复打扰。
          if (!wasWaiting && waitingOnYou.value) {
            const stealth = useStealthStore()
            stealth.notify({
              title: stealth.settings.documentTitle,
              body: needsBid.value ? '请填写承接量' : '轮到你处理',
              tag: 'sk-turn',
            })
          }

          continue
        }

        const pause = applyEvent(item.payload)

        if (pause > 0) {
          await sleep(pause)
        }
      }
    } finally {
      draining = false
    }
  }

  /**
   * 事件和快照走同一条队列。否则快照会在出牌动画播完前抢先落地，
   * 桌面上的牌会瞬间清空，看起来像丢帧。
   */
  function enqueue(item: QueueItem) {
    queue.push(item)
    void drain()
  }

  function onRoomState(next: RoomStateDto) {
    if (next.code !== code.value) {
      return
    }

    // 首次进房不排队，否则会白屏等到队列空转一轮。
    if (!state.value) {
      state.value = next
      syncCountdown()
      seedHistory(next)
      return
    }

    enqueue({ type: 'state', payload: next })
  }

  /**
   * 把服务端补发的流水里还没画过的部分铺进消息流。半途来观战、刷新页面、
   * 断线重连的人都靠这个补齐——不走播放队列，那是给实时事件做动画用的，
   * 拿来补历史就得一条条干等。
   *
   * 每份快照都会过一遍，正常打牌时全都是看过的，等于空转。
   */
  function seedHistory(next: RoomStateDto) {
    const history: FeedItem[] = []

    for (const event of next.recentEvents) {
      if (seenEvents.has(event.seq)) {
        continue
      }

      seenEvents.add(event.seq)

      const item = toFeedItem(event, nicknameOf)

      if (item) {
        history.push(item)
      }
    }

    for (const message of next.recentChat) {
      if (seenChat.has(message.id)) {
        continue
      }

      seenChat.add(message.id)
      history.push({ kind: 'chat', id: nextFeedId(), at: Date.parse(message.sentAt), message })
    }

    if (history.length === 0) {
      return
    }

    history.sort((a, b) => a.at - b.at)

    for (const item of history) {
      push(item)
    }
  }

  function onGameEvent(event: GameEventDto) {
    enqueue({ type: 'event', payload: event })
  }

  function onChat(message: ChatMessageDto) {
    if (seenChat.has(message.id)) {
      return
    }

    seenChat.add(message.id)
    push({ kind: 'chat', id: nextFeedId(), at: Date.parse(message.sentAt), message })

    if (message.playerId === state.value?.yourPlayerId) {
      return
    }

    useStealthStore().notify({
      title: message.nickname,
      body: message.text,
      tag: 'sk-chat',
    })
  }

  function syncCountdown() {
    if (countdownTimer !== null) {
      window.clearInterval(countdownTimer)
      countdownTimer = null
    }

    const remaining = game.value?.turnSecondsRemaining ?? null
    secondsLeft.value = remaining

    if (remaining === null) {
      return
    }

    countdownTimer = window.setInterval(() => {
      if (secondsLeft.value === null || secondsLeft.value <= 0) {
        return
      }

      secondsLeft.value -= 1
    }, 1000)
  }

  function reset() {
    state.value = null
    feed.value = []
    code.value = ''
    lastError.value = ''
    removedReason.value = ''
    passwordUsed.value = undefined
    queue.length = 0
    seenEvents.clear()
    seenChat.clear()
    syncCountdown()
  }

  async function run(action: () => Promise<{ ok: boolean; error: string | null }>) {
    try {
      const result = await action()

      if (!result.ok) {
        lastError.value = result.error ?? '操作失败'
        return false
      }

      lastError.value = ''
      return true
    } catch (error) {
      lastError.value = error instanceof Error ? error.message : '网络异常'
      return false
    }
  }

  async function join(roomCode: string, password?: string) {
    const connection = useConnectionStore()
    const next = roomCode.trim().toUpperCase()

    // 换群要先退掉手上这个。点左边另一个群时 RoomView 只是换了 props、不会卸载，
    // 卸载时那句 leave 根本不会跑；不主动退的话，旧群里会留下一个永远「在线」的
    // 自己，那个群既回收不掉，还可能占着座位卡住别人的对局。
    if (code.value && code.value !== next) {
      await leave()
    }

    reset()
    code.value = next
    passwordUsed.value = password
    joining.value = true

    try {
      await connection.ensureStarted()

      const ok = await run(() => connection.hub.joinRoom(code.value, password))

      if (!ok) {
        code.value = ''
      }

      return ok
    } finally {
      joining.value = false
    }
  }

  async function leave() {
    const connection = useConnectionStore()

    if (code.value) {
      await run(() => connection.hub.leaveRoom(code.value))
    }

    reset()
  }

  const hub = () => useConnectionStore().hub

  return {
    state,
    feed,
    code,
    lastError,
    secondsLeft,
    joining,
    removedReason,
    passwordUsed,

    game,
    members,
    seated,
    spectators,
    you,
    isHost,
    yourSeat,
    isYourTurn,
    needsBid,
    waitingOnYou,

    nicknameOf,
    onRoomState,
    onGameEvent,
    onChat,
    reset,
    join,
    leave,

    setReady: (ready: boolean) => run(() => hub().setReady(code.value, ready)),
    sitDown: () => run(() => hub().sitDown(code.value)),
    standUp: () => run(() => hub().standUp(code.value)),
    startGame: () => run(() => hub().startGame(code.value)),
    placeBid: (bid: number) => run(() => hub().placeBid(code.value, bid)),
    playCard: (cardId: string, tigressMode?: string) =>
      run(() => hub().playCard(code.value, cardId, tigressMode)),
    sendChat: (text: string) => run(() => hub().sendChat(code.value, text)),
    kick: (playerId: string) => run(() => hub().kick(code.value, playerId)),
    transferHost: (playerId: string) => run(() => hub().transferHost(code.value, playerId)),
    updateSettings: (request: UpdateRoomSettingsRequest) =>
      run(() => hub().updateSettings(code.value, request)),
  }
})
