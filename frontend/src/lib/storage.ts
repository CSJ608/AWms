/**
 * 环境兼容 localStorage 访问。
 * Node 26 自带实验性全局 localStorage（未加 --localstorage-file 时为 undefined），
 * 会遮蔽 jsdom 注入版 → 一律走 window.localStorage + 守卫（评审 F-R1 / 陷阱 19）。
 */
export function getLocalStorage(): Storage | null {
  try {
    if (typeof window === 'undefined') return null
    const s = window.localStorage
    return s ?? null
  } catch {
    // 隐私模式等场景访问 localStorage 可能抛错
    return null
  }
}
