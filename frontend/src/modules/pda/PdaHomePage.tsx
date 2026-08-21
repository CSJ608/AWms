import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertTriangle, ArrowLeft, Camera, CheckCircle2, ClipboardCheck, PackagePlus, Plus, Printer, RotateCw, ScanLine, Trash2,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { useEffect, useMemo, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import {
  apiCreatePutawayRecord, apiCreateReceipt, apiGetPutawayRecommendations, apiListLocations,
  apiListPutawayTodos, apiListQualityTodos, apiParseScan, apiPrintBatchLabels, apiPrintReceipt,
  apiQuickSearchMaterials, apiQuickSearchWarehouses, apiSubmitQualityCheck,
} from '@/api'
import { parseApiError } from '@/api/client'
import type {
  AttachmentItem, BatchItem, BatchProps, InboundOrderType, MaterialItem, PrintJob,
  PutawayTodo, QualityExceptionReason, QualityTodo, Receipt, ReceiptCreateRequest, ScanDocument,
  ScanMaterial, ScanResult, SourceItem, WarehouseItem,
} from '@/api/types'
import { PrintJobItems } from '@/components/PrintJobItems'
import { ProtectedImagePreview } from '@/components/ProtectedMedia'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { qtyText, statusBadge } from '@/modules/inbound/format'
import { useAuth } from '@/platform/auth/auth-context'
import { useStableIdempotencyKey } from '@/platform/idempotency'
import { ReferencePicker } from '@/platform/picker/ReferencePicker'
import { useAttachmentUploads } from './useAttachmentUploads'

const WAREHOUSE_STORAGE_KEY = 'awms:pda:warehouse-id'
type PdaEntryConfig = {
  path: 'receiving' | 'qc' | 'putaway'
  title: '收货' | '质检' | '上架'
  permission: 'action.receiving.create' | 'action.quality.check' | 'action.putaway.create'
  Icon: LucideIcon
}

const pdaEntryByCode: Record<string, PdaEntryConfig> = {
  'pda.receiving': { path: 'receiving', title: '收货', permission: 'action.receiving.create', Icon: ScanLine },
  'pda.qc': { path: 'qc', title: '质检', permission: 'action.quality.check', Icon: ClipboardCheck },
  'pda.putaway': { path: 'putaway', title: '上架', permission: 'action.putaway.create', Icon: PackagePlus },
  receiving: { path: 'receiving', title: '收货', permission: 'action.receiving.create', Icon: ScanLine },
  qc: { path: 'qc', title: '质检', permission: 'action.quality.check', Icon: ClipboardCheck },
  putaway: { path: 'putaway', title: '上架', permission: 'action.putaway.create', Icon: PackagePlus },
} as const

interface WarehouseContext {
  id: string
  code: string
  name: string
}

export function PdaHomePage() {
  const { session, hasPerm } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const page = location.pathname.split('/')[2] ?? ''
  const [warehouse, setWarehouse] = useState<WarehouseContext | null>(null)
  const warehouses = useQuery({
    queryKey: ['pda-warehouses'],
    queryFn: () => apiQuickSearchWarehouses('', 10),
  })

  useEffect(() => {
    if (warehouse || !warehouses.data?.items.length) return
    const enabled = warehouses.data.items.filter((item) => item.status === 'ENABLED')
    const saved = window.localStorage.getItem(WAREHOUSE_STORAGE_KEY)
    const selected = enabled.find((item) => item.id === saved) ?? enabled[0]
    if (selected) setWarehouse(toWarehouseContext(selected))
  }, [warehouse, warehouses.data])

  const selectWarehouse = (item: unknown | null) => {
    if (!item) {
      setWarehouse(null)
      window.localStorage.removeItem(WAREHOUSE_STORAGE_KEY)
      return
    }
    const selected = item as WarehouseItem
    setWarehouse(toWarehouseContext(selected))
    window.localStorage.setItem(WAREHOUSE_STORAGE_KEY, selected.id)
  }

  if (!warehouse) {
    return (
      <div className="mx-auto grid min-h-screen max-w-md content-center gap-3 p-4">
        <p className="text-center text-sm text-muted-foreground">选择当前作业仓库</p>
        <ReferencePicker
          resource="warehouses"
          value={null}
          onChange={() => undefined}
          onSelectItem={selectWarehouse}
          query={{ status: 'ENABLED', page: 1, pageSize: 10 }}
          placeholder="搜索仓库"
        />
      </div>
    )
  }

  if (page === 'receiving') return <PdaShell title="收货" warehouse={warehouse}><ReceivingPage warehouse={warehouse} /></PdaShell>
  if (page === 'qc') return <PdaShell title="质检" warehouse={warehouse}><QualityPage warehouse={warehouse} /></PdaShell>
  if (page === 'putaway') return <PdaShell title="上架" warehouse={warehouse}><PutawayPage warehouse={warehouse} /></PdaShell>

  const entries = (session?.menus.pda ?? [])
    .flatMap((entry) => {
      const config = pdaEntryByCode[entry.code]
      if (!config || !hasPerm('route.inbound') || !hasPerm(config.permission)) return []
      return [{ ...config, sort: entry.sort }]
    })
    .sort((a, b) => a.sort - b.sort)

  return (
    <div className="min-h-screen bg-background">
      <header className="sticky top-0 z-10 flex min-h-14 items-center justify-between bg-primary px-4 text-primary-foreground">
        <div>
          <p className="text-sm font-semibold">{session?.user.name ?? '作业员'}</p>
          <p className="text-xs opacity-85">{warehouse.code} {warehouse.name}</p>
        </div>
        <div className="w-48">
          <ReferencePicker
            resource="warehouses"
            value={warehouse.id}
            onChange={() => undefined}
            onSelectItem={selectWarehouse}
            query={{ status: 'ENABLED', page: 1, pageSize: 10 }}
          />
        </div>
      </header>
      <main className="mx-auto max-w-md space-y-4 p-4" data-testid="pda-menu">
        <div className="grid gap-3">
          {entries.map((entry) => {
            const { path, title, Icon } = entry
            return (
              <button
                key={path}
                type="button"
                className="flex min-h-16 items-center gap-3 rounded-lg border bg-card p-4 text-left shadow-sm active:translate-y-px"
                onClick={() => navigate(`/pda/${path}`)}
                aria-label={title}
              >
                <span className="flex size-12 items-center justify-center rounded-lg bg-primary/10 text-primary">
                  <Icon className="size-5" data-icon />
                </span>
                <span className="text-lg font-semibold">{title}</span>
              </button>
            )
          })}
        </div>
      </main>
    </div>
  )
}

function PdaShell({ title, warehouse, children }: { title: string; warehouse: WarehouseContext; children: ReactNode }) {
  const navigate = useNavigate()
  return (
    <div className="min-h-screen bg-background">
      <header className="sticky top-0 z-10 flex h-14 items-center justify-between border-b bg-card px-3">
        <Button variant="ghost" size="icon-sm" aria-label="返回" onClick={() => navigate('/pda')}>
          <ArrowLeft className="size-4" data-icon />
        </Button>
        <div className="text-center">
          <p className="text-sm font-semibold">{title}</p>
          <p className="text-xs text-muted-foreground">{warehouse.code}</p>
        </div>
        <span className="size-7" />
      </header>
      <main className="mx-auto max-w-md space-y-4 p-3 pb-24">{children}</main>
    </div>
  )
}

function ScanInput({ placeholder, onSubmit, disabled }: { placeholder: string; onSubmit: (value: string) => void; disabled?: boolean }) {
  const [value, setValue] = useState('')
  const submit = () => {
    if (!value.trim()) return
    onSubmit(value.trim())
    setValue('')
  }
  return (
    <div className="flex gap-2">
      <Input
        autoFocus
        className="h-11 text-base"
        value={value}
        disabled={disabled}
        placeholder={placeholder}
        onChange={(event) => setValue(event.target.value)}
        onKeyDown={(event) => { if (event.key === 'Enter') submit() }}
        data-testid="pda-scan-input"
      />
      <Button className="h-11" disabled={disabled} onClick={submit} data-testid="pda-scan-submit" aria-label="扫码确认">
        <ScanLine className="size-4" data-icon />
      </Button>
    </div>
  )
}

interface UniqueDraft { code: string; quantity: string }
interface ReceivingLineDraft {
  key: string
  orderLineId: string | null
  materialId: string
  materialCode: string
  materialName: string
  expectedQty: string | null
  batchControlled: boolean
  labelType: ScanMaterial['labelType']
  quantity: string
  uniqueCodes: UniqueDraft[]
  batchId: string | null
  batchProps: BatchProps | null
}

function ReceivingPage({ warehouse }: { warehouse: WarehouseContext }) {
  const [document, setDocument] = useState<ScanDocument | null>(null)
  const [sourceDocType, setSourceDocType] = useState<InboundOrderType | null>(null)
  const [sourceDocNo, setSourceDocNo] = useState('')
  const [source, setSource] = useState<SourceItem | null>(null)
  const [stagingLocationId, setStagingLocationId] = useState('')
  const [lines, setLines] = useState<ReceivingLineDraft[]>([])
  const [activeKey, setActiveKey] = useState('')
  const [reviewing, setReviewing] = useState(false)
  const photoUploads = useAttachmentUploads('RECEIPT')
  const photos = photoUploads.uploaded
  const [error, setError] = useState('')
  const [result, setResult] = useState<Receipt | null>(null)
  const [printJob, setPrintJob] = useState<PrintJob | null>(null)
  const { getKey, clearKey } = useStableIdempotencyKey()
  const staging = useQuery({
    queryKey: ['pda-staging-locations', warehouse.id],
    queryFn: () => apiListLocations(warehouse.id, { type: 'STAGING', status: 'ENABLED', page: 1, pageSize: 20 }),
  })

  useEffect(() => {
    const items = staging.data?.items ?? []
    if (items.length === 1) setStagingLocationId(items[0].id)
    else if (!items.some((item) => item.id === stagingLocationId)) setStagingLocationId('')
  }, [staging.data, stagingLocationId])

  const activeLine = lines.find((line) => line.key === activeKey) ?? null
  const parse = useMutation({
    mutationFn: (content: string) => apiParseScan({
      content,
      context: { inboundOrderId: document?.inboundOrderId, warehouseId: warehouse.id },
    }),
  })
  const receiptMutation = useMutation({
    mutationFn: () => {
      const body = toReceiptRequest({ warehouse, stagingLocationId, document, sourceDocType, sourceDocNo, source, lines, photos })
      const fingerprint = JSON.stringify(body)
      return apiCreateReceipt(body, getKey(`receipt:${fingerprint}`)).then((receipt) => ({ receipt, fingerprint }))
    },
    onSuccess: ({ receipt, fingerprint }) => {
      clearKey(`receipt:${fingerprint}`)
      setResult(receipt)
      setError('')
    },
    onError: (reason) => setError(parseApiError(reason).message),
  })
  const printMutation = useMutation({
    mutationFn: () => {
      const fingerprint = `receipt-print:${result!.id}`
      return apiPrintReceipt(result!.id, getKey(fingerprint)).then((job) => ({ job, fingerprint }))
    },
    onSuccess: ({ job, fingerprint }) => { clearKey(fingerprint); setPrintJob(job) },
    onError: (reason) => setError(parseApiError(reason).message),
  })

  const replaceLine = (next: ReceivingLineDraft) => {
    setLines((current) => current.map((line) => line.key === next.key ? next : line))
  }

  const addManualMaterial = (item: unknown | null) => {
    if (!item) return
    const material = item as MaterialItem
    const existing = lines.find((line) => !line.orderLineId && line.materialId === material.id)
    if (existing) {
      setActiveKey(existing.key)
      return
    }
    const next = fromMaterial(material)
    setLines((current) => [...current, next])
    setActiveKey(next.key)
  }

  const handleScan = async (content: string) => {
    setError('')
    try {
      const scan = await parse.mutateAsync(content)
      const blocking = scan.warnings.find((warning) => warning.blocking)
      if (blocking) throw new Error(blocking.message)
      if (scan.document) {
        if (scan.document.warehouseId !== warehouse.id) throw new Error(`单据所属仓库为 ${scan.document.warehouseCode}，请切换仓库`)
        const next = await Promise.all(scan.document.lines.map(async (line) => {
          const matches = await apiQuickSearchMaterials(line.materialCode, 10)
          const material = matches.items.find((item) => item.id === line.materialId)
          return fromDocumentLine(line, material)
        }))
        setDocument(scan.document)
        setSourceDocType(scan.document.docType)
        setLines(next)
        setActiveKey(next[0]?.key ?? '')
        return
      }
      if (!scan.material) throw new Error(scan.message ?? '未识别，请使用手工选择物料')
      const matched = findScanTarget(lines, activeKey, scan)
      const base = matched ?? fromScanMaterial(scan.material)
      const next = applyScan(base, scan)
      setLines((current) => matched
        ? current.map((line) => line.key === matched.key ? next : line)
        : [...current, next])
      setActiveKey(next.key)
    } catch (reason) {
      setError(parseApiError(reason).message)
    }
  }

  const reset = () => {
    setDocument(null)
    setSourceDocType(null)
    setSourceDocNo('')
    setSource(null)
    setLines([])
    setActiveKey('')
    setReviewing(false)
    photoUploads.clear()
    setResult(null)
    setPrintJob(null)
    setError('')
  }

  if (result) {
    return (
      <ResultPanel
        title="收货成功"
        subtitle={`${result.receiptNo} · ${qtyText(result.lines.reduce((sum, line) => sum + Number(line.actualQty), 0).toFixed(4))} 件进入待检`}
        job={printJob}
        primaryLabel="打印收货回执"
        onPrimary={() => printMutation.mutate()}
        onNext={reset}
      />
    )
  }

  if (!document && !sourceDocType) {
    return (
      <div className="space-y-4">
        <ScanInput placeholder="扫单据二维码" onSubmit={handleScan} disabled={parse.isPending} />
        <div className="grid gap-3">
          <Button className="h-14 justify-start text-base" variant="outline" onClick={() => setSourceDocType('PR')}>生产入库（PR）</Button>
          <Button className="h-14 justify-start text-base" variant="outline" onClick={() => setSourceDocType('OT')}>其他入库（OT）</Button>
        </div>
        <p className="text-sm text-muted-foreground">采购入库必须扫描预建单</p>
        {error && <PdaError message={error} />}
      </div>
    )
  }

  if (reviewing) {
    return (
      <div className="space-y-4" data-testid="receiving-confirmation">
        <div className="rounded-lg border bg-card p-3">
          <p className="font-semibold">确认收货</p>
          <p className="text-sm text-muted-foreground">{document?.docNo ?? `${sourceDocType} 无单收货`} · {warehouse.code}</p>
          <p className="text-sm text-muted-foreground">暂存 {staging.data?.items.find((item) => item.id === stagingLocationId)?.code ?? '未选择'}</p>
        </div>
        <div className="space-y-2">
          {lines.map((line) => (
            <div key={line.key} className="rounded-lg border bg-card p-3 text-sm">
              <p className="font-medium">{line.materialCode} {line.materialName}</p>
              <p>{qtyText(line.quantity)} · {line.batchId ? '复用内部批次' : batchSummary(line.batchProps)}</p>
              {line.uniqueCodes.length > 0 && <p className="text-muted-foreground">唯一码 {line.uniqueCodes.length} 个</p>}
            </div>
          ))}
        </div>
        <AttachmentUpload uploads={photoUploads} />
        {error && <PdaError message={error} />}
        <div className="grid grid-cols-2 gap-2">
          <Button variant="outline" onClick={() => setReviewing(false)}>继续添加</Button>
          <Button disabled={receiptMutation.isPending || photoUploads.busy || photoUploads.hasFailures} onClick={() => receiptMutation.mutate()} data-testid="submit-receipt">确认提交</Button>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <div className="rounded-lg border bg-card p-3">
        <p className="text-sm font-medium">{document?.docNo ?? `${sourceDocType} 无单新建`}</p>
        <p className="text-xs text-muted-foreground">{warehouse.code}</p>
      </div>
      <label className="grid gap-1 text-sm">
        暂存库位
        <select
          className="h-10 rounded-lg border border-input bg-background px-2"
          value={stagingLocationId}
          onChange={(event) => setStagingLocationId(event.target.value)}
          aria-label="暂存库位"
        >
          <option value="">请选择</option>
          {(staging.data?.items ?? []).map((item) => <option key={item.id} value={item.id}>{item.code}</option>)}
        </select>
      </label>
      {!document && (
        <div className="space-y-2 rounded-lg border bg-card p-3">
          <label className="grid gap-1 text-sm">来源单号（可选）<Input value={sourceDocNo} onChange={(event) => setSourceDocNo(event.target.value)} /></label>
          <label className="grid gap-1 text-sm">
            {sourceDocType === 'PR' ? '生产车间' : '来源（可选）'}
            <ReferencePicker
              resource="sources"
              value={source?.id ?? null}
              onChange={() => undefined}
              onSelectItem={(item) => setSource(item as SourceItem | null)}
              query={{ status: 'ENABLED', ...(sourceDocType === 'PR' ? { type: 'WORKSHOP' } : {}), page: 1, pageSize: 10 }}
              placeholder="搜索来源"
            />
          </label>
        </div>
      )}
      <ScanInput placeholder="扫描物料、唯一码、批次或外部条码" onSubmit={handleScan} disabled={parse.isPending} />
      {!document && (
        <div className="space-y-1">
          <p className="text-sm font-medium">手工选择物料</p>
          <ReferencePicker
            resource="materials"
            value={null}
            onChange={() => undefined}
            onSelectItem={addManualMaterial}
            query={{ status: 'ENABLED', page: 1, pageSize: 10 }}
            placeholder="物料编码/名称/搜索码"
          />
        </div>
      )}
      {lines.length > 0 && (
        <label className="grid gap-1 text-sm">
          收货行
          <select className="h-10 rounded-lg border border-input bg-background px-2" value={activeKey} onChange={(event) => setActiveKey(event.target.value)} aria-label="单据行">
            {lines.map((line, index) => <option key={line.key} value={line.key}>{index + 1}. {line.materialCode} {line.materialName} · {qtyText(line.quantity)}</option>)}
          </select>
        </label>
      )}
      {activeLine ? (
        <LineEditor
          line={activeLine}
          sourceDocType={sourceDocType}
          onChange={replaceLine}
          onRemove={!activeLine.orderLineId ? () => {
            setLines((current) => current.filter((line) => line.key !== activeLine.key))
            setActiveKey(lines.find((line) => line.key !== activeLine.key)?.key ?? '')
          } : undefined}
        />
      ) : <p className="rounded-lg border bg-card p-4 text-center text-sm text-muted-foreground">请扫描或手工选择物料</p>}
      {!document && (
        <Button variant="outline" className="w-full" onClick={() => setActiveKey('')}>
          <Plus className="size-4" data-icon />继续添加另一物料
        </Button>
      )}
      {error && <PdaError message={error} />}
      <Button
        className="fixed right-3 bottom-3 left-3 mx-auto h-12 max-w-md text-base"
        disabled={lines.length === 0 || !stagingLocationId}
        onClick={() => {
          try {
            toReceiptRequest({ warehouse, stagingLocationId, document, sourceDocType, sourceDocNo, source, lines, photos })
            setError('')
            setReviewing(true)
          } catch (reason) {
            setError(parseApiError(reason).message)
          }
        }}
        data-testid="review-receipt"
      >
        下一步确认
      </Button>
    </div>
  )
}

function LineEditor({
  line, sourceDocType, onChange, onRemove,
}: {
  line: ReceivingLineDraft
  sourceDocType: InboundOrderType | null
  onChange: (line: ReceivingLineDraft) => void
  onRemove?: () => void
}) {
  const updateProps = (patch: Partial<BatchProps>) => onChange({
    ...line,
    batchId: null,
    batchProps: { ...(line.batchProps ?? {}), ...patch },
  })
  return (
    <div className="space-y-3 rounded-lg border bg-card p-3">
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="font-semibold">{line.materialCode} {line.materialName}</p>
          {line.expectedQty && <p className="text-sm text-muted-foreground">应到 {qtyText(line.expectedQty)}</p>}
        </div>
        {onRemove && <Button size="icon-sm" variant="ghost" aria-label="删除行" onClick={onRemove}><Trash2 className="size-4" data-icon /></Button>}
      </div>
      <label className="grid gap-1 text-sm">
        实收数量
        <Input value={line.quantity} onChange={(event) => onChange({ ...line, quantity: event.target.value })} inputMode="decimal" data-testid="receiving-qty" />
      </label>
      {line.uniqueCodes.length > 0 && (
        <div className="text-sm">
          <p>唯一码登记数量合计 {qtyText(sumUnique(line.uniqueCodes))}</p>
          <p className="break-all text-xs text-muted-foreground">{line.uniqueCodes.map((item) => `${item.code} (${qtyText(item.quantity)})`).join('、')}</p>
        </div>
      )}
      {line.batchControlled && (
        <div className="space-y-2 border-t pt-3">
          <p className="text-sm font-medium">批次信息</p>
          {sourceDocType === 'PR' && (
            <ReferencePicker
              resource="batches"
              value={line.batchId}
              onChange={(batchId) => onChange({ ...line, batchId, batchProps: batchId ? null : line.batchProps })}
              onSelectItem={(item) => {
                const batch = item as BatchItem | null
                if (batch) onChange({ ...line, batchId: batch.id, batchProps: null })
              }}
              query={{ materialId: line.materialId, status: 'ACTIVE', page: 1, pageSize: 10 }}
              placeholder="生产退料可复用内部批次"
            />
          )}
          {!line.batchId && (
            <div className="grid grid-cols-2 gap-2">
              <label className="grid gap-1 text-xs">来源批号<Input value={line.batchProps?.sourceBatchNo ?? ''} onChange={(event) => updateProps({ sourceBatchNo: event.target.value || null })} /></label>
              <label className="grid gap-1 text-xs">生产日期<Input type="date" value={line.batchProps?.productionDate ?? ''} onChange={(event) => updateProps({ productionDate: event.target.value || null })} /></label>
              <label className="col-span-2 grid gap-1 text-xs">失效日期<Input type="date" value={line.batchProps?.expiryDate ?? ''} onChange={(event) => updateProps({ expiryDate: event.target.value || null })} /></label>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function QualityPage({ warehouse }: { warehouse: WarehouseContext }) {
  const queryClient = useQueryClient()
  const [selected, setSelected] = useState<QualityTodo | null>(null)
  const [candidates, setCandidates] = useState<QualityTodo[]>([])
  const [error, setError] = useState('')
  const [exceptionMode, setExceptionMode] = useState(false)
  const [reason, setReason] = useState<QualityExceptionReason>('DAMAGED')
  const [note, setNote] = useState('')
  const photoUploads = useAttachmentUploads('EXCEPTION')
  const photos = photoUploads.uploaded
  const [done, setDone] = useState('')
  const { getKey, clearKey } = useStableIdempotencyKey()
  const queryKey = ['pda-quality-todos', warehouse.id]
  const todos = useQuery({ queryKey, queryFn: () => apiListQualityTodos({ warehouseId: warehouse.id, page: 1, pageSize: 50 }) })
  const parse = useMutation({ mutationFn: (content: string) => apiParseScan({ content, context: { warehouseId: warehouse.id } }) })
  const qualitySubmission = () => {
    const body = exceptionMode
      ? { result: 'EXCEPTION' as const, checkedQty: selected!.quantity, exceptionReason: reason, note: note.trim() || null, photoIds: photos.map((item) => item.id) }
      : { result: 'PASS' as const, checkedQty: selected!.quantity }
    return { body, fingerprint: `quality:${selected!.receiptLineId}:${JSON.stringify(body)}` }
  }
  const resetDraft = () => {
    setExceptionMode(false)
    setReason('DAMAGED')
    setNote('')
  }
  const submit = useMutation({
    mutationFn: () => {
      const { body, fingerprint } = qualitySubmission()
      return apiSubmitQualityCheck(selected!.receiptLineId, body, getKey(fingerprint)).then(() => fingerprint)
    },
    onSuccess: (fingerprint) => {
      clearKey(fingerprint)
      setDone(exceptionMode ? '异常已上报' : '质检通过')
      setSelected(null)
      resetDraft()
      photoUploads.clear()
      void queryClient.invalidateQueries({ queryKey })
    },
    onError: (reason) => {
      const apiError = parseApiError(reason)
      setError(apiError.message)
      if (apiError.code === 'QC_STATUS_INVALID') {
        const invalidTask = selected
        if (invalidTask) {
          const { fingerprint } = qualitySubmission()
          clearKey(fingerprint)
          photoUploads.discard({ id: invalidTask.receiptLineId, label: invalidTask.receiptNo })
        }
        setSelected(null)
        resetDraft()
        void queryClient.invalidateQueries({ queryKey })
      }
    },
  })
  const locate = async (content: string) => {
    setError('')
    setDone('')
    try {
      const scan = await parse.mutateAsync(content)
      if (!scan.batch || !scan.material) throw new Error('未找到待质检任务')
      const list = await apiListQualityTodos({ warehouseId: warehouse.id, materialId: scan.material.materialId, batchId: scan.batch.batchId, page: 1, pageSize: 20 })
      if (list.items.length === 1) { setSelected(list.items[0]); setCandidates([]) }
      else if (list.items.length > 1) { setCandidates(list.items); setSelected(null) }
      else throw new Error('未找到待质检任务')
    } catch (reason) {
      setError(parseApiError(reason).message)
    }
  }

  if (selected) {
    return (
      <div className="space-y-4">
        <TodoCard title={selected.receiptNo} material={`${selected.materialCode} ${selected.materialName}`} batch={selected.batchNo} quantity={selected.quantity} />
        {!exceptionMode ? (
          <div className="grid gap-3">
            <Button className="h-14 text-base" onClick={() => submit.mutate()} disabled={submit.isPending} data-testid="quality-pass">
              <ClipboardCheck className="size-4" data-icon />PASS 通过
            </Button>
            <Button className="h-14 text-base" variant="outline" onClick={() => setExceptionMode(true)}>上报异常</Button>
          </div>
        ) : (
          <div className="space-y-3">
            <select className="h-11 w-full rounded-lg border border-input bg-background px-2" value={reason} onChange={(event) => setReason(event.target.value as QualityExceptionReason)}>
              <option value="DAMAGED">破损</option><option value="QTY_MISMATCH">数量不符</option><option value="OTHER">其他</option>
            </select>
            <Input className="h-11" value={note} onChange={(event) => setNote(event.target.value)} placeholder="备注" />
            <AttachmentUpload uploads={photoUploads} />
            <Button className="h-12 w-full text-base" disabled={photos.length === 0 || submit.isPending || photoUploads.busy || photoUploads.hasFailures} onClick={() => submit.mutate()} data-testid="quality-exception-submit">提交异常</Button>
          </div>
        )}
        {error && <PdaError message={error} />}
        <AttachmentCleanupNotice uploads={photoUploads} />
      </div>
    )
  }
  return (
    <div className="space-y-4">
      <ScanInput placeholder="扫描批次标签" onSubmit={(value) => void locate(value)} disabled={parse.isPending} />
      {done && <PdaSuccess message={done} />}
      {error && <PdaError message={error} />}
      <AttachmentCleanupNotice uploads={photoUploads} />
      {candidates.length > 0 && <CandidateList items={candidates} onPick={setSelected} />}
      <TodoList loading={todos.isLoading} items={todos.data?.items ?? []} empty="当前仓库无待质检任务" onPick={setSelected} />
    </div>
  )
}

function PutawayPage({ warehouse }: { warehouse: WarehouseContext }) {
  const queryClient = useQueryClient()
  const [selected, setSelected] = useState<PutawayTodo | null>(null)
  const [candidates, setCandidates] = useState<PutawayTodo[]>([])
  const [selectedLocationId, setSelectedLocationId] = useState('')
  const [scannedLocationCode, setScannedLocationCode] = useState('')
  const [qtyPerLabel, setQtyPerLabel] = useState('')
  const [printJob, setPrintJob] = useState<PrintJob | null>(null)
  const [done, setDone] = useState('')
  const [error, setError] = useState('')
  const [confirmMismatch, setConfirmMismatch] = useState(false)
  const { getKey, clearKey } = useStableIdempotencyKey()
  const todoKey = ['pda-putaway-todos', warehouse.id]
  const todos = useQuery({ queryKey: todoKey, queryFn: () => apiListPutawayTodos({ warehouseId: warehouse.id, page: 1, pageSize: 50 }) })
  const recommendations = useQuery({
    queryKey: ['putaway-recommendations', selected?.receiptLineId, selected?.inventoryVersion],
    queryFn: () => apiGetPutawayRecommendations(selected!.receiptLineId),
    enabled: !!selected,
  })
  const parse = useMutation({ mutationFn: (content: string) => apiParseScan({ content, context: { warehouseId: warehouse.id } }) })
  const print = useMutation({
    mutationFn: () => {
      const body = { receiptLineId: selected!.receiptLineId, qtyPerLabel }
      const fingerprint = `batch-print:${JSON.stringify(body)}`
      return apiPrintBatchLabels(body, getKey(fingerprint)).then((job) => ({ job, fingerprint }))
    },
    onSuccess: ({ job, fingerprint }) => { clearKey(fingerprint); setPrintJob(job) },
    onError: (reason) => setError(parseApiError(reason).message),
  })
  const submit = useMutation({
    mutationFn: () => {
      const body = {
        receiptLineId: selected!.receiptLineId,
        toLocationId: selectedLocationId,
        scannedLocationCode,
        expectedInventoryVersion: selected!.inventoryVersion,
      }
      const fingerprint = `putaway:${JSON.stringify(body)}`
      return apiCreatePutawayRecord(body, getKey(fingerprint)).then(() => fingerprint)
    },
    onSuccess: (fingerprint) => {
      clearKey(fingerprint)
      setDone('上架完成')
      setSelected(null)
      setScannedLocationCode('')
      setPrintJob(null)
      void queryClient.invalidateQueries({ queryKey: todoKey })
    },
    onError: async (reason) => {
      const apiError = parseApiError(reason)
      setError(apiError.message)
      if (apiError.code === 'VERSION_CONFLICT' && selected) {
        const refreshed = await todos.refetch()
        const fresh = refreshed.data?.items.find((item) => item.receiptLineId === selected.receiptLineId)
        if (fresh) setSelected(fresh)
        setSelectedLocationId('')
        setScannedLocationCode('')
        setConfirmMismatch(false)
        await recommendations.refetch()
      }
    },
  })
  const activeRecommendation = useMemo(
    () => recommendations.data?.find((item) => item.locationId === selectedLocationId) ?? recommendations.data?.[0],
    [recommendations.data, selectedLocationId],
  )

  useEffect(() => {
    if (selected && recommendations.data?.[0] && !selectedLocationId) {
      setSelectedLocationId(recommendations.data[0].locationId)
      setQtyPerLabel(selected.defaultQtyPerLabel ?? selected.quantity)
    }
  }, [recommendations.data, selected, selectedLocationId])

  const pickPutaway = (todo: PutawayTodo) => {
    setSelected(todo)
    setCandidates([])
    setSelectedLocationId('')
    setQtyPerLabel(todo.defaultQtyPerLabel ?? todo.quantity)
    setPrintJob(null)
    setError('')
    setConfirmMismatch(false)
  }
  const locate = async (content: string) => {
    setError('')
    setDone('')
    try {
      const scan = await parse.mutateAsync(content)
      if (!scan.batch || !scan.material) throw new Error('未找到待上架任务')
      const list = await apiListPutawayTodos({ warehouseId: warehouse.id, materialId: scan.material.materialId, batchId: scan.batch.batchId, page: 1, pageSize: 20 })
      if (list.items.length === 1) pickPutaway(list.items[0])
      else if (list.items.length > 1) { setCandidates(list.items); setSelected(null) }
      else throw new Error('未找到待上架任务')
    } catch (reason) {
      setError(parseApiError(reason).message)
    }
  }
  const scanLocation = (code: string) => {
    const recommendation = recommendations.data?.find((item) => item.locationCode === code)
    setScannedLocationCode(code)
    if (recommendation) {
      setSelectedLocationId(recommendation.locationId)
      setConfirmMismatch(false)
    } else {
      setConfirmMismatch(true)
    }
  }

  if (selected) {
    return (
      <div className="space-y-4">
        <TodoCard title={selected.receiptNo} material={`${selected.materialCode} ${selected.materialName}`} batch={selected.batchNo} quantity={selected.quantity} />
        <div className="space-y-2">
          <p className="text-sm font-medium">推荐库位</p>
          {recommendations.isLoading ? <Skeleton className="h-12 w-full" /> : recommendations.data?.map((item) => (
            <button key={item.locationId} type="button" className={`flex min-h-12 w-full items-center justify-between rounded-lg border px-3 text-left ${selectedLocationId === item.locationId ? 'border-primary bg-primary/5' : 'bg-background'}`} onClick={() => setSelectedLocationId(item.locationId)}>
              <span className="font-medium">{item.locationCode}</span><span className="text-sm text-muted-foreground">{item.reason}</span>
            </button>
          ))}
        </div>
        <div className="space-y-2 rounded-lg border bg-card p-3">
          <label className="grid gap-1 text-sm">每标签数量<Input className="h-11 tabular-nums" value={qtyPerLabel} onChange={(event) => setQtyPerLabel(event.target.value)} /></label>
          <p className="text-sm text-muted-foreground">将生成 {Math.ceil(num(selected.quantity) / Math.max(num(qtyPerLabel), 1))} 张</p>
          <Button className="w-full" variant="outline" disabled={print.isPending} onClick={() => print.mutate()}><Printer className="size-4" data-icon />预览标签二维码</Button>
          {printJob && <PrintPreview job={printJob} />}
        </div>
        <ScanInput placeholder="扫库位码确认" onSubmit={scanLocation} />
        {scannedLocationCode && <p className="rounded-lg border bg-card p-3 text-sm">已扫库位：<span className="font-medium">{scannedLocationCode}</span></p>}
        {confirmMismatch && (
          <div className="rounded-lg border border-warning/40 bg-warning/10 p-3 text-sm">
            <p>扫描库位不在推荐列表。请确认改选有效推荐库位后重新扫描，或取消本次操作。</p>
            <Button className="mt-2" size="sm" variant="outline" onClick={() => { setScannedLocationCode(''); setConfirmMismatch(false) }}>取消</Button>
          </div>
        )}
        {error && <PdaError message={error} />}
        <Button className="fixed right-3 bottom-3 left-3 mx-auto h-12 max-w-md text-base" disabled={!selectedLocationId || !scannedLocationCode || confirmMismatch || submit.isPending || scannedLocationCode !== activeRecommendation?.locationCode} onClick={() => submit.mutate()} data-testid="submit-putaway">完成上架</Button>
      </div>
    )
  }
  return (
    <div className="space-y-4">
      <ScanInput placeholder="扫描批次标签" onSubmit={(value) => void locate(value)} disabled={parse.isPending} />
      {done && <PdaSuccess message={done} />}
      {error && <PdaError message={error} />}
      {candidates.length > 0 && <PutawayCandidateList items={candidates} onPick={pickPutaway} />}
      <PutawayTodoList loading={todos.isLoading} items={todos.data?.items ?? []} empty="当前仓库无待上架任务" onPick={pickPutaway} />
    </div>
  )
}

function AttachmentUpload({ uploads }: { uploads: ReturnType<typeof useAttachmentUploads> }) {
  return (
    <div className="space-y-2 rounded-lg border bg-card p-3">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium">照片 {uploads.entries.length}/{uploads.maxCount}</span>
        <label className="inline-flex h-9 cursor-pointer items-center gap-1 rounded-lg border px-3 text-sm">
          <Camera className="size-4" data-icon />拍照
          <input
            type="file"
            accept="image/*"
            className="sr-only"
            disabled={uploads.entries.length >= uploads.maxCount}
            onChange={(event) => {
              const file = event.currentTarget.files?.[0]
              event.currentTarget.value = ''
              if (file) uploads.add(file)
            }}
          />
        </label>
      </div>
      {uploads.entries.length > 0 && (
        <div className="grid grid-cols-3 gap-2">
          {uploads.entries.map((entry) => (
            <div key={entry.id} className="relative min-w-0 overflow-hidden rounded border bg-muted/30" data-testid={`attachment-${entry.status}`}>
              {entry.attachment ? (
                <ProtectedImagePreview
                  thumbnailPath={entry.attachment.thumbnailUrl}
                  originalPath={entry.attachment.url}
                  alt={entry.file.name}
                  className="aspect-square size-full object-cover"
                />
              ) : (
                <div className="grid aspect-square place-items-center p-2 text-center text-xs">
                  <span className="break-all">{entry.file.name}</span>
                  <span className={entry.status === 'upload-failed' ? 'text-destructive' : 'text-muted-foreground'}>
                    {entry.status === 'upload-failed' ? entry.error : '上传中'}
                  </span>
                </div>
              )}
              <div className="absolute right-1 bottom-1 flex gap-1">
                {entry.status === 'upload-failed' && (
                  <Button type="button" size="icon-xs" variant="secondary" aria-label={`重传 ${entry.file.name}`} onClick={() => uploads.retry(entry.id)}>
                    <RotateCw className="size-3" data-icon />
                  </Button>
                )}
                {entry.status !== 'uploading' && entry.status !== 'deleting' && (
                  <Button type="button" size="icon-xs" variant="secondary" aria-label={`删除 ${entry.file.name}`} onClick={() => void uploads.remove(entry.id)}>
                    <Trash2 className="size-3" data-icon />
                  </Button>
                )}
              </div>
              {entry.status === 'deleting' && <p className="absolute inset-x-0 bottom-0 bg-background/90 p-1 text-center text-xs">删除中</p>}
              {entry.status === 'delete-failed' && <p className="absolute inset-x-0 top-0 bg-destructive/90 p-1 text-xs text-destructive-foreground">{entry.error}</p>}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function AttachmentCleanupNotice({ uploads }: { uploads: ReturnType<typeof useAttachmentUploads> }) {
  if (uploads.cleanupEntries.length === 0) return null
  return (
    <div className="space-y-2 rounded-lg border border-warning/40 bg-warning/10 p-3" data-testid="attachment-cleanup">
      <p className="text-sm font-medium">失效任务照片清理</p>
      {uploads.cleanupEntries.map((entry) => (
        <div key={entry.id} className="flex items-center justify-between gap-2 text-sm">
          <div className="min-w-0">
            <p className="truncate">{entry.taskLabel} · {entry.file.name}</p>
            <p className={entry.status === 'delete-failed' ? 'text-destructive' : 'text-muted-foreground'}>
              {entry.status === 'delete-failed' ? entry.error : '删除中'}
            </p>
          </div>
          {entry.status === 'delete-failed' && (
            <Button type="button" size="sm" variant="outline" onClick={() => uploads.retryCleanup(entry.id)}>
              <RotateCw className="size-3" data-icon />重试清理
            </Button>
          )}
        </div>
      ))}
    </div>
  )
}

function TodoCard({ title, material, batch, quantity }: { title: string; material: string; batch: string; quantity: string }) {
  return <div><p className="text-sm text-muted-foreground">{title}</p><p className="mt-1 text-lg font-semibold">{material}</p><p className="text-sm tabular-nums">批次 {batch} · {qtyText(quantity)} 件</p></div>
}

function TodoList({ loading, items, empty, onPick }: { loading: boolean; items: QualityTodo[]; empty: string; onPick: (item: QualityTodo) => void }) {
  if (loading) return <Skeleton className="h-40 w-full" />
  if (items.length === 0) return <p className="rounded-lg border bg-card p-4 text-center text-sm text-muted-foreground">{empty}</p>
  return <div className="space-y-2">{items.map((item) => <button key={item.receiptLineId} type="button" className="w-full rounded-lg border bg-card p-3 text-left" onClick={() => onPick(item)}><TodoCard title={item.receiptNo} material={`${item.materialCode} ${item.materialName}`} batch={item.batchNo} quantity={item.quantity} /></button>)}</div>
}

function CandidateList({ items, onPick }: { items: QualityTodo[]; onPick: (item: QualityTodo) => void }) {
  return <div className="space-y-2"><p className="text-sm font-medium">选择待质检任务</p>{items.map((item) => <button key={item.receiptLineId} type="button" className="w-full rounded-lg border bg-card p-3 text-left text-sm" onClick={() => onPick(item)}>{item.receiptNo} · {qtyText(item.quantity)} · {item.receivedAt.slice(0, 16).replace('T', ' ')}</button>)}</div>
}

function PutawayTodoList({ loading, items, empty, onPick }: { loading: boolean; items: PutawayTodo[]; empty: string; onPick: (item: PutawayTodo) => void }) {
  if (loading) return <Skeleton className="h-40 w-full" />
  if (items.length === 0) return <p className="rounded-lg border bg-card p-4 text-center text-sm text-muted-foreground">{empty}</p>
  return <div className="space-y-2">{items.map((item) => <button key={item.receiptLineId} type="button" className="w-full rounded-lg border bg-card p-3 text-left" onClick={() => onPick(item)}><TodoCard title={item.receiptNo} material={`${item.materialCode} ${item.materialName}`} batch={item.batchNo} quantity={item.quantity} /><p className="mt-1 text-sm text-muted-foreground">暂存 {item.fromLocationCode}</p></button>)}</div>
}

function PutawayCandidateList({ items, onPick }: { items: PutawayTodo[]; onPick: (item: PutawayTodo) => void }) {
  return <div className="space-y-2"><p className="text-sm font-medium">选择待上架任务</p>{items.map((item) => <button key={item.receiptLineId} type="button" className="w-full rounded-lg border bg-card p-3 text-left text-sm" onClick={() => onPick(item)}>{item.receiptNo} · {qtyText(item.quantity)} · 暂存 {item.fromLocationCode}</button>)}</div>
}

function PdaError({ message }: { message: string }) {
  return <div className="flex items-center gap-2 rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive"><AlertTriangle className="size-4" data-icon />{message}</div>
}

function PdaSuccess({ message }: { message: string }) {
  return <div className="flex items-center gap-2 rounded-lg border border-success/40 bg-success/10 p-3 text-sm text-success"><CheckCircle2 className="size-4" data-icon />{message}</div>
}

function ResultPanel({ title, subtitle, job, primaryLabel, onPrimary, onNext }: { title: string; subtitle: string; job: PrintJob | null; primaryLabel: string; onPrimary: () => void; onNext: () => void }) {
  return (
    <div className="space-y-4 text-center">
      <div className="rounded-lg border bg-success/10 p-6 text-success"><CheckCircle2 className="mx-auto mb-2 size-10" data-icon /><p className="text-xl font-semibold">{title}</p><p className="mt-1 text-sm">{subtitle}</p></div>
      {job && <PrintPreview job={job} />}
      <Button className="h-12 w-full text-base" onClick={onPrimary}><Printer className="size-4" data-icon />{primaryLabel}</Button>
      <Button className="h-12 w-full text-base" variant="outline" onClick={onNext}>继续下一单</Button>
    </div>
  )
}

function PrintPreview({ job }: { job: PrintJob }) {
  return (
    <div className="space-y-2 rounded-lg border bg-card p-3 text-left">
      <div className="flex items-center justify-between"><span className="text-sm font-medium">打印内容</span>{statusBadge(job.status)}</div>
      {job.status === 'FAILED' && <p className="text-sm text-destructive">{job.errorCode}</p>}
      <PrintJobItems items={job.items} limit={3} />
    </div>
  )
}

function toWarehouseContext(item: WarehouseItem): WarehouseContext {
  return { id: item.id, code: item.code, name: item.name }
}

function fromDocumentLine(line: ScanDocument['lines'][number], material?: MaterialItem): ReceivingLineDraft {
  return {
    key: line.orderLineId,
    orderLineId: line.orderLineId,
    materialId: line.materialId,
    materialCode: line.materialCode,
    materialName: line.materialName,
    expectedQty: line.expectedQty,
    batchControlled: material?.batchControlled ?? false,
    labelType: material?.labelType ?? (line.uniqueCodes.length > 0 ? 'UNIQUE' : 'NONE'),
    quantity: material?.labelType === 'UNIQUE' || line.uniqueCodes.length > 0 ? '0.0000' : line.remainingQty,
    uniqueCodes: [],
    batchId: null,
    batchProps: null,
  }
}

function fromMaterial(material: MaterialItem): ReceivingLineDraft {
  return {
    key: crypto.randomUUID(), orderLineId: null, materialId: material.id, materialCode: material.code,
    materialName: material.name, expectedQty: null, batchControlled: material.batchControlled,
    labelType: material.labelType, quantity: '0.0000', uniqueCodes: [], batchId: null, batchProps: null,
  }
}

function fromScanMaterial(material: ScanMaterial): ReceivingLineDraft {
  return {
    key: crypto.randomUUID(), orderLineId: null, materialId: material.materialId,
    materialCode: material.materialCode, materialName: material.materialName, expectedQty: null,
    batchControlled: material.batchControlled, labelType: material.labelType, quantity: '0.0000',
    uniqueCodes: [], batchId: null, batchProps: null,
  }
}

function findScanTarget(lines: ReceivingLineDraft[], activeKey: string, scan: ScanResult) {
  const active = lines.find((line) => line.key === activeKey)
  if (active?.materialId === scan.material?.materialId) return active
  const matches = lines.filter((line) => line.materialId === scan.material?.materialId)
  if (matches.length > 1) throw new Error('同物料存在多行，请先选择单据行')
  return matches[0]
}

function applyScan(line: ReceivingLineDraft, scan: ScanResult): ReceivingLineDraft {
  const material = scan.material!
  if (scan.uniqueCode) {
    if (line.uniqueCodes.some((item) => item.code === scan.uniqueCode!.code)) throw new Error('已收过')
    const uniqueCodes = [...line.uniqueCodes, { code: scan.uniqueCode.code, quantity: scan.uniqueCode.quantity }]
    return { ...line, batchControlled: material.batchControlled, labelType: material.labelType, uniqueCodes, quantity: sumUnique(uniqueCodes), batchId: scan.batch?.batchId ?? line.batchId, batchProps: scan.batchProps ?? line.batchProps }
  }
  const scannedQty = scan.quantity ?? material.defaultQtyPerLabel ?? '1.0000'
  return {
    ...line,
    batchControlled: material.batchControlled,
    labelType: material.labelType,
    quantity: dec(num(line.quantity) + num(scannedQty)),
    batchId: scan.batch?.batchId ?? line.batchId,
    batchProps: scan.batchProps ?? line.batchProps,
  }
}

function toReceiptRequest({
  warehouse, stagingLocationId, document, sourceDocType, sourceDocNo, source, lines, photos,
}: {
  warehouse: WarehouseContext
  stagingLocationId: string
  document: ScanDocument | null
  sourceDocType: InboundOrderType | null
  sourceDocNo: string
  source: SourceItem | null
  lines: ReceivingLineDraft[]
  photos: AttachmentItem[]
}): ReceiptCreateRequest {
  if (!stagingLocationId) throw new Error('请选择暂存库位')
  if (!document && !sourceDocType) throw new Error('请选择收货类型')
  if (!document && sourceDocType === 'PR' && (!source || source.type !== 'WORKSHOP')) throw new Error('生产入库必须选择有效车间来源')
  const submittedLines = lines.filter((line) => num(line.quantity) > 0)
  if (submittedLines.length === 0) throw new Error('请至少录入一行实收数量大于 0 的物料')
  const requestLines = submittedLines.map((line) => {
    if (!line.materialId) throw new Error('请选择物料')
    if (line.labelType === 'UNIQUE') {
      if (line.uniqueCodes.length === 0) throw new Error(`${line.materialCode} 必须登记唯一码`)
      if (dec(num(line.quantity)) !== sumUnique(line.uniqueCodes)) throw new Error(`${line.materialCode} 的数量必须等于唯一码登记数量之和`)
    }
    const batchProps = compactBatchProps(line.batchProps)
    if (line.batchControlled && !line.batchId && !batchProps) throw new Error(`${line.materialCode} 必须扫描或手工录入真实批次信息`)
    return {
      orderLineId: line.orderLineId,
      materialId: line.materialId,
      batchId: line.batchId,
      batchProps: line.batchId ? null : batchProps,
      quantity: dec(num(line.quantity)),
      uniqueCodes: line.uniqueCodes.length > 0 ? line.uniqueCodes.map((item) => item.code) : undefined,
    }
  })
  return {
    warehouseId: warehouse.id,
    stagingLocationId,
    inboundOrderId: document?.inboundOrderId ?? null,
    ...(!document ? {
      sourceDocType: sourceDocType!,
      sourceDocNo: sourceDocNo.trim() || null,
      sourceType: source?.type ?? null,
      sourceCode: source?.code ?? null,
    } : {}),
    lines: requestLines,
    photos: photos.map((item) => item.id),
  }
}

function compactBatchProps(props: BatchProps | null): BatchProps | null {
  if (!props) return null
  const result = Object.fromEntries(Object.entries(props).filter(([, value]) => value !== '' && value !== null && value !== undefined)) as BatchProps
  return Object.keys(result).length > 0 ? result : null
}

function batchSummary(props: BatchProps | null) {
  if (!props) return '非批控'
  return props.sourceBatchNo || props.productionDate || props.expiryDate || '批次信息待补充'
}

function sumUnique(items: UniqueDraft[]) {
  return dec(items.reduce((sum, item) => sum + num(item.quantity), 0))
}

function num(value: string | null | undefined): number {
  return Number(value ?? 0)
}

function dec(value: number): string {
  return Number.isFinite(value) ? value.toFixed(4) : '0.0000'
}
