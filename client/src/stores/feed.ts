import type {
  CardDto,
  ChatMessageDto,
  PlayedCardDto,
  PlayerRoundScoreDto,
  TigressMode,
  TrickWinReason,
} from '@/api/types'

interface FeedBase {
  id: string
  at: number
}

export type FeedItem =
  | (FeedBase & { kind: 'chat'; message: ChatMessageDto })
  | (FeedBase & { kind: 'notice'; text: string })
  | (FeedBase & { kind: 'roundStart'; round: number; cards: number })
  | (FeedBase & { kind: 'bids'; round: number; bids: number[] })
  | (FeedBase & {
      kind: 'play'
      seat: number
      nickname: string
      card: CardDto
      tigressMode: TigressMode | null
    })
  | (FeedBase & {
      kind: 'trick'
      round: number
      trick: number
      winnerSeat: number
      winnerName: string
      reason: TrickWinReason
      bonus: number
      plays: PlayedCardDto[]
    })
  | (FeedBase & { kind: 'round'; round: number; scores: PlayerRoundScoreDto[] })
  | (FeedBase & { kind: 'gameEnd'; totalScores: number[]; winnerSeats: number[]; winnerNames: string[] })

let counter = 0

export function nextFeedId(): string {
  counter += 1
  return `f${counter}`
}
