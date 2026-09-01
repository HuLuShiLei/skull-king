// 与 src/SkullKing.Contracts 一一对应。改动服务端契约时这里要同步。
// SignalR 的 Hub 消息不在 OpenAPI 里，所以整份契约统一手写，避免一半生成一半手写。

export type RoomStatus = 0 | 1 | 2

export const RoomStatusValues = {
  Waiting: 0,
  Playing: 1,
  Finished: 2,
} as const

export interface AuthResponse {
  playerId: string
  nickname: string
  token: string
}

export interface RoomSettingsDto {
  name: string
  isPublic: boolean
  maxPlayers: number
  maxRounds: number
  turnSeconds: number
  hasPassword: boolean
}

export interface CreateRoomRequest {
  name?: string
  isPublic: boolean
  maxPlayers: number
  maxRounds: number
  turnSeconds: number
  password?: string
}

export interface UpdateRoomSettingsRequest {
  name?: string
  isPublic?: boolean
  maxPlayers?: number
  maxRounds?: number
  turnSeconds?: number
}

export interface RoomSummaryDto {
  code: string
  name: string
  hostNickname: string
  playerCount: number
  maxPlayers: number
  status: RoomStatus
  hasPassword: boolean
  createdAt: string
}

export interface RoomProbeDto {
  code: string
  name: string
  exists: boolean
  hasPassword: boolean
  isFull: boolean
  status: RoomStatus
}

export interface RoomMemberDto {
  playerId: string
  nickname: string
  seat: number
  isHost: boolean
  isReady: boolean
  isConnected: boolean
  isSpectator: boolean
  totalScore: number
}

export type CardKind =
  | 'Number'
  | 'Escape'
  | 'Pirate'
  | 'Tigress'
  | 'Mermaid'
  | 'SkullKing'
  | 'Loot'
  | 'Kraken'
  | 'WhiteWhale'

export type CardSuit = 'Parrot' | 'TreasureChest' | 'TreasureMap' | 'JollyRoger'

export type TigressMode = 'AsPirate' | 'AsEscape'

export interface CardDto {
  id: string
  kind: CardKind
  suit: CardSuit | null
  rank: number | null
}

export interface PlayedCardDto {
  seat: number
  card: CardDto
  tigressMode: TigressMode | null
}

export interface PlayerRoundScoreDto {
  seat: number
  bid: number
  tricksWon: number
  baseScore: number
  bonus: number
  total: number
}

export interface RoundRecordDto {
  roundNumber: number
  scores: PlayerRoundScoreDto[]
}

export type GamePhase = 'Bidding' | 'Playing' | 'Finished'

export interface GameViewDto {
  phase: GamePhase
  roundNumber: number
  totalRounds: number
  trickNumber: number
  cardsPerPlayer: number
  myHand: CardDto[]
  playableCardIds: string[]
  currentTrick: PlayedCardDto[]
  leaderSeat: number
  currentSeat: number
  bidsRevealed: boolean
  bids: (number | null)[]
  hasBid: boolean[]
  tricksWon: number[]
  totalScores: number[]
  rounds: RoundRecordDto[]
  lastTrick: PlayedCardDto[]
  lastTrickWinnerSeat: number | null
  turnSecondsRemaining: number | null
}

export interface ChatMessageDto {
  id: string
  playerId: string
  nickname: string
  seat: number
  text: string
  sentAt: string
}

export interface RoomStateDto {
  code: string
  settings: RoomSettingsDto
  status: RoomStatus
  hostPlayerId: string
  yourPlayerId: string
  yourSeat: number
  members: RoomMemberDto[]
  game: GameViewDto | null
  recentChat: ChatMessageDto[]
}

export type GameEventType =
  | 'gameStarted'
  | 'roundStarted'
  | 'bidPlaced'
  | 'bidsRevealed'
  | 'trickStarted'
  | 'cardPlayed'
  | 'trickResolved'
  | 'roundScored'
  | 'gameEnded'
  | 'systemNotice'

export type TrickWinReason =
  | 'MermaidCapturesSkullKing'
  | 'SkullKing'
  | 'Pirate'
  | 'Mermaid'
  | 'Trump'
  | 'LeadSuit'
  | 'AllEscaped'

export interface GameEventDto {
  type: GameEventType
  seq: number
  seat: number | null
  card: CardDto | null
  tigressMode: TigressMode | null
  roundNumber: number | null
  trickNumber: number | null
  cardsPerPlayer: number | null
  winnerSeat: number | null
  reason: TrickWinReason | null
  bonus: number | null
  plays: PlayedCardDto[] | null
  bids: number[] | null
  scores: PlayerRoundScoreDto[] | null
  totalScores: number[] | null
  winnerSeats: number[] | null
  text: string | null
}

export interface RoomActionResult {
  ok: boolean
  error: string | null
}

export interface GameHistoryEntry {
  gameId: string
  roomCode: string
  roomName: string
  startedAt: string
  endedAt: string | null
  yourSeat: number
  yourScore: number
  youWon: boolean
  nicknames: string[]
  finalScores: number[]
}

export interface ReplaySeatDto {
  seat: number
  nickname: string
}

export interface GameReplayDto {
  gameId: string
  roomCode: string
  roomName: string
  playerCount: number
  totalRounds: number
  startedAt: string
  endedAt: string | null
  seats: ReplaySeatDto[]
  events: GameEventDto[]
}
