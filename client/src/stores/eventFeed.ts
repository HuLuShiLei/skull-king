import type { GameEventDto } from '@/api/types'
import { nextFeedId, type FeedItem } from './feed'

/** 每类事件播完后的停顿，让一墩的胜负能被看清再翻页。 */
export const EVENT_PAUSE_MS: Record<string, number> = {
  cardPlayed: 260,
  trickResolved: 1500,
  roundScored: 1400,
  bidsRevealed: 700,
  gameEnded: 0,
}

export function pauseAfter(event: GameEventDto): number {
  return EVENT_PAUSE_MS[event.type] ?? 0
}

/**
 * 把一条服务端事件翻成一条消息流条目。做成纯函数是为了让实时房间和历史回放
 * 共用同一套渲染，回放只是把「事件从 Hub 来」换成「事件从接口来」。
 */
export function toFeedItem(
  event: GameEventDto,
  nicknameOf: (seat: number) => string,
  at = event.at ? Date.parse(event.at) : Date.now(),
): FeedItem | null {
  const id = nextFeedId()

  switch (event.type) {
    case 'systemNotice':
      return { kind: 'notice', id, at, text: event.text ?? '' }

    case 'gameStarted':
      return { kind: 'notice', id, at, text: `本轮排期已启动，${event.text ?? ''}` }

    case 'roundStarted':
      return {
        kind: 'roundStart',
        id,
        at,
        round: event.roundNumber ?? 0,
        cards: event.cardsPerPlayer ?? 0,
      }

    case 'bidsRevealed':
      return { kind: 'bids', id, at, round: event.roundNumber ?? 0, bids: event.bids ?? [] }

    case 'cardPlayed':
      if (!event.card || event.seat === null) {
        return null
      }

      return {
        kind: 'play',
        id,
        at,
        seat: event.seat,
        nickname: nicknameOf(event.seat),
        card: event.card,
        tigressMode: event.tigressMode,
      }

    case 'trickResolved':
      return {
        kind: 'trick',
        id,
        at,
        round: event.roundNumber ?? 0,
        trick: event.trickNumber ?? 0,
        winnerSeat: event.winnerSeat ?? 0,
        winnerName: nicknameOf(event.winnerSeat ?? 0),
        reason: event.reason ?? 'LeadSuit',
        bonus: event.bonus ?? 0,
        plays: event.plays ?? [],
      }

    case 'roundScored':
      return { kind: 'round', id, at, round: event.roundNumber ?? 0, scores: event.scores ?? [] }

    case 'gameEnded':
      return {
        kind: 'gameEnd',
        id,
        at,
        totalScores: event.totalScores ?? [],
        winnerSeats: event.winnerSeats ?? [],
        winnerNames: (event.winnerSeats ?? []).map(nicknameOf),
      }

    default:
      return null
  }
}
