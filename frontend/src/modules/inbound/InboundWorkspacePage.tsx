import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertTriangle, Eye, FileText, PackagePlus, Printer, RotateCw, X,
} from 'lucide-react'
import type { ReactNode } from 'react'
import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import {
  apiCreateInboundOrder, apiGetInboundOrder, apiGetReceipt, apiListInboundOrders, apiListQualityExceptions,
  apiListReceipts, apiPrintBatchLabels, apiPrintExternalLabels, apiPrintInboundOrderQr, apiPrintReceipt,
  apiPrintUniqueLabels, apiResolveQualityException, apiRetryPrintJob, apiVoidInboundOrder,
} from '@/api'
import type {
  InboundOrder, InboundOrderCreateRequest, InboundOrderLine, InboundOrderType, PrintJob,
  QualityExceptionItem, QualityResolutionAction, Receipt, ReceiptLine, SourceType,
} from '@/api/types'
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter,
  AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { PrintJobItems } from '@/components/PrintJobItems'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/platform/auth/auth-context'
import { useStableIdempotencyKey } from '@/platform/idempotency'
import { ReferencePicker } from '@/platform/picker/ReferencePicker'
import {
  orderTypeText, qualityReasonText, qtyText, resolutionText, sourceTypeText, statusBadge,
} from './format'

type WorkTab = { key: string; title: string; path: string; closable: boolean }
type InboundView = 'orders' | 'receipts' | 'exceptions'

interface NewOrderDraft {
  type: InboundOrderType
  warehouseId: string
  sourceId: string
  sourceCode: string
  lines: Array<{ materialId: string; expectedQty: string }>
}

const BASE_TAB: WorkTab = { key: 'inbound', title: '入库管理', path: '/inbound/orders', closable: false }
const inputClass = 'h-8 rounded-lg border border-input bg-background px-2 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/40'
const selectClass = `${inputClass} pr-7`
const tableClass = 'w-full border-separate border-spacing-0 text-sm'
const thClass = 'border-b bg-muted/40 px-2 py-2 text-left text-xs font-medium text-muted-foreground'
const tdClass = 'border-b px-2 py-2 align-middle'

function emptyDraft(): NewOrderDraft {
  return {
    type: 'PO',
    warehouseId: '',
    sourceId: '',
    sourceCode: '',
    lines: [{ materialId: '', expectedQty: '1.0000' }],
  }
}

