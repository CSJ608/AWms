/**
 * Vitest 全局 setup —— MSW node server（同一套 handlers）、jsdom 桩、Radix 滚动锁清理。
 */
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest'
import { createDb, db } from '../mocks/db'
import { resetImportTasks, resetMockState } from '../mocks/handlers'
import { resetInboundMockState } from '../mocks/inbound-handlers'
import { server } from '../mocks/server'
import '../i18n'

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' })
  // MSW 接管 fetch 后再包一层：FormData body 手动序列化为 multipart（见上方说明）
  const mswFetch = globalThis.fetch.bind(globalThis)
  vi.stubGlobal('fetch', async (input: RequestInfo | URL, init?: RequestInit) => {
    const fd = init?.body
    if (fd instanceof UndiciFormData) {
      // DOM/undici/node:buffer 三套类型世界，边界处显式转换
      const { body, contentType } = await serializeMultipart(fd as unknown as FormData)
      const headers = new Headers(init?.headers)
      headers.set('Content-Type', contentType)
      const next: RequestInit = { ...init, body: body as unknown as BodyInit, headers }
      return mswFetch(input as RequestInfo, next)
    }
    return mswFetch(input as RequestInfo, init)
  })
})

beforeEach(() => {
  // mock 数据层重置（每用例独立种子数据）
  Object.assign(db, createDb())
})

afterEach(() => {
  server.resetHandlers()
  resetMockState()
  resetImportTasks()
  resetInboundMockState()
  // Node 26 实验性全局 localStorage 遮蔽 jsdom 版 → 一律 window.localStorage（评审 F-R1）
  window.localStorage?.clear()
  cleanup()
  // Radix body 滚动锁清理（frontend-standards：测试间互相污染）
  document.body.style.pointerEvents = ''
  delete document.body.dataset['scroll-locked']
})

afterAll(() => {
  server.close()
})

// ── jsdom 桩 ──────────────────────────────────────────
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
vi.stubGlobal('ResizeObserver', ResizeObserverStub)

// Node 26 自带实验性全局 localStorage（未加 --localstorage-file 时为 undefined），
// vitest populateGlobal 对「已存在于 Node 全局的 key」跳过 jsdom 注入 → window.localStorage 为
// undefined，会话/语言持久化全部静默失效（评审 F-R1）。统一 stub 内存实现保证测试环境可用：
if (typeof window.localStorage === 'undefined' || window.localStorage === null) {
  const memStore = new Map<string, string>()
  const memStorage: Storage = {
    get length() { return memStore.size },
    clear: () => { memStore.clear() },
    getItem: (k) => memStore.get(k) ?? null,
    key: (i) => [...memStore.keys()][i] ?? null,
    removeItem: (k) => { memStore.delete(k) },
    setItem: (k, v) => { memStore.set(k, String(v)) },
  }
  Object.defineProperty(window, 'localStorage', { value: memStorage, configurable: true, writable: true })
}

// multipart 上传：undici fetch 不认 jsdom 的 FormData/File（instanceof 检查失败，内容丢失），
// 测试环境统一换成 undici FormData + node:buffer Blob/File。
// Node 26 内置 undici 改为严格 instanceof 校验：外部 undici 包的 FormData 同样不被识别
// （序列化成 "[object FormData]"、content-type 丢失）→ request.formData() 抛
// "Content-Type was not one of multipart/form-data"。统一方案：fetch 包装层手动序列化 multipart
// （见 beforeAll），两种 Node 版本走同一路径。
import { Blob as NodeBlob, File as NodeFile } from 'node:buffer'
import { FormData as UndiciFormData } from 'undici'
vi.stubGlobal('FormData', UndiciFormData)
vi.stubGlobal('Blob', NodeBlob)
vi.stubGlobal('File', NodeFile)

/** 手动 multipart 序列化（fetch 包装层用，保证任意 Node 版本 request.formData() 可解析） */
async function serializeMultipart(fd: FormData): Promise<{ body: Uint8Array; contentType: string }> {
  const CRLF = String.fromCharCode(13) + String.fromCharCode(10) // multipart 行分隔必须是 CRLF
  const boundary = `----awms-test-${Math.random().toString(36).slice(2)}`
  const encoder = new TextEncoder()
  const chunks: Uint8Array[] = []
  const push = (s: string) => chunks.push(encoder.encode(s))
  for (const [name, value] of fd.entries()) {
    push(`--${boundary}${CRLF}`)
    if (typeof value === 'string') {
      push(`Content-Disposition: form-data; name="${name}"${CRLF}${CRLF}${value}${CRLF}`)
    } else {
      const file = value as Blob & { name?: string; type?: string }
      push(`Content-Disposition: form-data; name="${name}"; filename="${file.name ?? 'file'}"${CRLF}`)
      push(`Content-Type: ${file.type || 'application/octet-stream'}${CRLF}${CRLF}`)
      chunks.push(new Uint8Array(await file.arrayBuffer()))
      push(CRLF)
    }
  }
  push(`--${boundary}--${CRLF}`)
  const total = chunks.reduce((n, c) => n + c.length, 0)
  const body = new Uint8Array(total)
  let off = 0
  for (const c of chunks) {
    body.set(c, off)
    off += c.length
  }
  return { body, contentType: `multipart/form-data; boundary=${boundary}` }
}

Object.defineProperty(URL, 'createObjectURL', {
  writable: true,
  value: vi.fn(() => 'blob:mock-url'),
})
Object.defineProperty(URL, 'revokeObjectURL', {
  writable: true,
  value: vi.fn(),
})

if (!window.matchMedia) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }),
  })
}

// Element.prototype.scrollIntoView（jsdom 未实现）
if (!Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = () => {}
}
