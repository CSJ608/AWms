/**
 * 单飞刷新：并发 401 共享同一次 refresh 请求（评审 B-31：以 401 响应触发，不依赖本地倒计时）。
 */
let inFlight: Promise<string | null> | null = null

export function singleFlight(refreshFn: () => Promise<string | null>): Promise<string | null> {
  if (!inFlight) {
    inFlight = refreshFn().finally(() => {
      inFlight = null
    })
  }
  return inFlight
}
