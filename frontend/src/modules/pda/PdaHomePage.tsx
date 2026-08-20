import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertTriangle, ArrowLeft, Camera, CheckCircle2, ClipboardCheck, Printer, ScanLine,
} from 'lucide-react'
import type { ReactNode } from 'react'
import { useEffect, useMemo, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import {
  apiCreatePutawayRecord, apiCreateReceipt, apiGetPutawayRecommendations, apiListPutawayTodos,
  apiListQualityTodos, apiParseScan, apiPrintBatchLabels, apiPrintReceipt, apiSubmitQualityCheck,
  apiUploadAttachment,
} from '@/api'
import type {
  BatchProps, InboundOrderType, PrintJob, PutawayTodo, QualityExceptionReason, QualityTodo,
  Receipt, ScanDocument, ScanMaterial, ScanResult,
} from '@/api/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/platform/auth/auth-context'
import { menuIcon } from '@/platform/menu-icons'
import { qtyText, statusBadge } from '@/modules/inbound/format'

const CURRENT_WAREHOUSE_ID = 'wh-01'
const CURRENT_WAREHOUSE_CODE = 'WH-01'
const STAGING_LOCATION_ID = 'loc-01'
const actionByEntry: Record<string, string> = {
  receiving: 'action.receiving.create',
  qc: 'action.quality.check',
  putaway: 'action.putaway.create',
}

export function PdaHomePage() {
  const { session, hasPerm } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const page = location.pathname.split('/')[2] ?? ''
  const entries = (session?.menus.pda ?? [])
    .filter((entry) => hasPerm('route.inbound') && hasPerm(actionByEntry[entry.code] ?? ''))
    .sort((a, b) => a.sort - b.sort)

  if (page === 'receiving') return <PdaShell title="收货"><ReceivingPage /></PdaShell>
  if (page === 'qc') return <PdaShell title="质检"><QualityPage /></PdaShell>
  if (page === 'putaway') return <PdaShell title="上架"><PutawayPage /></PdaShell>

  return (
    <div className="min-h-screen bg-background">
      <header className="sticky top-0 z-10 flex h-14 items-center justify-between bg-primary px-4 text-primary-foreground">
        <div>
          <p className="text-sm font-semibold">{session?.user.name ?? '作业员'}</p>
          <p className="text-xs opacity-85">{CURRENT_WAREHOUSE_CODE}</p>
        </div>
        <ScanLine className="size-5" data-icon />
      </header>
      <main className="mx-auto max-w-md space-y-4 p-4" data-testid="pda-menu">
        <div className="grid gap-3">
          {entries.map((entry) => {
            const Icon = menuIcon(entry.code)
            return (
              <button
                key={entry.code}
                type="button"
                className="flex min-h-16 items-center gap-3 rounded-lg border bg-card p-4 text-left shadow-sm active:translate-y-px"
                onClick={() => navigate(`/pda/${entry.code}`)}
              >
                <span className="flex size-12 items-center justify-center rounded-lg bg-primary/10 text-primary">
                  <Icon className="size-5" data-icon />
                </span>
                <span className="text-lg font-semibold">{entry.code === 'qc' ? '质检' : entry.code === 'putaway' ? '上架' : '收货'}</span>
              </button>
            )
          })}
        </div>
        <p className="text-center text-sm text-muted-foreground">待办：质检 · 上架</p>
      </main>
    </div>
  )
}

function PdaShell({ title, children }: { title: string; children: ReactNode }) {
  const navigate = useNavigate()
  return (
    <div className="min-h-screen bg-background">
      <header className="sticky top-0 z-10 flex h-14 items-center justify-between border-b bg-card px-3">
        <Button variant="ghost" size="icon-sm" aria-label="返回" onClick={() => navigate('/pda')}>
          <ArrowLeft className="size-4" data-icon />
        </Button>
        <div className="text-center">
          <p className="text-sm font-semibold">{title}</p>
          <p className="text-xs text-muted-foreground">{CURRENT_WAREHOUSE_CODE}</p>
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
        onChange={(e) => setValue(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') submit()
        }}
        data-testid="pda-scan-input"
      />
      <Button className="h-11" disabled={disabled} onClick={submit} data-testid="pda-scan-submit">
        <ScanLine className="size-4" data-icon />
      </Button>
    </div>
  )
}

interface ReceivingLineDraft {
  orderLineId: string | null
  materialId: string
  materialCode: string
  materialName: string
  expectedQty: string | null
  quantity: string
  uniqueCodes: string[]
  batchId: string | null
  batchProps: BatchProps | null
}

function ReceivingPage() {
  const [document, setDocument] = useState<ScanDocument | null>(null)
  const [sourceDocType, setSourceDocType] = useState<InboundOrderType | null>(null)
  const [line, setLine] = useState<ReceivingLineDraft | null>(null)
  const [photoIds, setPhotoIds] = useState<string[]>([])
  const [error, setError] = useState('')
  const [result, setResult] = useState<Receipt | null>(null)
  const [printJob, setPrintJob] = useState<PrintJob | null>(null)
  const parse = useMutation({ mutationFn: (content: string) => apiParseScan({ content, context: { inboundOrderId: document?.inboundOrderId, warehouseId: CURRENT_WAREHOUSE_ID } }) })
  const receiptMutation = useMutation({
    mutationFn: () => apiCreateReceipt(toReceiptRequest({ document, sourceDocType, line, photoIds }), crypto.randomUUID()),
    onSuccess: (receipt) => {
      setResult(receipt)
      setError('')
    },
    onError: (e) => setError((e as Error).message),
  })
  const photoMutation = useMutation({
    mutationFn: (file: File) => apiUploadAttachment(file, 'RECEIPT'),
    onSuccess: (att) => {
      setPhotoIds((prev) => [...prev, att.id].slice(0, 3))
      setError('')
    },
    onError: (e) => setError((e as Error).message),
  })
  const printMutation = useMutation({
    mutationFn: () => apiPrintReceipt(result!.id, crypto.randomUUID()),
    onSuccess: setPrintJob,
    onError: (e) => setError((e as Error).message),
  })

  const handleScan = async (content: string) => {
    setError('')
    try {
      const scan = await parse.mutateAsync(content)
      const blocking = scan.warnings.find((w) => w.blocking)
      if (blocking) {
        setError(blocking.message)
        return
      }
      if (scan.document) {
        setDocument(scan.document)
        setSourceDocType(scan.document.docType)
        setLine(fromDocumentLine(scan.document.lines[0], scan.material, scan))
        return
      }
      if (scan.type === 'UNIQUE_LABEL') {
        const docLine = document?.lines.find((item) => item.materialId === scan.material?.materialId)
        const base = docLine && line?.materialId !== docLine.materialId ? fromDocumentLine(docLine, scan.material, null) : line
        setLine(addUniqueScan(base, scan))
        return
      }
      if (scan.material) {
        setLine((prev) => addSkuScan(prev, scan))
        return
      }
      setError(scan.message ?? '未识别，请手动输入')
    } catch (e) {
      setError((e as Error).message)
    }
  }

  if (result) {
    return (
      <ResultPanel
        title="收货成功"
        subtitle={`${result.receiptNo} · ${qtyText(result.lines[0]?.actualQty)} 件进入待检`}
        job={printJob}
        primaryLabel="打印收货回执"
        onPrimary={() => printMutation.mutate()}
        onNext={() => {
          setDocument(null)
          setSourceDocType(null)
          setLine(null)
          setPhotoIds([])
          setResult(null)
          setPrintJob(null)
        }}
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
        <p className="text-sm text-muted-foreground">采购入库请扫描预建单</p>
        {error && <PdaError message={error} />}
      </div>
    )
  }

  const orderLines = document?.lines ?? []

  return (
    <div className="space-y-4">
      <div className="rounded-lg border bg-card p-3">
        <p className="text-sm font-medium">{document?.docNo ?? `${sourceDocType} 无单新建`}</p>
        <p className="text-xs text-muted-foreground">{CURRENT_WAREHOUSE_CODE} · 暂存 STG-01</p>
      </div>
      <ScanInput placeholder="扫描物料/唯一码/批次标签" onSubmit={handleScan} disabled={parse.isPending} />
      {document && orderLines.length > 0 && (
        <label className="grid gap-1 text-sm">
          单据行
          <select
            className="h-10 rounded-lg border border-input bg-background px-2"
            value={line?.orderLineId ?? ''}
            onChange={(e) => {
              const next = orderLines.find((item) => item.id === e.target.value)
              if (next) setLine(fromDocumentLine(next, null, null))
            }}
          >
            {orderLines.map((item) => <option key={item.id} value={item.id}>{item.lineNo}. {item.materialCode} {item.materialName} 应到 {qtyText(item.expectedQty)}</option>)}
          </select>
        </label>
      )}
      <div className="rounded-lg border bg-card p-3">
        {line ? (
          <div className="space-y-3">
            <div>
              <p className="text-base font-semibold">{line.materialCode} {line.materialName}</p>
              <p className="text-sm text-muted-foreground">批次 {line.batchId ? '已复用' : line.batchProps?.sourceBatchNo ?? '自动生成'} · 唯一码 {line.uniqueCodes.length} 个</p>
            </div>
            <div className="flex items-center justify-between">
              <Button className="size-12 text-lg" variant="outline" onClick={() => setLine({ ...line, quantity: dec(Math.max(num(line.quantity) - 1, 0)) })}>-</Button>
              <Input className="mx-3 h-12 text-center text-2xl font-semibold tabular-nums" value={line.quantity} onChange={(e) => setLine({ ...line, quantity: e.target.value })} data-testid="receiving-qty" />
              <Button className="size-12 text-lg" variant="outline" onClick={() => setLine({ ...line, quantity: dec(num(line.quantity) + 1) })}>+</Button>
            </div>
          </div>
        ) : (
          <p className="text-center text-sm text-muted-foreground">请扫描或选择物料</p>
        )}
      </div>
      <div className="rounded-lg border bg-card p-3">
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium">照片 {photoIds.length}/3</span>
          <label className="inline-flex h-9 cursor-pointer items-center gap-1 rounded-lg border px-3 text-sm">
            <Camera className="size-4" data-icon />
            拍照
            <input
              type="file"
              accept="image/*"
              className="sr-only"
              disabled={photoIds.length >= 3 || photoMutation.isPending}
              onChange={(e) => {
                const file = e.target.files?.[0]
                if (file) photoMutation.mutate(file)
              }}
            />
          </label>
        </div>
      </div>
      {error && <PdaError message={error} />}
      <Button className="fixed right-3 bottom-3 left-3 mx-auto h-12 max-w-md text-base" disabled={!line || receiptMutation.isPending || photoMutation.isPending} onClick={() => receiptMutation.mutate()} data-testid="submit-receipt">
        提交
      </Button>
    </div>
  )
}

function QualityPage() {
  const qc = useQueryClient()
  const [selected, setSelected] = useState<QualityTodo | null>(null)
  const [candidates, setCandidates] = useState<QualityTodo[]>([])
  const [error, setError] = useState('')
  const [exceptionMode, setExceptionMode] = useState(false)
  const [reason, setReason] = useState<QualityExceptionReason>('DAMAGED')
  const [note, setNote] = useState('')
  const [photoIds, setPhotoIds] = useState<string[]>([])
  const [done, setDone] = useState('')
  const todos = useQuery({ queryKey: ['pda-quality-todos'], queryFn: () => apiListQualityTodos({ warehouseId: CURRENT_WAREHOUSE_ID, page: 1, pageSize: 50 }) })
  const parse = useMutation({ mutationFn: (content: string) => apiParseScan({ content, context: { warehouseId: CURRENT_WAREHOUSE_ID } }) })
  const submit = useMutation({
    mutationFn: () => apiSubmitQualityCheck(selected!.receiptLineId, exceptionMode
      ? { result: 'EXCEPTION', checkedQty: selected!.quantity, exceptionReason: reason, note, photoIds }
      : { result: 'PASS', checkedQty: selected!.quantity }, crypto.randomUUID()),
    onSuccess: () => {
      setDone(exceptionMode ? '异常已上报' : '质检通过')
      setSelected(null)
      setExceptionMode(false)
      setPhotoIds([])
      void qc.invalidateQueries({ queryKey: ['pda-quality-todos'] })
    },
    onError: (e) => setError((e as Error).message),
  })
  const upload = useMutation({
    mutationFn: (file: File) => apiUploadAttachment(file, 'EXCEPTION'),
    onSuccess: (att) => setPhotoIds((prev) => [...prev, att.id].slice(0, 3)),
    onError: (e) => setError((e as Error).message),
  })

  const locate = async (content: string) => {
    setError('')
    setDone('')
    try {
      const scan = await parse.mutateAsync(content)
      if (!scan.batch || !scan.material) {
        setError('未找到待质检任务')
        return
      }
      const list = await apiListQualityTodos({ warehouseId: CURRENT_WAREHOUSE_ID, materialId: scan.material.materialId, batchId: scan.batch.batchId, page: 1, pageSize: 20 })
      if (list.items.length === 1) {
        setSelected(list.items[0])
        setCandidates([])
      } else if (list.items.length > 1) {
        setCandidates(list.items)
        setSelected(null)
      } else {
        setError('未找到待质检任务')
      }
    } catch (e) {
      setError((e as Error).message)
    }
  }

  if (selected) {
    return (
      <div className="space-y-4">
        <TodoCard title={selected.receiptNo} material={`${selected.materialCode} ${selected.materialName}`} batch={selected.batchNo} quantity={selected.quantity} />
        {!exceptionMode ? (
          <div className="grid gap-3">
            <Button className="h-14 text-base" onClick={() => submit.mutate()} disabled={submit.isPending} data-testid="quality-pass">
              <ClipboardCheck className="size-4" data-icon />
              PASS 通过
            </Button>
            <Button className="h-14 text-base" variant="outline" onClick={() => setExceptionMode(true)}>上报异常</Button>
          </div>
        ) : (
          <div className="space-y-3">
            <select className="h-11 w-full rounded-lg border border-input bg-background px-2" value={reason} onChange={(e) => setReason(e.target.value as QualityExceptionReason)}>
              <option value="DAMAGED">破损</option>
              <option value="QTY_MISMATCH">数量不符</option>
              <option value="OTHER">其他</option>
            </select>
            <Input className="h-11" value={note} onChange={(e) => setNote(e.target.value)} placeholder="备注" />
            <label className="flex h-11 cursor-pointer items-center justify-center gap-2 rounded-lg border text-sm">
              <Camera className="size-4" data-icon />
              拍照 {photoIds.length}/3
              <input type="file" accept="image/*" className="sr-only" onChange={(e) => { const file = e.target.files?.[0]; if (file) upload.mutate(file) }} />
            </label>
            <Button className="h-12 w-full text-base" disabled={photoIds.length === 0 || submit.isPending || upload.isPending} onClick={() => submit.mutate()} data-testid="quality-exception-submit">
              提交异常
            </Button>
          </div>
        )}
        {error && <PdaError message={error} />}
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <ScanInput placeholder="扫描批次标签" onSubmit={(v) => void locate(v)} disabled={parse.isPending} />
      {done && <PdaSuccess message={done} />}
      {error && <PdaError message={error} />}
      {candidates.length > 0 && <CandidateList items={candidates} onPick={setSelected} />}
      <TodoList loading={todos.isLoading} items={todos.data?.items ?? []} empty="当前仓库无待质检任务" onPick={setSelected} />
    </div>
  )
}

function PutawayPage() {
  const qc = useQueryClient()
  const [selected, setSelected] = useState<PutawayTodo | null>(null)
  const [candidates, setCandidates] = useState<PutawayTodo[]>([])
  const [selectedLocationId, setSelectedLocationId] = useState('')
  const [scannedLocationCode, setScannedLocationCode] = useState('')
  const [qtyPerLabel, setQtyPerLabel] = useState('')
  const [printJob, setPrintJob] = useState<PrintJob | null>(null)
  const [done, setDone] = useState('')
  const [error, setError] = useState('')
  const todos = useQuery({ queryKey: ['pda-putaway-todos'], queryFn: () => apiListPutawayTodos({ warehouseId: CURRENT_WAREHOUSE_ID, page: 1, pageSize: 50 }) })
  const recommendations = useQuery({
    queryKey: ['putaway-recommendations', selected?.receiptLineId],
    queryFn: () => apiGetPutawayRecommendations(selected!.receiptLineId),
    enabled: !!selected,
  })
  const parse = useMutation({ mutationFn: (content: string) => apiParseScan({ content, context: { warehouseId: CURRENT_WAREHOUSE_ID } }) })
  const print = useMutation({
    mutationFn: () => apiPrintBatchLabels({ receiptLineId: selected!.receiptLineId, qtyPerLabel }, crypto.randomUUID()),
    onSuccess: setPrintJob,
    onError: (e) => setError((e as Error).message),
  })
  const submit = useMutation({
    mutationFn: () => apiCreatePutawayRecord({
      receiptLineId: selected!.receiptLineId,
      toLocationId: selectedLocationId,
      scannedLocationCode,
      expectedInventoryVersion: selected!.inventoryVersion,
    }, crypto.randomUUID()),
    onSuccess: () => {
      setDone('上架完成')
      setSelected(null)
      setScannedLocationCode('')
      setPrintJob(null)
      void qc.invalidateQueries({ queryKey: ['pda-putaway-todos'] })
    },
    onError: (e) => setError((e as Error).message),
  })

  const activeRecommendation = useMemo(() => recommendations.data?.find((r) => r.locationId === selectedLocationId) ?? recommendations.data?.[0], [recommendations.data, selectedLocationId])

  useEffect(() => {
    if (selected && recommendations.data?.[0] && !selectedLocationId) {
      setSelectedLocationId(recommendations.data[0].locationId)
      setQtyPerLabel(selected.defaultQtyPerLabel ?? selected.quantity)
    }
  }, [recommendations.data, selected, selectedLocationId])

  const locate = async (content: string) => {
    setError('')
    setDone('')
    try {
      const scan = await parse.mutateAsync(content)
      if (!scan.batch || !scan.material) {
        setError('未找到待上架任务')
        return
      }
      const list = await apiListPutawayTodos({ warehouseId: CURRENT_WAREHOUSE_ID, materialId: scan.material.materialId, batchId: scan.batch.batchId, page: 1, pageSize: 20 })
      if (list.items.length === 1) {
        pickPutaway(list.items[0])
      } else if (list.items.length > 1) {
        setCandidates(list.items)
        setSelected(null)
      } else {
        setError('未找到待上架任务')
      }
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const pickPutaway = (todo: PutawayTodo) => {
    setSelected(todo)
    setCandidates([])
    setSelectedLocationId('')
    setQtyPerLabel(todo.defaultQtyPerLabel ?? todo.quantity)
    setPrintJob(null)
    setError('')
  }

  if (selected) {
    return (
      <div className="space-y-4">
        <TodoCard title={selected.receiptNo} material={`${selected.materialCode} ${selected.materialName}`} batch={selected.batchNo} quantity={selected.quantity} />
        <div className="rounded-lg border bg-card p-3">
          <p className="mb-2 text-sm font-medium">推荐库位</p>
          {recommendations.isLoading ? <Skeleton className="h-12 w-full" /> : (
            <div className="space-y-2">
              {recommendations.data?.map((rec) => (
                <button
                  key={rec.locationId}
                  type="button"
                  className={`flex min-h-12 w-full items-center justify-between rounded-lg border px-3 text-left ${selectedLocationId === rec.locationId ? 'border-primary bg-primary/5' : 'bg-background'}`}
                  onClick={() => setSelectedLocationId(rec.locationId)}
                >
                  <span className="font-medium">{rec.locationCode}</span>
                  <span className="text-sm text-muted-foreground">{rec.reason}</span>
                </button>
              ))}
            </div>
          )}
        </div>
        <div className="rounded-lg border bg-card p-3">
          <label className="grid gap-1 text-sm">
            每标签数量
            <Input className="h-11 tabular-nums" value={qtyPerLabel} onChange={(e) => setQtyPerLabel(e.target.value)} />
          </label>
          <p className="mt-2 text-sm text-muted-foreground">将生成 {Math.ceil(num(selected.quantity) / Math.max(num(qtyPerLabel), 1))} 张</p>
          <Button className="mt-3 w-full" variant="outline" disabled={print.isPending} onClick={() => print.mutate()}>
            <Printer className="size-4" data-icon />
            预览标签二维码
          </Button>
          {printJob && <PrintPreview job={printJob} />}
        </div>
        <ScanInput placeholder="扫库位码确认" onSubmit={setScannedLocationCode} />
        {scannedLocationCode && (
          <div className="rounded-lg border bg-card p-3 text-sm">
            <p>已扫库位：<span className="font-medium">{scannedLocationCode}</span></p>
            {activeRecommendation && scannedLocationCode !== activeRecommendation.locationCode && <p className="text-warning">库位不一致，提交时将由服务端拦截或确认改选</p>}
          </div>
        )}
        {error && <PdaError message={error} />}
        <Button className="fixed right-3 bottom-3 left-3 mx-auto h-12 max-w-md text-base" disabled={!selectedLocationId || !scannedLocationCode || submit.isPending} onClick={() => submit.mutate()} data-testid="submit-putaway">
          完成上架
        </Button>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <ScanInput placeholder="扫描批次标签" onSubmit={(v) => void locate(v)} disabled={parse.isPending} />
      {done && <PdaSuccess message={done} />}
      {error && <PdaError message={error} />}
      {candidates.length > 0 && <PutawayCandidateList items={candidates} onPick={pickPutaway} />}
      <PutawayTodoList loading={todos.isLoading} items={todos.data?.items ?? []} empty="当前仓库无待上架任务" onPick={pickPutaway} />
    </div>
  )
}

function TodoCard({ title, material, batch, quantity }: { title: string; material: string; batch: string; quantity: string }) {
  return (
    <div className="rounded-lg border bg-card p-3">
      <p className="text-sm text-muted-foreground">{title}</p>
      <p className="mt-1 text-lg font-semibold">{material}</p>
      <p className="text-sm tabular-nums">批次 {batch} · {qtyText(quantity)} 件</p>
    </div>
  )
}

function TodoList({ loading, items, empty, onPick }: { loading: boolean; items: QualityTodo[]; empty: string; onPick: (item: QualityTodo) => void }) {
  if (loading) return <Skeleton className="h-40 w-full" />
  if (items.length === 0) return <p className="rounded-lg border bg-card p-4 text-center text-sm text-muted-foreground">{empty}</p>
  return (
    <div className="space-y-2">
      {items.map((item) => (
        <button key={item.receiptLineId} type="button" className="w-full rounded-lg border bg-card p-3 text-left" onClick={() => onPick(item)}>
          <TodoCard title={item.receiptNo} material={`${item.materialCode} ${item.materialName}`} batch={item.batchNo} quantity={item.quantity} />
        </button>
      ))}
    </div>
  )
}

function CandidateList({ items, onPick }: { items: QualityTodo[]; onPick: (item: QualityTodo) => void }) {
  return (
    <div className="rounded-lg border bg-card p-3">
      <p className="mb-2 text-sm font-medium">选择待质检任务</p>
      <div className="space-y-2">
        {items.map((item) => (
          <button key={item.receiptLineId} type="button" className="w-full rounded-lg border p-2 text-left text-sm" onClick={() => onPick(item)}>
            {item.receiptNo} · {qtyText(item.quantity)} · {item.receivedAt.slice(0, 16).replace('T', ' ')}
          </button>
        ))}
      </div>
    </div>
  )
}

function PutawayTodoList({ loading, items, empty, onPick }: { loading: boolean; items: PutawayTodo[]; empty: string; onPick: (item: PutawayTodo) => void }) {
  if (loading) return <Skeleton className="h-40 w-full" />
  if (items.length === 0) return <p className="rounded-lg border bg-card p-4 text-center text-sm text-muted-foreground">{empty}</p>
  return (
    <div className="space-y-2">
      {items.map((item) => (
        <button key={item.receiptLineId} type="button" className="w-full rounded-lg border bg-card p-3 text-left" onClick={() => onPick(item)}>
          <TodoCard title={item.receiptNo} material={`${item.materialCode} ${item.materialName}`} batch={item.batchNo} quantity={item.quantity} />
          <p className="mt-1 text-sm text-muted-foreground">暂存 {item.fromLocationCode}</p>
        </button>
      ))}
    </div>
  )
}

function PutawayCandidateList({ items, onPick }: { items: PutawayTodo[]; onPick: (item: PutawayTodo) => void }) {
  return (
    <div className="rounded-lg border bg-card p-3">
      <p className="mb-2 text-sm font-medium">选择待上架任务</p>
      <div className="space-y-2">
        {items.map((item) => (
          <button key={item.receiptLineId} type="button" className="w-full rounded-lg border p-2 text-left text-sm" onClick={() => onPick(item)}>
            {item.receiptNo} · {qtyText(item.quantity)} · 暂存 {item.fromLocationCode}
          </button>
        ))}
      </div>
    </div>
  )
}

function PdaError({ message }: { message: string }) {
  return (
    <div className="flex items-center gap-2 rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
      <AlertTriangle className="size-4" data-icon />
      {message}
    </div>
  )
}

function PdaSuccess({ message }: { message: string }) {
  return (
    <div className="flex items-center gap-2 rounded-lg border border-success/40 bg-success/10 p-3 text-sm text-success">
      <CheckCircle2 className="size-4" data-icon />
      {message}
    </div>
  )
}

function ResultPanel({
  title, subtitle, job, primaryLabel, onPrimary, onNext,
}: {
  title: string
  subtitle: string
  job: PrintJob | null
  primaryLabel: string
  onPrimary: () => void
  onNext: () => void
}) {
  return (
    <div className="space-y-4 text-center">
      <div className="rounded-lg border bg-success/10 p-6 text-success">
        <CheckCircle2 className="mx-auto mb-2 size-10" data-icon />
        <p className="text-xl font-semibold">{title}</p>
        <p className="mt-1 text-sm">{subtitle}</p>
      </div>
      {job && <PrintPreview job={job} />}
      <Button className="h-12 w-full text-base" onClick={onPrimary}>
        <Printer className="size-4" data-icon />
        {primaryLabel}
      </Button>
      <Button className="h-12 w-full text-base" variant="outline" onClick={onNext}>继续下一单</Button>
    </div>
  )
}

function PrintPreview({ job }: { job: PrintJob }) {
  return (
    <div className="rounded-lg border bg-card p-3 text-left">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-sm font-medium">打印内容</span>
        {statusBadge(job.status)}
      </div>
      {job.status === 'FAILED' && <p className="mb-2 text-sm text-destructive">{job.errorCode}</p>}
      {job.items.slice(0, 3).map((item, index) => (
        <div key={`${item.content}-${index}`} className="mb-2 rounded-lg border bg-muted/30 p-2">
          <div className="mb-2 flex size-20 items-center justify-center rounded-lg border bg-background text-xs text-muted-foreground">QR</div>
          <pre className="whitespace-pre-wrap text-xs">{item.readableText}</pre>
        </div>
      ))}
      {job.items.length > 3 && <p className="text-xs text-muted-foreground">共 {job.items.length} 张</p>}
    </div>
  )
}

function fromDocumentLine(line: ScanDocument['lines'][number] | null | undefined, material: ScanMaterial | null, scan: ScanResult | null): ReceivingLineDraft | null {
  if (!line && !material) return null
  const source = line ?? {
    id: null,
    materialId: material!.materialId,
    materialCode: material!.materialCode,
    materialName: material!.materialName,
    expectedQty: null,
  }
  return {
    orderLineId: source.id,
    materialId: source.materialId,
    materialCode: source.materialCode,
    materialName: source.materialName,
    expectedQty: source.expectedQty,
    quantity: scan?.quantity ?? (line && line.uniqueCodes.length > 0 ? '0.0000' : source.expectedQty ?? '1.0000'),
    uniqueCodes: scan?.uniqueCode ? [scan.uniqueCode.code] : [],
    batchId: scan?.batch?.batchId ?? null,
    batchProps: scan?.batchProps ?? defaultBatchProps(),
  }
}

function addSkuScan(prev: ReceivingLineDraft | null, scan: ScanResult): ReceivingLineDraft {
  const base = fromDocumentLine(null, scan.material, scan)!
  const addQty = num(scan.quantity ?? scan.material?.defaultQtyPerLabel ?? '1')
  if (!prev || prev.materialId !== base.materialId) return { ...base, quantity: dec(addQty) }
  return {
    ...prev,
    quantity: dec(num(prev.quantity) + addQty),
    batchId: scan.batch?.batchId ?? prev.batchId,
    batchProps: scan.batchProps ?? prev.batchProps ?? defaultBatchProps(),
  }
}

function addUniqueScan(prev: ReceivingLineDraft | null, scan: ScanResult): ReceivingLineDraft {
  if (!scan.uniqueCode || !scan.material) throw new Error('唯一码缺少物料或数量')
  if (prev?.uniqueCodes.includes(scan.uniqueCode.code)) throw new Error('已收过')
  const base = prev ?? fromDocumentLine(null, scan.material, scan)!
  return {
    ...base,
    quantity: dec(num(base.quantity) + (prev ? num(scan.uniqueCode.quantity) : 0)),
    uniqueCodes: [...base.uniqueCodes, scan.uniqueCode.code],
    batchProps: base.batchProps ?? defaultBatchProps(),
  }
}

function toReceiptRequest({
  document, sourceDocType, line, photoIds,
}: {
  document: ScanDocument | null
  sourceDocType: InboundOrderType | null
  line: ReceivingLineDraft | null
  photoIds: string[]
}) {
  if (!line) throw new Error('请先录入收货行')
  return {
    warehouseId: CURRENT_WAREHOUSE_ID,
    stagingLocationId: STAGING_LOCATION_ID,
    inboundOrderId: document?.inboundOrderId ?? null,
    sourceDocType: document ? undefined : sourceDocType ?? undefined,
    sourceType: document ? undefined : sourceDocType === 'PR' ? 'WORKSHOP' as const : null,
    sourceCode: document ? undefined : sourceDocType === 'PR' ? 'WS-01' : null,
    lines: [{
      orderLineId: line.orderLineId,
      materialId: line.materialId,
      batchId: line.batchId,
      batchProps: line.batchId ? null : line.batchProps ?? defaultBatchProps(),
      quantity: line.quantity,
      uniqueCodes: line.uniqueCodes.length > 0 ? line.uniqueCodes : undefined,
    }],
    photos: photoIds,
  }
}

function defaultBatchProps(): BatchProps {
  return {
    sourceBatchNo: 'PRD-20260821-01',
    productionDate: '2026-08-21',
    expiryDate: null,
    sourceType: null,
    sourceCode: null,
  }
}

function num(v: string | null | undefined): number {
  return Number(v ?? 0)
}

function dec(v: number): string {
  return Number.isFinite(v) ? v.toFixed(4) : '0.0000'
}
