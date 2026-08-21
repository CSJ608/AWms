import { useCallback, useRef } from 'react'
import { genIdempotencyKey } from './format'

/**
 * 同一请求指纹在失败重试时复用 key；业务成功后显式 clear，下一次用户操作生成新 key。
 */
export function useStableIdempotencyKey() {
  const keys = useRef(new Map<string, string>())

  const getKey = useCallback((fingerprint: string) => {
    const existing = keys.current.get(fingerprint)
    if (existing) return existing
    const next = genIdempotencyKey()
    keys.current.set(fingerprint, next)
    return next
  }, [])

  const clearKey = useCallback((fingerprint: string) => {
    keys.current.delete(fingerprint)
  }, [])

  return { getKey, clearKey }
}