export function InboundWorkspacePage() {
  const location = useLocation()
  const navigate = useNavigate()
  const [tabs, setTabs] = useState<WorkTab[]>([BASE_TAB])
  const [activeKey, setActiveKey] = useState('inbound')
  const [recentKeys, setRecentKeys] = useState<string[]>(['inbound'])
  const [closeTarget, setCloseTarget] = useState<WorkTab | null>(null)
  const [draft, setDraft] = useState<NewOrderDraft>(() => emptyDraft())
  const draftDirty = isDraftDirty(draft)
  const scrollPositions = useRef<Record<string, number>>({})

  useLayoutEffect(() => {
    const main = document.querySelector<HTMLElement>('[data-app-scroll]')
    const positions = scrollPositions.current
    if (!main) return
    main.scrollTop = positions[location.pathname] ?? 0
    return () => { positions[location.pathname] = main.scrollTop }
  }, [location.pathname])

  useEffect(() => {
    if (location.pathname === '/inbound' || location.pathname === '/inbound/') return
    const next = tabForPath(location.pathname)
    setActiveKey(next.key)
    setRecentKeys((current) => [next.key, ...current.filter((key) => key !== next.key)])
    setTabs((prev) => {
      const existing = prev.find((tab) => tab.key === next.key)
      if (next.key === 'inbound') {
        return prev.map((tab) => {
          if (tab.key !== 'inbound') return tab
          return tab.path === next.path ? tab : { ...tab, path: next.path }
        })
      }
      if (existing) return prev
      return [...prev, next]
    })
  }, [location.pathname])

  if (location.pathname === '/inbound' || location.pathname === '/inbound/') {
    return <Navigate to="/inbound/orders" replace />
  }

  const closeTab = (tab: WorkTab) => {
    if (tab.key === 'order:new' && draftDirty) {
      setCloseTarget(tab)
      return
    }
    doCloseTab(tab)
  }

  const doCloseTab = (tab: WorkTab) => {
    setTabs((prev) => prev.filter((item) => item.key !== tab.key))
    if (activeKey === tab.key) {
      const fallbackKey = recentKeys.find((key) => key !== tab.key && tabs.some((item) => item.key === key))
      const fallback = tabs.find((item) => item.key === fallbackKey) ?? tabs.find((item) => item.key === 'inbound') ?? BASE_TAB
      navigate(fallback.path)
    }
    setRecentKeys((current) => current.filter((key) => key !== tab.key))
    if (tab.key === 'order:new') setDraft(emptyDraft())
    setCloseTarget(null)
  }

  const handleCreated = (order: InboundOrder) => {
    setDraft(emptyDraft())
    setTabs((prev) => prev.map((tab) => (
      tab.key === 'order:new'
        ? { key: `order:${order.id}`, title: order.orderNo, path: `/inbound/orders/${order.id}`, closable: true }
        : tab
    )))
    setActiveKey(`order:${order.id}`)
    navigate(`/inbound/orders/${order.id}`)
  }

  const titleOrder = (order: InboundOrder) => {
    setTabs((prev) => {
      let changed = false
      const next = prev.map((tab) => {
        if (tab.key !== `order:${order.id}`) return tab
        const path = `/inbound/orders/${order.id}`
        if (tab.title === order.orderNo && tab.path === path) return tab
        changed = true
        return { ...tab, title: order.orderNo, path }
      })
      return changed ? next : prev
    })
  }

  return (
    <div className="space-y-3" data-testid="inbound-workspace">
      <div className="flex min-h-10 items-end gap-1 overflow-x-auto border-b" data-testid="work-tabs">
        {tabs.map((tab) => (
          <div
            key={tab.key}
            className={`flex h-9 max-w-52 items-center gap-1 rounded-t-lg border px-2 text-sm ${
              activeKey === tab.key ? 'border-b-background bg-background font-medium' : 'bg-muted/50 text-muted-foreground'
            }`}
          >
            <button type="button" className="min-w-0 truncate" title={tab.title} onClick={() => navigate(tab.path)}>
              {tab.title}
            </button>
            {tab.closable && (
              <button
                type="button"
                className="rounded p-0.5 hover:bg-muted"
                aria-label={`关闭${tab.title}`}
                onClick={() => closeTab(tab)}
              >
                <X className="size-3.5" data-icon />
              </button>
            )}
          </div>
        ))}
      </div>

      <div hidden={location.pathname === '/inbound/orders/new' || /^\/inbound\/orders\/[^/]+$/.test(location.pathname)}>
        <InboundBusinessFrame view={businessView(location.pathname)} />
      </div>
      <RouteBody
        pathname={location.pathname}
        draft={draft}
        setDraft={setDraft}
        onCreated={handleCreated}
        onTitleOrder={titleOrder}
        onCancel={() => closeTab(tabForPath('/inbound/orders/new'))}
      />

      <AlertDialog open={!!closeTarget} onOpenChange={(open) => !open && setCloseTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>内容尚未提交</AlertDialogTitle>
            <AlertDialogDescription>关闭后本次填写不会保留。</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>继续编辑</AlertDialogCancel>
            <AlertDialogAction onClick={() => closeTarget && doCloseTab(closeTarget)}>放弃填写</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

function tabForPath(pathname: string): WorkTab {
  if (/^\/inbound\/orders\/new$/.test(pathname)) return { key: 'order:new', title: '新建入库单', path: pathname, closable: true }
  const detail = pathname.match(/^\/inbound\/orders\/([^/]+)$/)
  if (detail) {
    const key = `order:${detail[1]}`
    return { key, title: `单据 ${detail[1]}`, path: pathname, closable: true }
  }
  if (/^\/inbound\/(orders|receipts|exceptions)$/.test(pathname)) return { ...BASE_TAB, path: pathname }
  return BASE_TAB
}

function RouteBody({
  pathname, draft, setDraft, onCreated, onTitleOrder, onCancel,
}: {
  pathname: string
  draft: NewOrderDraft
  setDraft: (draft: NewOrderDraft) => void
  onCreated: (order: InboundOrder) => void
  onTitleOrder: (order: InboundOrder) => void
  onCancel: () => void
}) {
  if (pathname === '/inbound/orders/new') return <NewInboundOrderPage draft={draft} setDraft={setDraft} onCreated={onCreated} onCancel={onCancel} />
  const detail = pathname.match(/^\/inbound\/orders\/([^/]+)$/)
  if (detail) return <InboundOrderDetailPage orderId={detail[1]} onLoaded={onTitleOrder} />
  return null
}

function businessView(pathname: string): InboundView {
  if (pathname === '/inbound/receipts') return 'receipts'
  if (pathname === '/inbound/exceptions') return 'exceptions'
  return 'orders'
}

function InboundBusinessFrame({ view }: { view: InboundView }) {
  const navigate = useNavigate()
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-base font-semibold">入库管理</h2>
          <p className="text-sm text-muted-foreground">Web 建单、收货追踪和质检异常闭环</p>
        </div>
      </div>
      <div className="flex gap-1 border-b" data-testid="business-tabs">
        {[
          ['orders', '入库单', '/inbound/orders'],
          ['receipts', '收货记录', '/inbound/receipts'],
          ['exceptions', '质检异常', '/inbound/exceptions'],
        ].map(([key, title, path]) => (
          <button
            key={key}
            type="button"
            className={`h-8 border-b-2 px-3 text-sm ${view === key ? 'border-primary font-medium text-primary' : 'border-transparent text-muted-foreground'}`}
            onClick={() => navigate(path)}
          >
            {title}
          </button>
        ))}
      </div>
      <div hidden={view !== 'orders'}><InboundOrdersView /></div>
      <div hidden={view !== 'receipts'}><ReceiptsView /></div>
      <div hidden={view !== 'exceptions'}><QualityExceptionsView /></div>
    </div>
  )
}

function InboundOrdersView() {
  const navigate = useNavigate()
  const { hasPerm } = useAuth()
  const [orderNo, setOrderNo] = useState('')
  const [type, setType] = useState('')
  const [status, setStatus] = useState('')
  const [warehouseId, setWarehouseId] = useState('')
  const [page, setPage] = useState(1)
  const query = useQuery({
    queryKey: ['inbound-orders', orderNo, type, status, warehouseId, page],
    queryFn: () => apiListInboundOrders({ orderNo, type, status, warehouseId, page, pageSize: 20 }),
  })

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-2">
        <div className="flex flex-wrap items-end gap-2">
          <Field label="单号"><Input className="h-8 w-44" value={orderNo} onChange={(e) => setOrderNo(e.target.value)} /></Field>
          <Field label="类型">
            <select className={selectClass} value={type} onChange={(e) => setType(e.target.value)}>
              <option value="">全部</option>
              <option value="PO">采购</option>
              <option value="PR">生产</option>
              <option value="OT">其他</option>
            </select>
          </Field>
          <Field label="状态">
            <select className={selectClass} value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="">全部</option>
              <option value="CONFIRMED">已确认</option>
              <option value="RECEIVING">收货中</option>
              <option value="RECEIVED">已收货</option>
              <option value="VOIDED">已作废</option>
            </select>
          </Field>
          <Field label="仓库">
            <ReferencePicker resource="warehouses" value={warehouseId || null} onChange={(value) => { setWarehouseId(value ?? ''); setPage(1) }} query={{ status: 'ENABLED', page: 1, pageSize: 10 }} placeholder="全部仓库" className="w-52" />
          </Field>
          <Button variant="outline" size="sm" onClick={() => query.refetch()} data-testid="inbound-query">查询</Button>
          <Button variant="ghost" size="sm" onClick={() => { setOrderNo(''); setType(''); setStatus(''); setWarehouseId('') }}>重置</Button>
        </div>
        {hasPerm('action.inbound-order.create') && (
          <Button size="sm" onClick={() => navigate('/inbound/orders/new')} data-testid="new-inbound-order">
            <PackagePlus className="size-3.5" data-icon />
            新建入库单
          </Button>
        )}
      </div>
      <SimpleTable loading={query.isLoading} error={query.error} onRetry={() => query.refetch()}>
        <thead>
          <tr>
            <th className={thClass}>单号</th>
            <th className={thClass}>类型</th>
            <th className={thClass}>仓库</th>
            <th className={thClass}>来源</th>
            <th className={thClass}>状态</th>
            <th className={thClass}>创建时间</th>
          </tr>
        </thead>
        <tbody>
          {query.data?.items.map((order) => (
            <tr key={order.id}>
              <td className={tdClass}>
                <button type="button" className="font-medium text-primary tabular-nums" onClick={() => navigate(`/inbound/orders/${order.id}`)}>
                  {order.orderNo}
                </button>
              </td>
              <td className={tdClass}>{orderTypeText(order.type)}</td>
              <td className={tdClass}>{order.warehouseCode}</td>
              <td className={tdClass}>{sourceTypeText(order.sourceType)} {order.sourceCode ?? '-'}</td>
              <td className={tdClass}>{statusBadge(order.status)}</td>
              <td className={`${tdClass} tabular-nums`}>{order.createdAt.slice(0, 16).replace('T', ' ')}</td>
            </tr>
          ))}
        </tbody>
      </SimpleTable>
      <PageControls page={page} total={query.data?.total ?? 0} pageSize={20} onPageChange={setPage} />
    </div>
  )
}

function NewInboundOrderPage({
  draft, setDraft, onCreated, onCancel,
}: {
  draft: NewOrderDraft
  setDraft: (draft: NewOrderDraft) => void
  onCreated: (order: InboundOrder) => void
  onCancel: () => void
}) {
  const { getKey, clearKey } = useStableIdempotencyKey()
  const create = useMutation({
    mutationFn: (body: InboundOrderCreateRequest) => {
      const fingerprint = `inbound-order:${JSON.stringify(body)}`
      return apiCreateInboundOrder(body, getKey(fingerprint)).then((order) => ({ order, fingerprint }))
    },
    onSuccess: ({ order, fingerprint }) => {
      clearKey(fingerprint)
      toast.success('入库单已创建')
      onCreated(order)
    },
    onError: (e) => toast.error((e as Error).message),
  })

  const sourceType = sourceTypeForOrder(draft.type)
  const canSubmit = draft.warehouseId && draft.lines.every((l) => l.materialId && Number(l.expectedQty) > 0)
    && (draft.type === 'OT' || draft.sourceCode)

  const setType = (type: InboundOrderType) => {
    setDraft({ ...draft, type, sourceId: '', sourceCode: '' })
  }

  return (
    <div className="space-y-3">
      <div>
        <h2 className="text-base font-semibold">新建入库单</h2>
        <p className="text-sm text-muted-foreground">采购收货必须先建单；生产/其他也可在 PDA 无单新建。</p>
      </div>
      <div className="grid gap-3 md:grid-cols-4">
        <Field label="类型">
          <select className="h-8 w-full rounded-lg border border-input bg-background px-2 text-sm" value={draft.type} onChange={(e) => setType(e.target.value as InboundOrderType)}>
            <option value="PO">采购入库</option>
            <option value="PR">生产入库</option>
            <option value="OT">其他入库</option>
          </select>
        </Field>
        <Field label="仓库">
          <ReferencePicker resource="warehouses" value={draft.warehouseId || null} onChange={(value) => setDraft({ ...draft, warehouseId: value ?? '' })} query={{ status: 'ENABLED', page: 1, pageSize: 10 }} placeholder="选择仓库" />
        </Field>
        <Field label="来源类型">
          <Input className="h-8" value={sourceType ? sourceTypeText(sourceType) : '可空'} readOnly />
        </Field>
        <Field label="来源">
          <ReferencePicker
            resource="sources"
            value={draft.sourceId || null}
            onChange={(value) => { if (!value) setDraft({ ...draft, sourceId: '', sourceCode: '' }) }}
            onSelectItem={(item) => {
              const selected = item as { id: string; code: string } | null
              setDraft({ ...draft, sourceId: selected?.id ?? '', sourceCode: selected?.code ?? '' })
            }}
            query={{ status: 'ENABLED', ...(sourceType ? { type: sourceType } : {}), page: 1, pageSize: 10 }}
            placeholder={draft.type === 'OT' ? '可不填写' : '选择来源'}
          />
        </Field>
      </div>
      <div className="overflow-hidden rounded-lg border bg-card">
        <table className={tableClass}>
          <thead>
            <tr>
              <th className={thClass}>行号</th>
              <th className={thClass}>物料</th>
              <th className={thClass}>应到数量</th>
              <th className={thClass}>操作</th>
            </tr>
          </thead>
          <tbody>
            {draft.lines.map((line, index) => (
              <tr key={index}>
                <td className={tdClass}>{index + 1}</td>
                <td className={tdClass}>
                  <ReferencePicker resource="materials" value={line.materialId || null} onChange={(value) => updateDraftLine(draft, setDraft, index, { materialId: value ?? '' })} query={{ status: 'ENABLED', page: 1, pageSize: 10 }} placeholder="选择物料" />
                </td>
                <td className={tdClass}>
                  <Input
                    className="h-8 w-32 tabular-nums"
                    value={line.expectedQty}
                    onChange={(e) => updateDraftLine(draft, setDraft, index, { expectedQty: e.target.value })}
                  />
                </td>
                <td className={tdClass}>
                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={draft.lines.length === 1}
                    onClick={() => setDraft({ ...draft, lines: draft.lines.filter((_, i) => i !== index) })}
                  >
                    删除
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="flex justify-between">
        <Button
          variant="outline"
          size="sm"
          onClick={() => setDraft({ ...draft, lines: [...draft.lines, { materialId: '', expectedQty: '1.0000' }] })}
        >
          添加一行
        </Button>
        <div className="flex gap-2">
          <Button variant="outline" onClick={onCancel}>取消</Button>
          <Button disabled={!canSubmit || create.isPending} onClick={() => create.mutate(toCreateRequest(draft, sourceType))} data-testid="create-and-view">
            创建并查看
          </Button>
        </div>
      </div>
    </div>
  )
}

function InboundOrderDetailPage({ orderId, onLoaded }: { orderId: string; onLoaded: (order: InboundOrder) => void }) {
  const { hasPerm } = useAuth()
  const qc = useQueryClient()
  const [voidOpen, setVoidOpen] = useState(false)
  const [voidReason, setVoidReason] = useState('')
  const [printTarget, setPrintTarget] = useState<PrintTarget | null>(null)
  const [previewJob, setPreviewJob] = useState<PrintJob | null>(null)
  const { getKey, clearKey } = useStableIdempotencyKey()
  const query = useQuery({ queryKey: ['inbound-order', orderId], queryFn: () => apiGetInboundOrder(orderId) })
  const order = query.data

  useEffect(() => {
    if (order) onLoaded(order)
  }, [order, onLoaded])

  const voidMutation = useMutation({
    mutationFn: () => {
      const body = { reason: voidReason }
      const fingerprint = `void-order:${orderId}:${JSON.stringify(body)}`
      return apiVoidInboundOrder(orderId, body, getKey(fingerprint)).then((next) => ({ next, fingerprint }))
    },
    onSuccess: ({ next, fingerprint }) => {
      clearKey(fingerprint)
      toast.success('入库单已作废')
      setVoidOpen(false)
      setVoidReason('')
      qc.setQueryData(['inbound-order', orderId], next)
      void qc.invalidateQueries({ queryKey: ['inbound-orders'] })
    },
    onError: (e) => toast.error((e as Error).message),
  })
  const orderQr = useMutation({
    mutationFn: () => {
      const fingerprint = `print-order:${orderId}`
      return apiPrintInboundOrderQr(orderId, getKey(fingerprint)).then((job) => ({ job, fingerprint }))
    },
    onSuccess: ({ job, fingerprint }) => { clearKey(fingerprint); setPreviewJob(job) },
    onError: (e) => toast.error((e as Error).message),
  })

  if (query.isLoading) return <Skeleton className="h-64 w-full" />
  if (query.error || !order) return <ErrorBlock message={(query.error as Error)?.message ?? '入库单不存在'} onRetry={() => query.refetch()} />

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h2 className="text-base font-semibold tabular-nums">{order.orderNo}</h2>
          <div className="mt-1 flex flex-wrap gap-2 text-sm text-muted-foreground">
            <span>{orderTypeText(order.type)}</span>
            <span>{order.warehouseCode}</span>
            <span>{sourceTypeText(order.sourceType)} {order.sourceCode ?? '-'}</span>
            <span>{statusBadge(order.status)}</span>
          </div>
        </div>
        <div className="flex gap-2">
          {hasPerm('action.print.create') && (
            <Button variant="outline" size="sm" onClick={() => orderQr.mutate()}>
              <FileText className="size-3.5" data-icon />
              打印单据码
            </Button>
          )}
          {hasPerm('action.inbound-order.void') && (
            <Button variant="destructive" size="sm" disabled={!['CONFIRMED', 'RECEIVING'].includes(order.status)} onClick={() => setVoidOpen(true)}>
              作废
            </Button>
          )}
        </div>
      </div>
      <div className="overflow-hidden rounded-lg border bg-card">
        <table className={tableClass}>
          <thead>
            <tr>
              <th className={thClass}>行号</th>
              <th className={thClass}>物料</th>
              <th className={thClass}>应到</th>
              <th className={thClass}>已收</th>
              <th className={thClass}>未收</th>
              <th className={thClass}>唯一码</th>
              <th className={`${thClass} text-right`}>操作</th>
            </tr>
          </thead>
          <tbody>
            {order.lines.map((line) => (
              <tr key={line.id}>
                <td className={tdClass}>{line.lineNo}</td>
                <td className={tdClass}>{line.materialCode} {line.materialName}</td>
                <td className={`${tdClass} tabular-nums`}>{qtyText(line.expectedQty)}</td>
                <td className={`${tdClass} tabular-nums`}>{qtyText(line.receivedQty)}</td>
                <td className={`${tdClass} tabular-nums`}>{qtyText(line.remainingQty)}</td>
                <td className={tdClass}>{line.uniqueCodes.length} 个 / {qtyText(sumUnique(line.uniqueCodes))}</td>
                <td className={`${tdClass} text-right`}>
                  {hasPerm('action.print.create') && (
                    <div className="flex justify-end gap-1">
                      <Button variant="ghost" size="sm" onClick={() => setPrintTarget({ kind: 'external', order, line })}>打印外标签</Button>
                      <Button variant="ghost" size="sm" onClick={() => setPrintTarget({ kind: 'unique', order, line })}>生成唯一码标签</Button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <AlertDialog open={voidOpen} onOpenChange={setVoidOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>确认作废</AlertDialogTitle>
            <AlertDialogDescription>作废后不可继续收货，请填写原因。</AlertDialogDescription>
          </AlertDialogHeader>
          <Input value={voidReason} onChange={(e) => setVoidReason(e.target.value)} placeholder="作废原因" />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={voidMutation.isPending}>取消</AlertDialogCancel>
            <AlertDialogAction disabled={!voidReason.trim() || voidMutation.isPending} onClick={(e) => { e.preventDefault(); voidMutation.mutate() }}>
              确认作废
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      <PrintParamDialog target={printTarget} onOpenChange={(open) => !open && setPrintTarget(null)} onPreview={setPreviewJob} />
      <PrintPreviewDialog job={previewJob} onOpenChange={(open) => !open && setPreviewJob(null)} />
    </div>
  )
}

type PrintTarget = { kind: 'external' | 'unique'; order: InboundOrder; line: InboundOrderLine }

function PrintParamDialog({ target, onOpenChange, onPreview }: {
  target: PrintTarget | null
  onOpenChange: (open: boolean) => void
  onPreview: (job: PrintJob) => void
}) {
  const [count, setCount] = useState('1')
  const [qtyPerCode, setQtyPerCode] = useState('1')
  const [confirmDiff, setConfirmDiff] = useState(false)
  const { getKey, clearKey } = useStableIdempotencyKey()
  const mutation = useMutation({
    mutationFn: async () => {
      if (!target) throw new Error('缺少打印上下文')
      if (target.kind === 'external') {
        const body = {
          items: [{
            materialId: target.line.materialId,
            count: Number(count),
            inboundOrderLineId: target.line.id,
            rt: target.order.sourceType === 'SUPPLIER' ? 'S' as const : target.order.sourceType === 'WORKSHOP' ? 'W' as const : undefined,
            rc: target.order.sourceCode ?? undefined,
          }],
        }
        const fingerprint = `print-external:${JSON.stringify(body)}`
        return apiPrintExternalLabels(body, getKey(fingerprint)).then((job) => ({ job, fingerprint }))
      }
      const body = {
        inboundOrderLineId: target.line.id,
        count: Number(count),
        qtyPerCode,
      }
      const fingerprint = `print-unique:${JSON.stringify(body)}`
      return apiPrintUniqueLabels(body, getKey(fingerprint)).then((job) => ({ job, fingerprint }))
    },
    onSuccess: ({ job, fingerprint }) => {
      clearKey(fingerprint)
      onPreview(job)
      onOpenChange(false)
    },
    onError: (e) => toast.error((e as Error).message),
  })

  useEffect(() => {
    setCount('1')
    setQtyPerCode('1')
    setConfirmDiff(false)
  }, [target])

  const registrationQty = Number(count || 0) * Number(qtyPerCode || 0)
  const registered = target ? Number(sumUnique(target.line.uniqueCodes)) : 0
  const expected = target ? Number(target.line.expectedQty) : 0
  const overPo = target?.kind === 'unique' && target.order.type === 'PO' && registered + registrationQty > expected
  const lenientDiff = target?.kind === 'unique' && target.order.type !== 'PO' && registered + registrationQty !== expected
  const disabled = mutation.isPending || Number(count) < 1 || Number(count) > 1000 || (target?.kind === 'unique' && (Number(qtyPerCode) <= 0 || overPo || (lenientDiff && !confirmDiff)))

  return (
    <Dialog open={!!target} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{target?.kind === 'external' ? '打印外标签' : '生成唯一码标签'}</DialogTitle>
        </DialogHeader>
        {target && (
          <div className="space-y-3 text-sm">
            <div className="rounded-lg border bg-muted/30 p-3">
              <p className="font-medium">{target.line.materialCode} {target.line.materialName}</p>
              <p className="text-muted-foreground">{target.order.orderNo} · {sourceTypeText(target.order.sourceType)} {target.order.sourceCode ?? '-'}</p>
            </div>
            <Field label="张数">
              <Input className="h-8 tabular-nums" value={count} onChange={(e) => setCount(e.target.value)} />
            </Field>
            {target.kind === 'unique' && (
              <>
                <Field label="每码数量">
                  <Input className="h-8 tabular-nums" value={qtyPerCode} onChange={(e) => setQtyPerCode(e.target.value)} />
                </Field>
                <p className={overPo ? 'text-sm text-destructive' : 'text-sm text-muted-foreground'}>
                  本次登记数量 = {qtyText(String(registrationQty))}；累计已登记 / 应到 = {qtyText(String(registered + registrationQty))} / {qtyText(target.line.expectedQty)}
                </p>
                {lenientDiff && (
                  <label className="flex items-center gap-2 text-sm">
                    <input type="checkbox" checked={confirmDiff} onChange={(e) => setConfirmDiff(e.target.checked)} />
                    确认登记数量与应到存在差异
                  </label>
                )}
              </>
            )}
          </div>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>取消</Button>
          <Button disabled={disabled} onClick={() => mutation.mutate()}>
            <Printer className="size-3.5" data-icon />
            生成并预览
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function ReceiptsView() {
  const { hasPerm } = useAuth()
  const [receiptNo, setReceiptNo] = useState('')
  const [status, setStatus] = useState('')
  const [warehouseId, setWarehouseId] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [previewJob, setPreviewJob] = useState<PrintJob | null>(null)
  const [page, setPage] = useState(1)
  const query = useQuery({
    queryKey: ['receipts', receiptNo, status, warehouseId, page],
    queryFn: () => apiListReceipts({ receiptNo, status, warehouseId, page, pageSize: 20 }),
  })
  const detail = useQuery({ queryKey: ['receipt', selectedId], queryFn: () => apiGetReceipt(selectedId!), enabled: !!selectedId })

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="收货单号"><Input className="h-8 w-44" value={receiptNo} onChange={(e) => setReceiptNo(e.target.value)} /></Field>
        <Field label="状态">
          <select className={selectClass} value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">全部</option>
            <option value="RECEIVING">收货中</option>
            <option value="CHECKING">待质检</option>
            <option value="PUTAWAY">待上架</option>
            <option value="DONE">已完成</option>
          </select>
        </Field>
        <Field label="仓库">
          <ReferencePicker resource="warehouses" value={warehouseId || null} onChange={(value) => { setWarehouseId(value ?? ''); setPage(1) }} query={{ status: 'ENABLED', page: 1, pageSize: 10 }} placeholder="全部仓库" className="w-52" />
        </Field>
        <Button variant="outline" size="sm" onClick={() => query.refetch()}>查询</Button>
      </div>
      <SimpleTable loading={query.isLoading} error={query.error} onRetry={() => query.refetch()}>
        <thead>
          <tr>
            <th className={thClass}>收货单号</th>
            <th className={thClass}>来源单号</th>
            <th className={thClass}>仓库</th>
            <th className={thClass}>状态</th>
            <th className={thClass}>操作人</th>
            <th className={thClass}>发生时间</th>
            <th className={thClass}>照片</th>
          </tr>
        </thead>
        <tbody>
          {query.data?.items.map((receipt) => (
            <tr key={receipt.id}>
              <td className={tdClass}>
                <button type="button" className="font-medium text-primary tabular-nums" onClick={() => setSelectedId(receipt.id)}>
                  {receipt.receiptNo}
                </button>
              </td>
              <td className={tdClass}>{receipt.sourceDocNo ?? '-'}</td>
              <td className={tdClass}>{receipt.warehouseCode}</td>
              <td className={tdClass}>{statusBadge(receipt.status)}</td>
              <td className={tdClass}>{receipt.operatorName}</td>
              <td className={`${tdClass} tabular-nums`}>{receipt.occurredAt.slice(0, 16).replace('T', ' ')}</td>
              <td className={tdClass}>{receipt.photos.length} 张</td>
            </tr>
          ))}
        </tbody>
      </SimpleTable>
      <PageControls page={page} total={query.data?.total ?? 0} pageSize={20} onPageChange={setPage} />
      {detail.data && (
        <ReceiptDetail receipt={detail.data} canPrint={hasPerm('action.print.create')} onPreview={setPreviewJob} />
      )}
      <PrintPreviewDialog job={previewJob} onOpenChange={(open) => !open && setPreviewJob(null)} />
    </div>
  )
}

function ReceiptDetail({ receipt, canPrint, onPreview }: { receipt: Receipt; canPrint: boolean; onPreview: (job: PrintJob) => void }) {
  const { getKey, clearKey } = useStableIdempotencyKey()
  const printReceipt = useMutation({
    mutationFn: () => {
      const fingerprint = `print-receipt:${receipt.id}`
      return apiPrintReceipt(receipt.id, getKey(fingerprint)).then((job) => ({ job, fingerprint }))
    },
    onSuccess: ({ job, fingerprint }) => { clearKey(fingerprint); onPreview(job) },
    onError: (e) => toast.error((e as Error).message),
  })
  const printBatch = useMutation({
    mutationFn: (line: ReceiptLine) => {
      const body = { receiptLineId: line.id }
      const fingerprint = `print-batch:${JSON.stringify(body)}`
      return apiPrintBatchLabels(body, getKey(fingerprint)).then((job) => ({ job, fingerprint }))
    },
    onSuccess: ({ job, fingerprint }) => { clearKey(fingerprint); onPreview(job) },
    onError: (e) => toast.error((e as Error).message),
  })
  return (
    <div className="space-y-3 rounded-lg border bg-card p-3">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="font-medium tabular-nums">{receipt.receiptNo}</h3>
          <p className="text-sm text-muted-foreground">{receipt.sourceDocType} · {receipt.stagingLocationCode} · {statusBadge(receipt.status)}</p>
        </div>
        {canPrint && <Button variant="outline" size="sm" onClick={() => printReceipt.mutate()}>打印/补打收货回执</Button>}
      </div>
      <div className="overflow-hidden rounded-lg border">
        <table className={tableClass}>
          <thead>
            <tr>
              <th className={thClass}>物料</th>
              <th className={thClass}>批次</th>
              <th className={thClass}>应到/实收/差异</th>
              <th className={thClass}>质检/上架</th>
              <th className={`${thClass} text-right`}>操作</th>
            </tr>
          </thead>
          <tbody>
            {receipt.lines.map((line) => (
              <tr key={line.id}>
                <td className={tdClass}>{line.materialCode} {line.materialName}</td>
                <td className={`${tdClass} tabular-nums`}>{line.batchNo}</td>
                <td className={`${tdClass} tabular-nums`}>{qtyText(line.expectedQty)} / {qtyText(line.actualQty)} / {qtyText(line.qtyDiff)}</td>
                <td className={tdClass}>{statusBadge(line.status)}</td>
                <td className={`${tdClass} text-right`}>
                  {canPrint && <Button variant="ghost" size="sm" onClick={() => printBatch.mutate(line)}>预览批次标签</Button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div>
        <p className="mb-2 text-sm font-medium">照片</p>
        {receipt.photos.length === 0 ? (
          <p className="text-sm text-muted-foreground">无照片</p>
        ) : (
          <div className="grid grid-cols-3 gap-2 sm:grid-cols-6">
            {receipt.photos.map((id) => (
              <a key={id} href={`/api/attachments/${id}`} target="_blank" rel="noreferrer" className="block aspect-square overflow-hidden rounded-lg border bg-muted/30">
                <img src={`/api/attachments/${id}/thumbnail`} alt={`收货附件 ${id}`} className="size-full object-cover" />
              </a>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

function QualityExceptionsView() {
  const { hasPerm } = useAuth()
  const qc = useQueryClient()
  const [status, setStatus] = useState('PENDING')
  const [warehouseId, setWarehouseId] = useState('')
  const [reason, setReason] = useState('')
  const [page, setPage] = useState(1)
  const [detail, setDetail] = useState<QualityExceptionItem | null>(null)
  const [resolveTarget, setResolveTarget] = useState<{ item: QualityExceptionItem; action: QualityResolutionAction } | null>(null)
  const [note, setNote] = useState('')
  const { getKey, clearKey } = useStableIdempotencyKey()
  const query = useQuery({
    queryKey: ['quality-exceptions', status, warehouseId, reason, page],
    queryFn: () => apiListQualityExceptions({ resolutionStatus: status, warehouseId, exceptionReason: reason, page, pageSize: 20 }),
  })
  const resolveMutation = useMutation({
    mutationFn: () => {
      const body = { action: resolveTarget!.action, note }
      const fingerprint = `resolve-quality:${resolveTarget!.item.id}:${JSON.stringify(body)}`
      return apiResolveQualityException(resolveTarget!.item.id, body, getKey(fingerprint)).then(() => fingerprint)
    },
    onSuccess: (fingerprint) => {
      clearKey(fingerprint)
      toast.success('异常已处理')
      setResolveTarget(null)
      setNote('')
      void qc.invalidateQueries({ queryKey: ['quality-exceptions'] })
    },
    onError: (e) => toast.error((e as Error).message),
  })

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="状态">
          <select className={selectClass} value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="PENDING">待处理</option>
            <option value="RESOLVED">已处理</option>
          </select>
        </Field>
        <Field label="仓库">
          <ReferencePicker resource="warehouses" value={warehouseId || null} onChange={(value) => { setWarehouseId(value ?? ''); setPage(1) }} query={{ status: 'ENABLED', page: 1, pageSize: 10 }} placeholder="全部仓库" className="w-52" />
        </Field>
        <Field label="原因">
          <select className={selectClass} value={reason} onChange={(e) => setReason(e.target.value)}>
            <option value="">全部</option>
            <option value="DAMAGED">破损</option>
            <option value="QTY_MISMATCH">数量不符</option>
            <option value="OTHER">其他</option>
          </select>
        </Field>
        <Button variant="outline" size="sm" onClick={() => query.refetch()}>查询</Button>
      </div>
      <SimpleTable loading={query.isLoading} error={query.error} onRetry={() => query.refetch()}>
        <thead>
          <tr>
            <th className={thClass}>收货单</th>
            <th className={thClass}>物料/批次</th>
            <th className={thClass}>数量</th>
            <th className={thClass}>原因</th>
            <th className={thClass}>照片</th>
            <th className={thClass}>上报人/时间</th>
            <th className={thClass}>状态</th>
            <th className={`${thClass} text-right`}>操作</th>
          </tr>
        </thead>
        <tbody>
          {query.data?.items.map((item) => (
            <tr key={item.id}>
              <td className={`${tdClass} tabular-nums`}>{item.receiptNo}</td>
              <td className={tdClass}>{item.materialCode} {item.materialName}<br /><span className="text-muted-foreground">{item.batchNo}</span></td>
              <td className={`${tdClass} tabular-nums`}>{qtyText(item.checkedQty)}</td>
              <td className={tdClass}>{qualityReasonText(item.exceptionReason)}</td>
              <td className={tdClass}>{item.photoIds.length} 张</td>
              <td className={tdClass}>{item.checkedByName}<br /><span className="tabular-nums text-muted-foreground">{item.checkedAt.slice(0, 16).replace('T', ' ')}</span></td>
              <td className={tdClass}>{resolutionText(item.resolutionAction)}</td>
              <td className={`${tdClass} text-right`}>
                <div className="flex justify-end gap-1">
                  <Button variant="ghost" size="icon-sm" title="查看详情" aria-label="查看异常详情" onClick={() => setDetail(item)}><Eye className="size-4" data-icon /></Button>
                  {hasPerm('action.quality.resolve') && !item.resolutionAction && (
                    <>
                    <Button variant="ghost" size="sm" onClick={() => setResolveTarget({ item, action: 'PASS' })}>放行</Button>
                    <Button variant="ghost" size="sm" className="text-destructive" onClick={() => setResolveTarget({ item, action: 'REJECT' })}>驳回</Button>
                    </>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </SimpleTable>
      <PageControls page={page} total={query.data?.total ?? 0} pageSize={20} onPageChange={setPage} />
      <ExceptionDetailDialog item={detail} onOpenChange={(open) => { if (!open) setDetail(null) }} />
      <AlertDialog open={!!resolveTarget} onOpenChange={(open) => !open && setResolveTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{resolveTarget?.action === 'PASS' ? '确认放行' : '确认驳回'}</AlertDialogTitle>
            <AlertDialogDescription>
              {resolveTarget?.action === 'PASS' ? '放行后库存转为可用并进入上架待办。' : '驳回后货物继续留在暂存区且不可上架。'}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <Input value={note} onChange={(e) => setNote(e.target.value)} placeholder={resolveTarget?.action === 'REJECT' ? '驳回备注必填' : '处理备注'} />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={resolveMutation.isPending}>取消</AlertDialogCancel>
            <AlertDialogAction
              disabled={resolveMutation.isPending || (resolveTarget?.action === 'REJECT' && !note.trim())}
              onClick={(e) => { e.preventDefault(); resolveMutation.mutate() }}
            >
              确认
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

function PrintPreviewDialog({ job, onOpenChange }: { job: PrintJob | null; onOpenChange: (open: boolean) => void }) {
  const [currentJob, setCurrentJob] = useState<PrintJob | null>(job)
  const { getKey, clearKey } = useStableIdempotencyKey()
  const retry = useMutation({
    mutationFn: () => {
      const fingerprint = `retry-print:${currentJob!.id}`
      return apiRetryPrintJob(currentJob!.id, getKey(fingerprint)).then((next) => ({ next, fingerprint }))
    },
    onSuccess: ({ next, fingerprint }) => { clearKey(fingerprint); setCurrentJob(next) },
    onError: (e) => toast.error((e as Error).message),
  })

  useEffect(() => setCurrentJob(job), [job])

  return (
    <Dialog open={!!job} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>打印预览</DialogTitle>
        </DialogHeader>
        {currentJob && (
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-sm">
              {statusBadge(currentJob.status)}
              <span className="text-muted-foreground">{currentJob.templateCode}</span>
              {currentJob.errorCode && <span className="text-destructive">{currentJob.errorCode}</span>}
            </div>
            <div className="grid max-h-96 gap-2 overflow-auto sm:grid-cols-2">
              <PrintJobItems items={currentJob.items} />
            </div>
          </div>
        )}
        <DialogFooter>
          {currentJob?.status === 'FAILED' && (
            <Button variant="outline" disabled={retry.isPending} onClick={() => retry.mutate()}>
              <RotateCw className="size-3.5" data-icon />
              重试
            </Button>
          )}
          {currentJob?.fileUrl && <Button variant="outline" asChild><a href={currentJob.fileUrl}>下载 PDF</a></Button>}
          <Button onClick={() => onOpenChange(false)}>关闭</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function ExceptionDetailDialog({ item, onOpenChange }: { item: QualityExceptionItem | null; onOpenChange: (open: boolean) => void }) {
  return (
    <Dialog open={!!item} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader><DialogTitle>质检异常详情</DialogTitle></DialogHeader>
        {item && (
          <div className="space-y-3 text-sm">
            <div className="grid grid-cols-2 gap-2">
              <p><span className="text-muted-foreground">收货单：</span>{item.receiptNo}</p>
              <p><span className="text-muted-foreground">仓库：</span>{item.warehouseCode}</p>
              <p><span className="text-muted-foreground">物料：</span>{item.materialCode} {item.materialName}</p>
              <p><span className="text-muted-foreground">批次：</span>{item.batchNo}</p>
              <p><span className="text-muted-foreground">异常数量：</span>{qtyText(item.checkedQty)}</p>
              <p><span className="text-muted-foreground">原因：</span>{qualityReasonText(item.exceptionReason)}</p>
            </div>
            <div><p className="text-muted-foreground">上报备注</p><p className="whitespace-pre-wrap">{item.note || '无'}</p></div>
            <div>
              <p className="mb-2 text-muted-foreground">异常照片</p>
              {item.photoIds.length === 0 ? <p>无</p> : (
                <div className="grid grid-cols-3 gap-2">
                  {item.photoIds.map((id) => (
                    <a key={id} href={`/api/attachments/${id}`} target="_blank" rel="noreferrer" className="block aspect-square overflow-hidden rounded border">
                      <img src={`/api/attachments/${id}/thumbnail`} alt={`异常附件 ${id}`} className="size-full object-cover" />
                    </a>
                  ))}
                </div>
              )}
            </div>
            {item.resolutionAction && (
              <div className="border-t pt-3">
                <p>{resolutionText(item.resolutionAction)} · {item.resolvedByName ?? '-'}</p>
                <p className="whitespace-pre-wrap text-muted-foreground">{item.resolutionNote || '无处理备注'}</p>
              </div>
            )}
          </div>
        )}
        <DialogFooter><Button onClick={() => onOpenChange(false)}>关闭</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function PageControls({ page, total, pageSize, onPageChange }: { page: number; total: number; pageSize: number; onPageChange: (page: number) => void }) {
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  return (
    <div className="flex items-center justify-end gap-2 text-sm text-muted-foreground">
      <span>第 {page} / {pageCount} 页，共 {total} 条</span>
      <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>上一页</Button>
      <Button size="sm" variant="outline" disabled={page >= pageCount} onClick={() => onPageChange(page + 1)}>下一页</Button>
    </div>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="grid gap-1 text-xs font-medium text-muted-foreground">
      <span>{label}</span>
      {children}
    </label>
  )
}

function SimpleTable({
  loading, error, onRetry, children,
}: {
  loading: boolean
  error: unknown
  onRetry: () => void
  children: ReactNode
}) {
  if (loading) {
    return (
      <div className="space-y-2 rounded-lg border bg-card p-3">
        {Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-8 w-full" />)}
      </div>
    )
  }
  if (error) return <ErrorBlock message={(error as Error).message} onRetry={onRetry} />
  return <div className="overflow-auto rounded-lg border bg-card"><table className={tableClass}>{children}</table></div>
}

function ErrorBlock({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="flex items-center justify-between rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
      <span className="flex items-center gap-2"><AlertTriangle className="size-4" data-icon />{message}</span>
      <Button variant="outline" size="sm" onClick={onRetry}>重试</Button>
    </div>
  )
}

function sourceTypeForOrder(type: InboundOrderType): SourceType | null {
  if (type === 'PO') return 'SUPPLIER'
  if (type === 'PR') return 'WORKSHOP'
  return null
}

function updateDraftLine(draft: NewOrderDraft, setDraft: (draft: NewOrderDraft) => void, index: number, patch: Partial<NewOrderDraft['lines'][number]>) {
  setDraft({ ...draft, lines: draft.lines.map((line, i) => (i === index ? { ...line, ...patch } : line)) })
}

function toCreateRequest(draft: NewOrderDraft, sourceType: SourceType | null): InboundOrderCreateRequest {
  return {
    type: draft.type,
    warehouseId: draft.warehouseId,
    sourceType,
    sourceCode: draft.sourceCode || null,
    lines: draft.lines.map((line) => ({ materialId: line.materialId, expectedQty: line.expectedQty })),
  }
}

function isDraftDirty(draft: NewOrderDraft): boolean {
  const base = emptyDraft()
  return JSON.stringify(draft) !== JSON.stringify(base)
}

function sumUnique(codes: InboundOrderLine['uniqueCodes']): string {
  return codes.reduce((sum, code) => sum + Number(code.quantity), 0).toFixed(4)
}
