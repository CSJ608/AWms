/** 会话过期事件：client 刷新失败时广播，AuthProvider 订阅后跳登录页 */
type Listener = () => void
const listeners = new Set<Listener>()

export function onSessionExpired(fn: Listener): () => void {
  listeners.add(fn)
  return () => {
    listeners.delete(fn)
  }
}

export function emitSessionExpired(): void {
  listeners.forEach((fn) => fn())
}
