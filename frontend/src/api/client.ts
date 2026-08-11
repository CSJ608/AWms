/**
 * HTTP 客户端 —— 统一 envelope 解析 / Bearer 注入 / Accept-Language /
 * Idempotency-Key / 401 单飞刷新 + 排队重放（通用规范 2.1/2.4/2.5/2.6，评审 B-31）。
 * 真实 HTTP 与 MSW mock 共用本层：mock 拦截 fetch，页面/数据层零改动。
 */
import type { ApiError } from './types'
import { sessionStore } from '../platform/auth/session-store'
import { emitSessionExpired } from '../platform/auth/session-events'
import { singleFlight } from '../platform/auth/refresh-queue'
import i18n from '../i18n'

const API_BASE = '/api'
const REQUEST_TIMEOUT_MS = 20_000

export class ApiErrorImpl extends Error implements ApiError {
  code: string
  status?: number

  constructor(code: string, message: string, status?: number) {
    super(message)
    this.name = 'ApiError'
    this.code = code
    this.status = status
  }
}

export function parseApiError(e: unknown): ApiError {
  if (e instanceof ApiErrorImpl) return { code: e.code, message: e.message, status: e.status }
  if (e && typeof e === 'object' && 'code' in e && 'message' in e) {
    const { code, message } = e as { code: string; message: string }
    return { code, message }
  }
  if (e instanceof Error) return { code: 'NETWORK_ERROR', message: e.message }
  return { code: 'UNKNOWN_ERROR', message: String(e) }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  formData?: FormData
  query?: Record<string, string | number | boolean | undefined | null>
  idempotencyKey?: string
  /** 文件下载（不解析 envelope） */
  blob?: boolean
  /** 跳过 Bearer（如刷新用过期 token 场景自行控制头） */
  skipAuth?: boolean
}

function buildUrl(path: string, query?: RequestOptions['query']): string {
  // 契约中 fileUrl/failReportUrl 等为完整路径（含 /api 前缀）
  const url = path.startsWith('/api/') ? path : `${API_BASE}${path}`
  if (!query) return url
  const params = new URLSearchParams()
  for (const [k, v] of Object.entries(query)) {
    if (v === undefined || v === null || v === '') continue // 筛选空值不发送（通用规范 2.2）
    params.set(k, String(v))
  }
  const qs = params.toString()
  return qs ? `${url}?${qs}` : url
}

async function doFetch(path: string, options: RequestOptions, withAuth: boolean): Promise<Response> {
  const headers: Record<string, string> = {
    Accept: 'application/json',
    'Accept-Language': i18n.language || 'zh',
  }
  if (withAuth) {
    const token = sessionStore.getToken()
    if (token) headers.Authorization = `Bearer ${token}`
  }
  if (options.idempotencyKey) headers['Idempotency-Key'] = options.idempotencyKey
  if (options.formData) {
    // multipart：不设 Content-Type，由浏览器带 boundary
  } else if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  const res = await fetch(buildUrl(path, options.query), {
    method: options.method ?? 'GET',
    headers,
    body: options.formData ?? (options.body !== undefined ? JSON.stringify(options.body) : undefined),
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
  })
  return res
}

async function readError(res: Response): Promise<ApiError> {
  try {
    const json = (await res.json()) as { code?: string; message?: string }
    return new ApiErrorImpl(json.code ?? 'UNKNOWN_ERROR', json.message ?? res.statusText, res.status)
  } catch {
    return new ApiErrorImpl('NETWORK_ERROR', `HTTP ${res.status}`, res.status)
  }
}

/** 单飞刷新：并发 401 共享同一次 refresh，成功后各自重放原请求 */
async function refreshToken(): Promise<string | null> {
  return singleFlight(async () => {
    const expiredToken = sessionStore.getToken()
    if (!expiredToken) return null
    try {
      const res = await doFetch('/auth/refresh', { method: 'POST', skipAuth: false }, true)
      if (res.status === 401) {
        sessionStore.clear()
        emitSessionExpired()
        return null
      }
      const json = (await res.json()) as { code: string; message: string; data: { token: string; expiresAt: string } | null }
      if (!res.ok || !json.data?.token) {
        sessionStore.clear()
        emitSessionExpired()
        return null
      }
      sessionStore.setToken(json.data.token, json.data.expiresAt)
      return json.data.token
    } catch {
      sessionStore.clear()
      emitSessionExpired()
      return null
    }
  })
}

export async function request<T>(path: string, options: RequestOptions = {}, attempt = 0): Promise<T> {
  const withAuth = !options.skipAuth
  let res: Response
  try {
    res = await doFetch(path, options, withAuth)
  } catch (e) {
    throw new ApiErrorImpl('NETWORK_ERROR', e instanceof Error ? e.message : String(e))
  }

  // 401 → 单飞刷新 + 重放一次（刷新端点自身 401 直接抛）
  if (res.status === 401 && withAuth && !path.startsWith('/auth/refresh')) {
    const newToken = await refreshToken()
    if (newToken) {
      if (attempt === 0) return request<T>(path, options, 1)
    }
    throw new ApiErrorImpl('UNAUTHORIZED', '未登录或会话已过期', 401)
  }

  if (options.blob) {
    if (!res.ok) throw await readError(res)
    return res as T
  }

  let json: { code?: string; message?: string; data?: T | null } | null = null
  try {
    json = (await res.json()) as { code?: string; message?: string; data?: T | null } | null
  } catch {
    if (res.status === 204) return undefined as T
    throw new ApiErrorImpl('NETWORK_ERROR', `HTTP ${res.status}`, res.status)
  }

  if (!res.ok || (json && json.code && json.code !== 'OK')) {
    throw new ApiErrorImpl(json?.code ?? 'UNKNOWN_ERROR', json?.message ?? res.statusText, res.status)
  }
  return json?.data as T
}

export { API_BASE }
