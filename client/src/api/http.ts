import type {
  AuthResponse,
  CreateRoomRequest,
  GameHistoryEntry,
  GameReplayDto,
  RoomProbeDto,
  RoomSummaryDto,
} from './types'

const TOKEN_KEY = 'sk.token'

export function readToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function writeToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY)
}

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = readToken()

  const response = await fetch(`/api${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    throw new ApiError(await response.text().catch(() => response.statusText), response.status)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const api = {
  loginAnonymous: (nickname: string) =>
    request<AuthResponse>('/auth/anonymous', {
      method: 'POST',
      body: JSON.stringify({ nickname }),
    }),

  me: () => request<AuthResponse>('/auth/me'),

  rename: (nickname: string) =>
    request<AuthResponse>('/auth/rename', {
      method: 'POST',
      body: JSON.stringify({ nickname }),
    }),

  listRooms: () => request<RoomSummaryDto[]>('/rooms'),

  createRoom: (payload: CreateRoomRequest) =>
    request<{ code: string }>('/rooms', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  probeRoom: (code: string) => request<RoomProbeDto>(`/rooms/${encodeURIComponent(code)}/probe`),

  history: (limit = 20) => request<GameHistoryEntry[]>(`/history?limit=${limit}`),

  replay: (gameId: string) =>
    request<GameReplayDto>(`/games/${encodeURIComponent(gameId)}/replay`),
}
