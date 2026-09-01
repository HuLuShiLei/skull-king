import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'

import { apiBase } from './config'
import { readToken } from './http'
import type {
  ChatMessageDto,
  GameEventDto,
  RoomActionResult,
  RoomStateDto,
  UpdateRoomSettingsRequest,
} from './types'

export interface HubHandlers {
  onRoomState: (state: RoomStateDto) => void
  onGameEvent: (event: GameEventDto) => void
  onChat: (message: ChatMessageDto) => void
  onRemoved: (code: string, reason: string) => void
  onLobbyChanged: () => void
  onConnectionChanged: (connected: boolean) => void
}

/**
 * SignalR 连接的薄封装。断线自动重连，重连后由调用方负责重新入房，
 * 服务端会凭 token 认回原座位并补发完整快照。
 */
export class GameHubClient {
  private connection: HubConnection | null = null

  private readonly handlers: HubHandlers

  constructor(handlers: HubHandlers) {
    this.handlers = handlers
  }

  get connected(): boolean {
    return this.connection?.state === HubConnectionState.Connected
  }

  async start(): Promise<void> {
    if (this.connection) {
      return
    }

    // token 走 query 而不是 Authorization 头：WebSocket 握手不支持自定义头。
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBase}/hub/game?access_token=${encodeURIComponent(readToken() ?? '')}`)
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('RoomState', this.handlers.onRoomState)
    connection.on('GameEvent', this.handlers.onGameEvent)
    connection.on('Chat', this.handlers.onChat)
    connection.on('Removed', this.handlers.onRemoved)
    connection.on('LobbyChanged', this.handlers.onLobbyChanged)

    connection.onreconnected(() => this.handlers.onConnectionChanged(true))
    connection.onreconnecting(() => this.handlers.onConnectionChanged(false))
    connection.onclose(() => this.handlers.onConnectionChanged(false))

    this.connection = connection

    await connection.start()
    this.handlers.onConnectionChanged(true)
  }

  async stop(): Promise<void> {
    await this.connection?.stop()
    this.connection = null
  }

  private invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      return Promise.reject(new Error('连接尚未就绪'))
    }

    return this.connection.invoke<T>(method, ...args)
  }

  subscribeLobby = () => this.invoke<void>('SubscribeLobby')

  unsubscribeLobby = () => this.invoke<void>('UnsubscribeLobby')

  joinRoom = (code: string, password?: string) =>
    this.invoke<RoomActionResult>('JoinRoom', code, password ?? null)

  leaveRoom = (code: string) => this.invoke<RoomActionResult>('LeaveRoom', code)

  setReady = (code: string, ready: boolean) => this.invoke<RoomActionResult>('SetReady', code, ready)

  sitDown = (code: string) => this.invoke<RoomActionResult>('SitDown', code)

  standUp = (code: string) => this.invoke<RoomActionResult>('StandUp', code)

  updateSettings = (code: string, request: UpdateRoomSettingsRequest) =>
    this.invoke<RoomActionResult>('UpdateSettings', code, request)

  kick = (code: string, targetPlayerId: string) =>
    this.invoke<RoomActionResult>('Kick', code, targetPlayerId)

  transferHost = (code: string, targetPlayerId: string) =>
    this.invoke<RoomActionResult>('TransferHost', code, targetPlayerId)

  startGame = (code: string) => this.invoke<RoomActionResult>('StartGame', code)

  placeBid = (code: string, bid: number) => this.invoke<RoomActionResult>('PlaceBid', code, bid)

  playCard = (code: string, cardId: string, tigressMode?: string) =>
    this.invoke<RoomActionResult>('PlayCard', code, cardId, tigressMode ?? null)

  sendChat = (code: string, text: string) => this.invoke<RoomActionResult>('SendChat', code, text)

  requestState = (code: string) => this.invoke<void>('RequestState', code)
}
