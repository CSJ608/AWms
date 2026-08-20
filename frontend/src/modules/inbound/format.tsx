import type { ReactNode } from 'react'
import { Badge } from '@/components/ui/badge'
import type {
  InboundOrderStatus, InboundOrderType, PrintJobStatus, QualityExceptionReason, QualityResolutionAction,
  ReceiptLineStatus, ReceiptStatus, SourceType,
} from '@/api/types'

export function qtyText(value: string | null | undefined): string {
  if (value === null || value === undefined) return '-'
  const n = Number(value)
  if (!Number.isFinite(n)) return value
  return n.toLocaleString('zh-CN', { maximumFractionDigits: 4 })
}

export function sourceTypeText(value: SourceType | null | undefined): string {
  if (value === 'SUPPLIER') return '供应商'
  if (value === 'WORKSHOP') return '车间'
  return '-'
}

export function orderTypeText(value: InboundOrderType): string {
  if (value === 'PO') return '采购'
  if (value === 'PR') return '生产'
  return '其他'
}

export function qualityReasonText(value: QualityExceptionReason): string {
  if (value === 'DAMAGED') return '破损'
  if (value === 'QTY_MISMATCH') return '数量不符'
  return '其他'
}

export function resolutionText(value: QualityResolutionAction | null): string {
  if (value === 'PASS') return '放行'
  if (value === 'REJECT') return '驳回'
  return '待处理'
}

export function statusBadge(status: InboundOrderStatus | ReceiptStatus | ReceiptLineStatus | PrintJobStatus): ReactNode {
  const label = statusLabel(status)
  if (['RECEIVED', 'CHECKING', 'GENERATING'].includes(status)) return <Badge variant="warning">{label}</Badge>
  if (['DONE', 'CHECKED', 'PUTAWAY_DONE', 'READY'].includes(status)) return <Badge variant="success">{label}</Badge>
  if (['VOIDED', 'EXCEPTION', 'FAILED'].includes(status)) return <Badge variant="destructive">{label}</Badge>
  return <Badge variant="secondary">{label}</Badge>
}

function statusLabel(status: InboundOrderStatus | ReceiptStatus | ReceiptLineStatus | PrintJobStatus): string {
  switch (status) {
    case 'CONFIRMED': return '已确认'
    case 'RECEIVING': return '收货中'
    case 'RECEIVED': return '已收货'
    case 'VOIDED': return '已作废'
    case 'CHECKING': return '待质检'
    case 'PUTAWAY': return '待上架'
    case 'DONE': return '已完成'
    case 'CHECKED': return '已通过'
    case 'EXCEPTION': return '异常'
    case 'PUTAWAY_DONE': return '已上架'
    case 'GENERATING': return '生成中'
    case 'READY': return '可预览'
    case 'FAILED': return '生成失败'
  }
}

