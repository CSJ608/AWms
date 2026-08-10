/**
 * 格式化工具 —— 数量 decimal(18,4) 字符串去尾零展示、等宽数字（契约 2.3）、时间本地化。
 */
import Decimal from 'decimal.js'
import dayjs from 'dayjs'

/** "10.0000" → "10"；"0.5000" → "0.5"（去尾零，契约 decimal 字符串） */
export function formatQuantity(value: string | number | null | undefined): string {
  if (value === null || value === undefined || value === '') return '-'
  try {
    const d = new Decimal(String(value))
    return d.isNaN() ? String(value) : d.toFixed()
  } catch {
    return String(value)
  }
}

/** ISO UTC → 本地日期时间（契约 2.3：时间 ISO 8601 UTC） */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) return '-'
  const d = dayjs(value)
  return d.isValid() ? d.format('YYYY-MM-DD HH:mm') : value
}

/** 日期字段（yyyy-MM-dd）直接展示 */
export function formatDate(value: string | null | undefined): string {
  if (!value) return '-'
  return value
}

/** 生成幂等键（Idempotency-Key：按用户动作粒度，重试沿用同一 key） */
export function genIdempotencyKey(): string {
  return crypto.randomUUID()
}
