/**
 * 端到端连通性检查：三个匿名玩家真的连上 Hub，打完一整局，再把回放拉回来对一遍。
 * 覆盖的是单测覆盖不到的那一层——鉴权、Hub 方法签名、序列化、REST 与 Hub 的衔接。
 *
 *   node scripts/smoke.mjs [http://localhost:5080]
 *
 * 带 --half 时打到一半就断开退出，用来验证「停服重启后能不能恢复这局」：
 * 跑完之后重启服务端，日志里应该出现「已恢复 1 个房间」。
 */
import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
} from '@microsoft/signalr'

const args = process.argv.slice(2)
const half = args.includes('--half')
const base = args.find((a) => a.startsWith('http')) ?? 'http://localhost:5080'

const log = (...args) => console.log(...args)

async function post(path, body, token) {
  const response = await fetch(`${base}/api${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(body),
  })

  if (!response.ok) {
    throw new Error(`POST ${path} -> ${response.status} ${await response.text()}`)
  }

  return response.json()
}

async function get(path, token) {
  const response = await fetch(`${base}/api${path}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })

  if (!response.ok) {
    throw new Error(`GET ${path} -> ${response.status} ${await response.text()}`)
  }

  return response.json()
}

class Player {
  constructor(auth) {
    this.auth = auth
    this.state = null
    this.events = []
  }

  async connect() {
    this.hub = new HubConnectionBuilder()
      .withUrl(`${base}/hub/game`, {
        accessTokenFactory: () => this.auth.token,
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
      })
      .configureLogging(LogLevel.Error)
      .build()

    // 回调必须返回 undefined，SignalR 会把返回值当成「客户端结果」再回传给服务端。
    this.hub.on('RoomState', (state) => {
      this.state = state
    })
    this.hub.on('GameEvent', (event) => {
      this.events.push(event)
    })
    this.hub.on('Chat', () => {})
    this.hub.on('Removed', () => {})
    this.hub.on('LobbyChanged', () => {})

    await this.hub.start()
  }

  async call(method, ...args) {
    const result = await this.hub.invoke(method, ...args)

    if (result && result.ok === false) {
      throw new Error(`${method} 被拒绝：${result.error}`)
    }

    return result
  }
}

/** 快照是 invoke 返回之后才推过来的，断言前得先等它落地。 */
async function waitFor(predicate, message, timeoutMs = 5000) {
  const deadline = Date.now() + timeoutMs

  while (Date.now() < deadline) {
    if (predicate()) {
      return
    }

    await new Promise((resolve) => setTimeout(resolve, 20))
  }

  throw new Error(`等待超时：${message}`)
}

function check(condition, message) {
  if (!condition) {
    throw new Error(`断言失败：${message}`)
  }

  log(`  ok  ${message}`)
}

async function main() {
  log(`目标 ${base}`)

  const players = []

  for (const nickname of ['阿甲', '阿乙', '阿丙']) {
    const auth = await post('/auth/anonymous', { nickname })
    const player = new Player(auth)

    await player.connect()
    players.push(player)
  }

  check(players.length === 3, '三个匿名玩家都登录并连上了 Hub')

  const { code } = await post(
    '/rooms',
    { name: '联调用群', isPublic: true, maxPlayers: 6, maxRounds: 2, turnSeconds: 0 },
    players[0].auth.token,
  )

  log(`房间 ${code}`)

  for (const player of players) {
    await player.call('JoinRoom', code, null)
  }

  await waitFor(
    () => players.every((p) => p.state && p.state.members.length === 3),
    '三名成员的快照',
  )
  check(true, '每个人都收到了包含三名成员的快照')

  const others = players.slice(1)

  for (const player of others) {
    await player.call('SetReady', code, true)
  }

  await players[0].call('StartGame', code)

  await waitFor(() => players.every((p) => p.state.game), '开局快照')
  check(true, '开局后所有人都拿到了对局视图')

  const hands = players.map((p) => p.state.game.myHand.map((c) => c.id).join(','))
  check(new Set(hands).size === 3, '三个人的手牌各不相同（手牌是单播的）')

  const outsiderSeesHand = players.some((p) =>
    p.state.members.some((m) => m.playerId !== p.state.yourPlayerId && m.hand),
  )
  check(!outsiderSeesHand, '快照里没有夹带别人的手牌')

  if (half) {
    for (const player of players) {
      const seat = player.state.yourSeat

      if (!player.state.game.hasBid[seat]) {
        await player.call('PlaceBid', code, 0)
      }
    }

    await waitFor(() => players[0].state.game.phase === 'Playing', '进入出牌阶段')

    for (const player of players) {
      await player.hub.stop()
    }

    log(`\n房间 ${code} 停在出牌阶段，现在重启服务端应该能看到「已恢复 1 个房间」`)
    return
  }

  let guard = 0

  while (players[0].state.status === 1 && guard++ < 4000) {
    let acted = false

    for (const player of players) {
      const game = player.state.game

      if (!game) {
        continue
      }

      const seat = player.state.yourSeat

      if (game.phase === 'Bidding' && seat >= 0 && !game.hasBid[seat]) {
        await player.call('PlaceBid', code, 0)
        acted = true
        break
      }

      if (game.phase === 'Playing' && game.currentSeat === seat && game.playableCardIds.length > 0) {
        await player.call('PlayCard', code, game.playableCardIds[0], null)
        acted = true
        break
      }
    }

    if (!acted) {
      await new Promise((resolve) => setTimeout(resolve, 20))
    }
  }

  check(players[0].state.status === 2, `一整局打完了（${guard} 步）`)

  await waitFor(
    () => players[0].events.some((e) => e.type === 'gameEnded'),
    '结束事件',
  )

  const ended = players[0].events.filter((e) => e.type === 'gameEnded')
  check(ended.length === 1, '收到了一条对局结束事件')
  check(ended[0].winnerSeats.length > 0, '结束事件里带了名次')

  // 广播出去的叫牌事件不能带数字，否则揭示前就泄露了。
  const leaked = players[0].events.filter((e) => e.type === 'bidPlaced' && e.bids !== null)
  check(leaked.length === 0, '广播的叫牌事件没有泄露数字')

  const history = await get('/history?limit=5', players[0].auth.token)
  check(history.length === 1, '历史里出现了这一局')

  const replay = await get(`/games/${history[0].gameId}/replay`, players[0].auth.token)

  const liveTypes = players[0].events.filter((e) => e.type !== 'systemNotice').map((e) => e.type)
  const replayTypes = replay.events.map((e) => e.type)

  check(
    JSON.stringify(liveTypes) === JSON.stringify(replayTypes),
    `回放事件序列与实时一致（${replayTypes.length} 条）`,
  )
  check(replay.seats.length === 3, '回放带了三个座位的名单')

  for (const player of players) {
    await player.hub.stop()
  }

  log('\n全部通过')
}

main().catch((error) => {
  console.error(`\n失败：${error.message}`)
  process.exit(1)
})
