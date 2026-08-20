import { delay, http, HttpResponse } from 'msw'
import type {
  AttachmentItem, BatchItem, BatchProps, InboundOrder, InboundOrderCreateRequest, InboundOrderLine,
  InboundOrderType, InboundOrderVoidRequest, LocationItem, PrintBizType,
  PrintJob, PrintJobItem, PutawayRecordCreateRequest, PutawayTodo, QualityCheckRequest,
  QualityExceptionItem, QualityResolveRequest, QualityTodo, Receipt, ReceiptCreateRequest,
  ReceiptLine, ScanDocument, ScanMaterial, ScanParseRequest, ScanResult, SourceType,
} from '../api/types'
import { db, findUser, getById, newId, nowIso, queryList } from './db'
import type { QueryOptions } from './db'
import { MOCK_TOKEN_PREFIX } from './seed'

type EnvelopePayload = { code: string; message: string; data: unknown }
type IdempotencyEntry = { status: number; payload: EnvelopePayload | null }

const idempotencyStore = new Map<string, IdempotencyEntry>()
let orderSeq = 3
let receiptSeq = 4
let batchSeq = 100
let printSeq = 1
let uniqueSeq = 10

export function resetInboundMockState(): void {
  idempotencyStore.clear()
  orderSeq = 3
  receiptSeq = 4
  batchSeq = 100
  printSeq = 1
  uniqueSeq = 10
}

const mockDelay = () => delay(import.meta.env.MODE === 'test' ? 0 : 80)

const ok = (data: unknown) => HttpResponse.json({ code: 'OK', message: 'ok', data })
const created = (data: unknown) => HttpResponse.json({ code: 'OK', message: 'ok', data }, { status: 201 })
const noContent = () => new HttpResponse(null, { status: 204 })
const fail = (code: string, message: string, status: number) =>
  HttpResponse.json({ code, message, data: null }, { status })
const isResponse = (value: unknown): value is Response => value instanceof Response

function tokenUsername(req: Request): string | null {
  const h = req.headers.get('Authorization')
  if (!h?.startsWith('Bearer ')) return null
  const token = h.slice(7)
  if (!token.startsWith(MOCK_TOKEN_PREFIX)) return null
  return token.slice(MOCK_TOKEN_PREFIX.length).split('#')[0]
}

function requireAuth(req: Request): { userId: string; userName: string; username: string } | Response {
  const username = tokenUsername(req)
  const user = username ? findUser(username) : null
  if (!user) return fail('UNAUTHORIZED', '未登录或会话已过期', 401)
  return { userId: user.id, userName: user.name, username: username ?? '' }
}

async function parseSearchBody(req: Request, fuzzyFields: string[], defaultSort?: QueryOptions['sort']): Promise<QueryOptions> {
  const body = (await req.json()) as Record<string, unknown>
  const opts: QueryOptions = {
    fuzzyFields,
    fixed: {},
    filter: body.filter as QueryOptions['filter'],
    sort: Array.isArray(body.sort) && body.sort.length > 0 ? body.sort as QueryOptions['sort'] : defaultSort,
    page: Number(body.page ?? 1),
    pageSize: Number(body.pageSize ?? 20),
  }
  if (typeof body.keyword === 'string' && body.keyword) opts.keyword = body.keyword
  for (const [k, v] of Object.entries(body)) {
    if (['keyword', 'filter', 'sort', 'page', 'pageSize'].includes(k)) continue
    if (v === undefined || v === null || v === '') continue
    opts.fixed![k] = v as string | boolean | number
  }
  return opts
}

function idempotencyKey(req: Request): string | null {
  const key = req.headers.get('Idempotency-Key')
  return key ? `${req.method}:${new URL(req.url).pathname}:${key}` : null
}

function replay(req: Request): Response | null {
  const key = idempotencyKey(req)
  if (!key) return null
  const hit = idempotencyStore.get(key)
  if (!hit) return null
  return hit.payload ? HttpResponse.json(hit.payload, { status: hit.status }) : noContent()
}

function remember(req: Request, status: number, data: unknown): Response {
  const payload = { code: 'OK', message: 'ok', data }
  const key = idempotencyKey(req)
  if (key) idempotencyStore.set(key, { status, payload })
  return HttpResponse.json(payload, { status })
}

function rememberNoContent(req: Request): Response {
  const key = idempotencyKey(req)
  if (key) idempotencyStore.set(key, { status: 204, payload: null })
  return noContent()
}

function num(v: string | null | undefined): number {
  return Number(v ?? 0)
}

function dec(v: number): string {
  return Number.isFinite(v) ? v.toFixed(4) : '0.0000'
}

function whCode(warehouseId: string): string {
  return getById(db.warehouses, warehouseId)?.code ?? warehouseId
}

function materialById(id: string) {
  return getById(db.materials, id)
}

function materialByCode(code: string) {
  return db.materials.find((m) => m.code === code)
}

function sourceBy(type: SourceType | null | undefined, code: string | null | undefined) {
  if (!type || !code) return null
  return db.sources.find((s) => s.type === type && s.code === code && s.status === 'ENABLED') ?? null
}

function findOrderLine(lineId: string): { order: InboundOrder; line: InboundOrderLine } | null {
  for (const order of db.inboundOrders) {
    const line = order.lines.find((l) => l.id === lineId)
    if (line) return { order, line }
  }
  return null
}

function findReceiptLine(lineId: string): { receipt: Receipt; line: ReceiptLine } | null {
  for (const receipt of db.receipts) {
    const line = receipt.lines.find((l) => l.id === lineId)
    if (line) return { receipt, line }
  }
  return null
}

function scanMaterial(materialId: string): ScanMaterial | null {
  const material = materialById(materialId)
  if (!material) return null
  return {
    materialId: material.id,
    materialCode: material.code,
    materialName: material.name,
    batchControlled: material.batchControlled,
    labelType: material.labelType,
    defaultUom: material.defaultUom,
    defaultQtyPerLabel: material.defaultQtyPerLabel,
  }
}

function scanDocument(order: InboundOrder): ScanDocument {
  return {
    inboundOrderId: order.id,
    docType: order.type,
    docNo: order.orderNo,
    warehouseId: order.warehouseId,
    warehouseCode: order.warehouseCode,
    status: order.status,
    lines: structuredClone(order.lines),
  }
}

function emptyScan(type: ScanResult['type'], labelType: ScanResult['labelType']): ScanResult {
  return {
    type,
    labelType,
    material: null,
    uniqueCode: null,
    batch: null,
    batchProps: null,
    quantity: null,
    document: null,
    source: null,
    external: null,
    warnings: [],
  }
}

function documentWarnings(order: InboundOrder, context?: ScanParseRequest['context']) {
  const warnings: ScanResult['warnings'] = []
  if (order.status === 'VOIDED') {
    warnings.push({ code: 'ORDER_VOIDED', message: '单据已作废', blocking: true })
  } else if (order.status === 'RECEIVED') {
    warnings.push({ code: 'ORDER_STATUS_INVALID', message: '单据已收完', blocking: true })
  }
  if (context?.warehouseId && context.warehouseId !== order.warehouseId) {
    warnings.push({ code: 'WAREHOUSE_MISMATCH', message: '单据仓库与当前 PDA 仓库不一致', blocking: true })
  }
  return warnings
}

function ensureSourceRule(type: InboundOrderType, sourceType?: SourceType | null, sourceCode?: string | null): Response | null {
  if (type === 'PO' && (sourceType !== 'SUPPLIER' || !sourceBy('SUPPLIER', sourceCode))) {
    return fail('SOURCE_NOT_FOUND', '采购入库必须选择启用供应商', 404)
  }
  if (type === 'PR' && (sourceType !== 'WORKSHOP' || !sourceBy('WORKSHOP', sourceCode))) {
    return fail('SOURCE_NOT_FOUND', '生产入库必须选择启用车间', 404)
  }
  if (type === 'OT' && ((sourceType && !sourceCode) || (!sourceType && sourceCode))) {
    return fail('VALIDATION_ERROR', '来源类型与来源编码必须同时为空或同时有值', 400)
  }
  if (type === 'OT' && sourceType && !sourceBy(sourceType, sourceCode)) {
    return fail('SOURCE_NOT_FOUND', '来源不存在或未启用', 404)
  }
  return null
}

function ensureStaging(warehouseId: string, stagingLocationId: string): LocationItem | Response {
  const loc = getById(db.locations, stagingLocationId)
  if (!loc || loc.warehouseId !== warehouseId || loc.status !== 'ENABLED' || loc.type !== 'STAGING') {
    return fail('STAGING_LOCATION_INVALID', '暂存库位不合法', 400)
  }
  return loc
}

function createBatch(materialId: string, props: BatchProps | null | undefined): BatchItem {
  batchSeq += 1
  const material = materialById(materialId)!
  const batch: BatchItem = {
    id: newId('b'),
    materialId,
    materialCode: material.code,
    materialName: material.name,
    batchNo: `260821${String(batchSeq).padStart(3, '0')}`,
    sourceBatchNo: props?.sourceBatchNo ?? null,
    sourceType: props?.sourceType ?? null,
    sourceCode: props?.sourceCode ?? null,
    productionDate: props?.productionDate ?? null,
    expiryDate: props?.expiryDate ?? null,
    status: 'ACTIVE',
    createdAt: nowIso(),
  }
  db.batches.push(batch)
  return batch
}

function resolveBatch(line: ReceiptCreateRequest['lines'][number], sourceType?: SourceType | null, sourceCode?: string | null): BatchItem | Response {
  const material = materialById(line.materialId)
  if (!material) return fail('MATERIAL_NOT_FOUND', '物料不存在', 404)
  if (line.batchId && line.batchProps) return fail('VALIDATION_ERROR', 'batchId 与 batchProps 互斥', 400)
  if (line.batchId) {
    const batch = getById(db.batches, line.batchId)
    if (!batch || batch.materialId !== line.materialId) return fail('BATCH_NOT_FOUND', '批次不存在', 404)
    return batch
  }
  const props = line.batchProps ?? {}
  const finalProps: BatchProps = {
    sourceBatchNo: props.sourceBatchNo ?? null,
    productionDate: props.productionDate ?? null,
    expiryDate: props.expiryDate ?? null,
    sourceType: props.sourceType ?? sourceType ?? null,
    sourceCode: props.sourceCode ?? sourceCode ?? null,
  }
  if (material.batchControlled && !finalProps.sourceBatchNo) return fail('BATCH_REQUIRED', '批控物料必须提供批次', 400)
  if (finalProps.sourceType && !sourceBy(finalProps.sourceType, finalProps.sourceCode)) {
    return fail('SOURCE_NOT_FOUND', '批次来源不存在或未启用', 404)
  }
  if (sourceType && finalProps.sourceType && (finalProps.sourceType !== sourceType || finalProps.sourceCode !== sourceCode)) {
    return fail('SOURCE_MISMATCH', '批次来源与当前收货上下文不一致', 400)
  }
  return createBatch(line.materialId, finalProps)
}

function updateOrderStatus(order: InboundOrder): void {
  const allDone = order.lines.every((l) => num(l.receivedQty) >= num(l.expectedQty))
  order.status = allDone ? 'RECEIVED' : order.lines.some((l) => num(l.receivedQty) > 0) ? 'RECEIVING' : 'CONFIRMED'
}

function updateReceiptStatus(receipt: Receipt): void {
  if (receipt.lines.every((l) => l.status === 'PUTAWAY_DONE')) {
    receipt.status = 'DONE'
  } else if (receipt.lines.every((l) => l.status === 'CHECKED' || l.status === 'PUTAWAY_DONE')) {
    receipt.status = 'PUTAWAY'
  } else if (receipt.lines.some((l) => l.status === 'CHECKED' || l.status === 'EXCEPTION')) {
    receipt.status = 'CHECKING'
  } else {
    receipt.status = 'RECEIVING'
  }
}

function qualityTodos(): QualityTodo[] {
  return db.receipts.flatMap((receipt) => receipt.lines
    .filter((line) => line.status === 'RECEIVED')
    .map((line) => ({
      receiptLineId: line.id,
      receiptId: receipt.id,
      receiptNo: receipt.receiptNo,
      warehouseId: receipt.warehouseId,
      warehouseCode: receipt.warehouseCode,
      materialId: line.materialId,
      materialCode: line.materialCode,
      materialName: line.materialName,
      batchId: line.batchId,
      batchNo: line.batchNo,
      quantity: line.actualQty,
      receivedAt: receipt.occurredAt,
    })))
}

function putawayTodos(): PutawayTodo[] {
  return db.receipts.flatMap((receipt) => receipt.lines
    .filter((line) => receipt.status === 'PUTAWAY' && line.status === 'CHECKED')
    .map((line) => {
      const material = materialById(line.materialId)
      return {
        receiptLineId: line.id,
        receiptNo: receipt.receiptNo,
        warehouseId: receipt.warehouseId,
        warehouseCode: receipt.warehouseCode,
        materialId: line.materialId,
        materialCode: line.materialCode,
        materialName: line.materialName,
        batchId: line.batchId,
        batchNo: line.batchNo,
        quantity: line.actualQty,
        defaultQtyPerLabel: material?.defaultQtyPerLabel ?? null,
        fromLocationId: receipt.stagingLocationId,
        fromLocationCode: receipt.stagingLocationCode,
        inventoryVersion: db.putawayVersions[line.id] ?? 1,
      }
    }))
}

function printJob(
  templateCode: string,
  items: PrintJobItem[],
  ctx: { bizType: PrintBizType | null; bizId: string | null },
  user: { userId: string; userName: string },
  failed = false,
): PrintJob {
  printSeq += 1
  const job: PrintJob = {
    id: `pj-${String(printSeq).padStart(3, '0')}`,
    bizType: ctx.bizType,
    bizId: ctx.bizId,
    templateCode,
    status: failed ? 'FAILED' : 'READY',
    items,
    fileUrl: failed ? null : `/api/print/jobs/pj-${String(printSeq).padStart(3, '0')}/file`,
    errorCode: failed ? 'PRINT_GENERATION_FAILED' : null,
    createdBy: user.userId,
    createdByName: user.userName,
    createdAt: nowIso(),
    updatedAt: nowIso(),
  }
  db.printJobs.push(job)
  return job
}

function splitQuantities(totalQty: string, qtyPerLabel: string): string[] {
  const total = num(totalQty)
  const per = num(qtyPerLabel)
  if (per <= 0) return []
  const full = Math.floor(total / per)
  const rest = total - full * per
  const parts = Array.from({ length: full }, () => dec(per))
  if (rest > 0) parts.push(dec(rest))
  return parts.length > 0 ? parts : [dec(total)]
}

function receiptLineForPrint(receiptLineId: string): { receipt: Receipt; line: ReceiptLine } | Response {
  const found = findReceiptLine(receiptLineId)
  if (!found) return fail('RECEIPT_LINE_NOT_FOUND', '收货行不存在', 404)
  return found
}

export const inboundHandlers = [
  http.post('/api/inbound-orders/search', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const opts = await parseSearchBody(request, ['orderNo', 'sourceCode'], [{ field: 'createdAt', dir: 'desc' }])
    return ok(queryList(db.inboundOrders, opts))
  }),

  http.post('/api/inbound-orders', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const body = (await request.json()) as InboundOrderCreateRequest
    if (!body.warehouseId || body.lines.length === 0) return fail('VALIDATION_ERROR', '仓库和行不能为空', 400)
    const wh = getById(db.warehouses, body.warehouseId)
    if (!wh || wh.status !== 'ENABLED') return fail('WAREHOUSE_NOT_FOUND', '仓库不存在或未启用', 404)
    const sourceError = ensureSourceRule(body.type, body.sourceType, body.sourceCode)
    if (sourceError) return sourceError
    const lines: InboundOrderLine[] = []
    for (const [i, line] of body.lines.entries()) {
      const material = materialById(line.materialId)
      if (!material || material.status !== 'ENABLED') return fail('MATERIAL_NOT_FOUND', '物料不存在或未启用', 404)
      if (num(line.expectedQty) <= 0) return fail('VALIDATION_ERROR', '应到数量必须大于 0', 400)
      lines.push({
        id: newId('iol'),
        lineNo: i + 1,
        materialId: material.id,
        materialCode: material.code,
        materialName: material.name,
        expectedQty: dec(num(line.expectedQty)),
        receivedQty: '0.0000',
        remainingQty: dec(num(line.expectedQty)),
        uniqueCodes: [],
      })
    }
    orderSeq += 1
    const order: InboundOrder = {
      id: newId('io'),
      orderNo: `${body.type}-20260821-${String(orderSeq).padStart(4, '0')}`,
      type: body.type,
      warehouseId: wh.id,
      warehouseCode: wh.code,
      sourceType: body.sourceType ?? null,
      sourceCode: body.sourceCode ?? null,
      status: 'CONFIRMED',
      lines,
      createdAt: nowIso(),
      createdBy: auth.userName,
      voidedAt: null,
      voidedBy: null,
      voidReason: null,
    }
    db.inboundOrders.unshift(order)
    return remember(request, 201, order)
  }),

  http.get('/api/inbound-orders/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const order = getById(db.inboundOrders, String(params.id))
    if (!order) return fail('ORDER_NOT_FOUND', '入库单不存在', 404)
    return ok(order)
  }),

  http.post('/api/inbound-orders/:id/void', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const order = getById(db.inboundOrders, String(params.id))
    if (!order) return fail('ORDER_NOT_FOUND', '入库单不存在', 404)
    if (!['CONFIRMED', 'RECEIVING'].includes(order.status)) {
      return fail('ORDER_STATUS_INVALID', '当前状态不可作废', 409)
    }
    const body = (await request.json()) as InboundOrderVoidRequest
    if (!body.reason?.trim()) return fail('VALIDATION_ERROR', '作废原因必填', 400)
    order.status = 'VOIDED'
    order.voidedAt = nowIso()
    order.voidedBy = auth.userName
    order.voidReason = body.reason
    return remember(request, 200, order)
  }),

  http.post('/api/scan/parse', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const body = (await request.json()) as ScanParseRequest
    if (!body.content.startsWith('AWMS1:')) {
      return ok({ ...emptyScan('UNKNOWN', null), message: '未识别，请手动输入' })
    }
    let payload: Record<string, unknown>
    try {
      payload = JSON.parse(body.content.slice('AWMS1:'.length)) as Record<string, unknown>
    } catch {
      return fail('SCAN_PARSE_ERROR', '标签内容损坏', 400)
    }
    const t = String(payload.t ?? '')
    if (t === 'D') {
      const orderNo = String(payload.d ?? '')
      const order = db.inboundOrders.find((o) => o.orderNo === orderNo || o.id === orderNo)
      if (!order) return fail('ORDER_NOT_FOUND', '入库单不存在', 404)
      return ok({ ...emptyScan('DOCUMENT_QR', 'D'), document: scanDocument(order), warnings: documentWarnings(order, body.context) })
    }
    if (t === 'S') {
      const material = materialByCode(String(payload.s ?? ''))
      if (!material) return fail('MATERIAL_NOT_FOUND', '物料不存在', 404)
      const ol = typeof payload.ol === 'string' ? payload.ol : null
      const sourceType = payload.rt === 'S' ? 'SUPPLIER' : payload.rt === 'W' ? 'WORKSHOP' : null
      const sourceCode = typeof payload.rc === 'string' ? payload.rc : null
      const result = emptyScan('SKU_LABEL', 'S')
      result.material = scanMaterial(material.id)
      result.batchProps = {
        sourceBatchNo: typeof payload.rb === 'string' ? payload.rb : null,
        productionDate: typeof payload.pd === 'string' ? payload.pd : null,
        expiryDate: typeof payload.ex === 'string' ? payload.ex : null,
        sourceType,
        sourceCode,
      }
      result.source = sourceType && sourceCode && sourceBy(sourceType, sourceCode)
        ? { sourceType, sourceCode, sourceName: sourceBy(sourceType, sourceCode)!.name }
        : null
      if (ol) {
        const found = findOrderLine(ol)
        if (found) result.document = scanDocument(found.order)
      }
      return ok(result)
    }
    if (t === 'U') {
      const code = String(payload.u ?? '')
      const found = db.inboundOrders.flatMap((o) => o.lines).flatMap((l) => l.uniqueCodes.map((uc) => ({ line: l, uc }))).find((x) => x.uc.code === code)
      if (!found) return fail('UNIQUE_CODE_NOT_IN_ORDER', '唯一码不在单据清单', 400)
      const result = emptyScan('UNIQUE_LABEL', 'U')
      result.material = scanMaterial(found.line.materialId)
      result.uniqueCode = structuredClone(found.uc)
      result.quantity = found.uc.quantity
      if (body.context?.inboundOrderId) {
        const order = getById(db.inboundOrders, body.context.inboundOrderId)
        const inOrder = order?.lines.some((l) => l.uniqueCodes.some((uc) => uc.code === code))
        if (!inOrder) result.warnings.push({ code: 'UNIQUE_CODE_NOT_IN_ORDER', message: '唯一码不在当前单据', blocking: true })
      }
      if (found.uc.status === 'RECEIVED') {
        result.warnings.push({ code: 'UNIQUE_CODE_ALREADY_RECEIVED', message: '唯一码已收过', blocking: true })
      }
      return ok(result)
    }
    if (t === 'B') {
      const material = materialByCode(String(payload.s ?? ''))
      const batch = db.batches.find((b) => b.materialCode === payload.s && b.batchNo === payload.b)
      if (!material || !batch) return fail('BATCH_NOT_FOUND', '批次不存在', 404)
      const result = emptyScan('BATCH_LABEL', 'B')
      result.material = scanMaterial(material.id)
      result.batch = {
        batchId: batch.id,
        batchNo: batch.batchNo,
        sourceBatchNo: batch.sourceBatchNo,
        productionDate: batch.productionDate,
        expiryDate: batch.expiryDate,
      }
      result.quantity = typeof payload.q === 'string' ? payload.q : null
      return ok(result)
    }
    return ok({ ...emptyScan('UNKNOWN', null), message: '未识别，请手动输入' })
  }),

  http.post('/api/receipts/search', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const opts = await parseSearchBody(request, ['receiptNo', 'sourceDocNo'], [{ field: 'occurredAt', dir: 'desc' }])
    return ok(queryList(db.receipts, opts))
  }),

  http.get('/api/receipts/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const receipt = getById(db.receipts, String(params.id))
    if (!receipt) return fail('RECEIPT_NOT_FOUND', '收货单不存在', 404)
    return ok(receipt)
  }),

  http.post('/api/receipts', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const body = (await request.json()) as ReceiptCreateRequest
    const staging = ensureStaging(body.warehouseId, body.stagingLocationId)
    if (isResponse(staging)) return staging
    const sourceDocType = body.inboundOrderId ? undefined : body.sourceDocType
    if (!body.inboundOrderId && sourceDocType === 'PO') return fail('ORDER_REQUIRED_FOR_PO', '采购入库必须引用预建单', 400)

    const order = body.inboundOrderId ? getById(db.inboundOrders, body.inboundOrderId) : null
    if (body.inboundOrderId && !order) return fail('ORDER_NOT_FOUND', '入库单不存在', 404)
    if (order?.status === 'VOIDED') return fail('ORDER_VOIDED', '入库单已作废', 409)
    if (order?.status === 'RECEIVED') return fail('ORDER_STATUS_INVALID', '入库单已收完', 409)
    if (order && order.warehouseId !== body.warehouseId) return fail('WAREHOUSE_MISMATCH', '入库单仓库不一致', 400)

    const finalType = order?.type ?? body.sourceDocType
    if (!finalType) return fail('VALIDATION_ERROR', 'sourceDocType 必填', 400)
    const sourceError = ensureSourceRule(finalType, order?.sourceType ?? body.sourceType, order?.sourceCode ?? body.sourceCode)
    if (sourceError) return sourceError

    const lines: ReceiptLine[] = []
    for (const [idx, reqLine] of body.lines.entries()) {
      const material = materialById(reqLine.materialId)
      if (!material) return fail('MATERIAL_NOT_FOUND', '物料不存在', 404)
      let orderLine: InboundOrderLine | null = null
      if (order) {
        orderLine = reqLine.orderLineId
          ? order.lines.find((l) => l.id === reqLine.orderLineId) ?? null
          : order.lines.filter((l) => l.materialId === reqLine.materialId)[0] ?? null
        if (!orderLine || orderLine.materialId !== reqLine.materialId) return fail('ORDER_LINE_MISMATCH', '收货行与入库单不匹配', 400)
        const matchedLine = orderLine
        if (order.type === 'PO' && num(reqLine.quantity) !== num(matchedLine.expectedQty)) {
          return fail('QTY_MISMATCH_STRICT', '数量与入库单不一致，需作废重建或联系主管', 400)
        }
        if (material.labelType === 'UNIQUE') {
          const codes = reqLine.uniqueCodes ?? []
          const uniqueRows = codes.map((code) => matchedLine.uniqueCodes.find((uc) => uc.code === code))
          if (uniqueRows.some((uc) => !uc)) return fail('UNIQUE_CODE_NOT_IN_ORDER', '唯一码不在单据清单', 400)
          if (new Set(codes).size !== codes.length || uniqueRows.some((uc) => uc?.status === 'RECEIVED')) {
            return fail('UNIQUE_CODE_ALREADY_RECEIVED', '唯一码已收过', 409)
          }
          const sum = uniqueRows.reduce((total, uc) => total + num(uc?.quantity), 0)
          if (sum !== num(reqLine.quantity)) return fail('UNIQUE_CODE_QTY_MISMATCH', '唯一码数量之和必须等于提交数量', 400)
          uniqueRows.forEach((uc) => {
            if (uc) {
              uc.status = 'RECEIVED'
              uc.receivedAt = nowIso()
            }
          })
        }
        matchedLine.receivedQty = dec(num(matchedLine.receivedQty) + num(reqLine.quantity))
        matchedLine.remainingQty = dec(Math.max(num(matchedLine.expectedQty) - num(matchedLine.receivedQty), 0))
      }
      const batch = resolveBatch(reqLine, order?.sourceType ?? body.sourceType, order?.sourceCode ?? body.sourceCode)
      if (isResponse(batch)) return batch
      lines.push({
        id: newId('rl'),
        lineNo: idx + 1,
        orderLineId: orderLine?.id ?? null,
        orderLineNo: orderLine?.lineNo ?? null,
        materialId: material.id,
        materialCode: material.code,
        materialName: material.name,
        batchId: batch.id,
        batchNo: batch.batchNo,
        expectedQty: orderLine?.expectedQty ?? null,
        actualQty: dec(num(reqLine.quantity)),
        qtyDiff: orderLine ? dec(num(reqLine.quantity) - num(orderLine.expectedQty)) : null,
        status: 'RECEIVED',
        sourceBatchNo: batch.sourceBatchNo,
        productionDate: batch.productionDate,
        expiryDate: batch.expiryDate,
      })
    }
    if (order) updateOrderStatus(order)
    receiptSeq += 1
    const receipt: Receipt = {
      id: newId('rcp'),
      receiptNo: `RCP-20260821-${String(receiptSeq).padStart(4, '0')}`,
      warehouseId: body.warehouseId,
      warehouseCode: whCode(body.warehouseId),
      inboundOrderId: order?.id ?? null,
      sourceDocType: order?.type ?? finalType,
      sourceDocNo: order?.orderNo ?? body.sourceDocNo ?? null,
      sourceType: order?.sourceType ?? body.sourceType ?? null,
      sourceCode: order?.sourceCode ?? body.sourceCode ?? null,
      status: 'RECEIVING',
      lines,
      stagingLocationId: staging.id,
      stagingLocationCode: staging.code,
      photos: body.photos ?? [],
      operatorId: auth.userId,
      operatorName: auth.userName,
      occurredAt: nowIso(),
    }
    db.attachments.forEach((att) => {
      if (receipt.photos.includes(att.id)) {
        att.bizType = 'RECEIPT'
        att.bizId = receipt.id
      }
    })
    db.receipts.unshift(receipt)
    return remember(request, 201, receipt)
  }),

  http.post('/api/receipts/:id/print', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const receipt = getById(db.receipts, String(params.id))
    if (!receipt) return fail('RECEIPT_NOT_FOUND', '收货单不存在', 404)
    const job = printJob('RECEIPT', [{
      labelType: 'R',
      content: `RECEIPT:${receipt.receiptNo}`,
      readableText: `收货回执 ${receipt.receiptNo}\n数量：${receipt.lines.map((l) => l.actualQty).join(' / ')}`,
      quantity: null,
    }], { bizType: 'RECEIPT', bizId: receipt.id }, auth)
    return remember(request, 201, job)
  }),

  http.post('/api/quality-todos/search', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const opts = await parseSearchBody(request, ['receiptNo', 'materialCode', 'materialName', 'batchNo'], [{ field: 'receivedAt', dir: 'desc' }])
    return ok(queryList(qualityTodos(), opts))
  }),

  http.post('/api/receipt-lines/:lineId/quality-check', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const found = findReceiptLine(String(params.lineId))
    if (!found) return fail('RECEIPT_LINE_NOT_FOUND', '收货行不存在', 404)
    const body = (await request.json()) as QualityCheckRequest
    if (found.line.status !== 'RECEIVED') return fail('QC_STATUS_INVALID', '该行已质检', 409)
    if (num(body.checkedQty) !== num(found.line.actualQty)) return fail('VALIDATION_ERROR', '本期全检数量必须等于收货数量', 400)
    if (body.result === 'PASS') {
      found.line.status = 'CHECKED'
      updateReceiptStatus(found.receipt)
      return rememberNoContent(request)
    }
    if (!body.exceptionReason || !body.photoIds || body.photoIds.length === 0 || body.photoIds.length > 3) {
      return fail('VALIDATION_ERROR', '异常必须选择原因并上传 1-3 张照片', 400)
    }
    found.line.status = 'EXCEPTION'
    updateReceiptStatus(found.receipt)
    const check: QualityExceptionItem = {
      id: newId('qc'),
      receiptLineId: found.line.id,
      receiptNo: found.receipt.receiptNo,
      warehouseId: found.receipt.warehouseId,
      warehouseCode: found.receipt.warehouseCode,
      materialCode: found.line.materialCode,
      materialName: found.line.materialName,
      batchNo: found.line.batchNo,
      checkedQty: body.checkedQty,
      exceptionReason: body.exceptionReason,
      note: body.note ?? null,
      photoIds: body.photoIds,
      checkedBy: auth.userId,
      checkedByName: auth.userName,
      checkedAt: nowIso(),
      resolutionAction: null,
      resolutionNote: null,
      resolvedBy: null,
      resolvedByName: null,
      resolvedAt: null,
    }
    db.qualityChecks.unshift(check)
    db.attachments.forEach((att) => {
      if (check.photoIds.includes(att.id)) {
        att.bizType = 'EXCEPTION'
        att.bizId = check.id
      }
    })
    return rememberNoContent(request)
  }),

  http.post('/api/quality-checks/search', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const opts = await parseSearchBody(request, ['receiptNo', 'materialCode', 'materialName', 'batchNo'], [{ field: 'checkedAt', dir: 'desc' }])
    if (opts.fixed?.resolutionStatus === 'PENDING') {
      delete opts.fixed.resolutionStatus
      return ok(queryList(db.qualityChecks.filter((q) => q.resolutionAction === null), opts))
    }
    if (opts.fixed?.resolutionStatus === 'RESOLVED') {
      delete opts.fixed.resolutionStatus
      return ok(queryList(db.qualityChecks.filter((q) => q.resolutionAction !== null), opts))
    }
    return ok(queryList(db.qualityChecks, opts))
  }),

  http.post('/api/quality-checks/:checkId/resolve', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const check = getById(db.qualityChecks, String(params.checkId))
    if (!check) return fail('QUALITY_CHECK_NOT_FOUND', '质检异常不存在', 404)
    if (check.resolutionAction) return fail('QUALITY_CHECK_ALREADY_RESOLVED', '该异常已处理', 409)
    const body = (await request.json()) as QualityResolveRequest
    if (body.action === 'REJECT' && !body.note?.trim()) return fail('QC_STATUS_INVALID', '驳回必须填写备注', 400)
    const found = findReceiptLine(check.receiptLineId)
    if (body.action === 'PASS' && found) {
      found.line.status = 'CHECKED'
      updateReceiptStatus(found.receipt)
    }
    check.resolutionAction = body.action
    check.resolutionNote = body.note ?? null
    check.resolvedBy = auth.userId
    check.resolvedByName = auth.userName
    check.resolvedAt = nowIso()
    return remember(request, 200, check)
  }),

  http.post('/api/putaway-todos/search', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const opts = await parseSearchBody(request, ['receiptNo', 'materialCode', 'materialName', 'batchNo'])
    return ok(queryList(putawayTodos(), opts))
  }),

  http.get('/api/putaway-todos/:receiptLineId/recommendations', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const todo = putawayTodos().find((t) => t.receiptLineId === String(params.receiptLineId))
    if (!todo) return fail('RECEIPT_LINE_NOT_FOUND', '上架待办不存在', 404)
    const locations = db.locations
      .filter((l) => l.warehouseId === todo.warehouseId && l.status === 'ENABLED' && l.type === 'DEFAULT')
      .sort((a, b) => a.code.localeCompare(b.code, 'zh-CN'))
    return ok(locations.map((l, i) => ({
      locationId: l.id,
      locationCode: l.code,
      reasonCode: i === 0 ? 'SAME_MATERIAL' : 'FALLBACK',
      reason: i === 0 ? '同物料集中' : '按库位编码排序',
      recommended: i === 0,
    })))
  }),

  http.post('/api/putaway-records', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const body = (await request.json()) as PutawayRecordCreateRequest
    const found = findReceiptLine(body.receiptLineId)
    if (!found) return fail('RECEIPT_LINE_NOT_FOUND', '收货行不存在', 404)
    const loc = getById(db.locations, body.toLocationId)
    if (!loc || loc.warehouseId !== found.receipt.warehouseId || loc.status !== 'ENABLED' || loc.type !== 'DEFAULT' || loc.code !== body.scannedLocationCode) {
      return fail('PUTAWAY_LOCATION_INVALID', '目标库位不合法', 400)
    }
    const version = db.putawayVersions[body.receiptLineId] ?? 1
    if (body.expectedInventoryVersion !== version) return fail('VERSION_CONFLICT', '库存版本已变化，请刷新后重试', 409)
    if (found.line.status !== 'CHECKED') return fail('RECEIPT_STATUS_INVALID', '当前行不可上架', 409)
    found.line.status = 'PUTAWAY_DONE'
    db.putawayVersions[body.receiptLineId] = version + 1
    updateReceiptStatus(found.receipt)
    return rememberNoContent(request)
  }),

  http.post('/api/attachments', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const fd = await request.formData()
    const file = fd.get('file') as File | null
    if (!file) return fail('VALIDATION_ERROR', '缺少附件文件', 400)
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      return fail('ATTACHMENT_TYPE_INVALID', '附件类型不支持', 400)
    }
    if (file.size > 10 * 1024 * 1024 || file.name.includes('too-large')) {
      return fail('ATTACHMENT_TOO_LARGE', '附件超过 10MB', 413)
    }
    const item: AttachmentItem = {
      id: newId('att'),
      fileName: file.name,
      mimeType: file.type,
      size: file.size,
      bizType: null,
      bizId: null,
      uploadedBy: auth.userId,
      uploadedByName: auth.userName,
      uploadedAt: nowIso(),
      url: '',
      thumbnailUrl: '',
    }
    item.url = `/api/attachments/${item.id}`
    item.thumbnailUrl = `/api/attachments/${item.id}/thumbnail`
    db.attachments.unshift(item)
    return created(item)
  }),

  http.get('/api/attachments', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const sp = new URL(request.url).searchParams
    const fixed: QueryOptions['fixed'] = {}
    for (const key of ['bizType', 'bizId', 'uploadedBy', 'dateFrom', 'dateTo']) {
      const v = sp.get(key)
      if (v) fixed[key] = v
    }
    const page = Number(sp.get('page') ?? 1)
    const pageSize = Number(sp.get('pageSize') ?? 20)
    return ok(queryList(db.attachments, { fixed, page, pageSize }))
  }),

  http.get('/api/attachments/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    if (!getById(db.attachments, String(params.id))) return fail('ATTACHMENT_NOT_FOUND', '附件不存在', 404)
    return new HttpResponse(new Uint8Array([1, 2, 3]), { status: 200, headers: { 'Content-Type': 'image/jpeg' } })
  }),

  http.get('/api/attachments/:id/thumbnail', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    if (!getById(db.attachments, String(params.id))) return fail('ATTACHMENT_NOT_FOUND', '附件不存在', 404)
    return new HttpResponse(new Uint8Array([1, 2, 3]), { status: 200, headers: { 'Content-Type': 'image/jpeg' } })
  }),

  http.delete('/api/attachments/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const att = getById(db.attachments, String(params.id))
    if (!att) return fail('ATTACHMENT_NOT_FOUND', '附件不存在', 404)
    if (att.bizId) return fail('ATTACHMENT_IN_USE', '附件已关联业务，禁止删除', 409)
    db.attachments = db.attachments.filter((a) => a.id !== att.id)
    return noContent()
  }),

  http.post('/api/print/inbound-order-qr', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const body = (await request.json()) as { inboundOrderId: string }
    const order = getById(db.inboundOrders, body.inboundOrderId)
    if (!order) return fail('ORDER_NOT_FOUND', '入库单不存在', 404)
    const job = printJob('INBOUND_ORDER_QR', [{
      labelType: 'D',
      content: `AWMS1:${JSON.stringify({ v: 1, t: 'D', ty: order.type, d: order.orderNo, wh: order.warehouseCode })}`,
      readableText: `单据：${order.orderNo}\n仓库：${order.warehouseCode}`,
      quantity: null,
    }], { bizType: 'INBOUND_ORDER', bizId: order.id }, auth)
    return remember(request, 201, job)
  }),

  http.post('/api/print/external-labels', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const body = (await request.json()) as { items: Array<{ materialId: string; count: number; inboundOrderLineId?: string; rt?: string; rc?: string }> }
    const items: PrintJobItem[] = []
    for (const reqItem of body.items) {
      const material = materialById(reqItem.materialId)
      if (!material) return fail('MATERIAL_NOT_FOUND', '物料不存在', 404)
      for (let i = 0; i < reqItem.count; i += 1) {
        items.push({
          labelType: 'S',
          content: `AWMS1:${JSON.stringify({ v: 1, t: 'S', s: material.code, ol: reqItem.inboundOrderLineId, rt: reqItem.rt, rc: reqItem.rc })}`,
          readableText: `物料：${material.code} ${material.name}`,
          quantity: null,
        })
      }
    }
    const job = printJob('EXTERNAL_LABEL', items, { bizType: 'INBOUND_ORDER_LINE', bizId: body.items[0]?.inboundOrderLineId ?? null }, auth, body.items.some((i) => i.count === 13))
    return remember(request, 201, job)
  }),

  http.post('/api/print/unique-labels', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const body = (await request.json()) as { inboundOrderLineId: string; count: number; qtyPerCode?: string }
    const found = findOrderLine(body.inboundOrderLineId)
    if (!found) return fail('ORDER_LINE_MISMATCH', '入库单行不存在', 400)
    if (found.order.status === 'VOIDED' || found.order.status === 'RECEIVED') return fail('ORDER_STATUS_INVALID', '当前状态不可生成唯一码', 409)
    const qtyPerCode = num(body.qtyPerCode ?? '1')
    if (body.count < 1 || body.count > 1000 || qtyPerCode <= 0) return fail('VALIDATION_ERROR', '数量参数不合法', 400)
    const registered = found.line.uniqueCodes.reduce((sum, uc) => sum + num(uc.quantity), 0)
    if (found.order.type === 'PO' && registered + body.count * qtyPerCode > num(found.line.expectedQty)) {
      return fail('VALIDATION_ERROR', '登记数量超出应到数量', 400)
    }
    const items: PrintJobItem[] = []
    for (let i = 0; i < body.count; i += 1) {
      uniqueSeq += 1
      const code = `BOX-20260821-${String(uniqueSeq).padStart(4, '0')}`
      found.line.uniqueCodes.push({ code, quantity: dec(qtyPerCode), status: 'UNRECEIVED', receivedAt: null })
      items.push({
        labelType: 'U',
        content: `AWMS1:${JSON.stringify({ v: 1, t: 'U', s: found.line.materialCode, u: code, q: dec(qtyPerCode) })}`,
        readableText: `唯一码：${code}\n物料：${found.line.materialCode}\n数量：${dec(qtyPerCode)}`,
        quantity: dec(qtyPerCode),
      })
    }
    const job = printJob('UNIQUE_LABEL', items, { bizType: 'INBOUND_ORDER_LINE', bizId: found.line.id }, auth, body.count === 13)
    return remember(request, 201, job)
  }),

  http.post('/api/print/batch-labels', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const body = (await request.json()) as { receiptLineId: string; qtyPerLabel?: string }
    const found = receiptLineForPrint(body.receiptLineId)
    if (isResponse(found)) return found
    const material = materialById(found.line.materialId)
    const qtyPerLabel = body.qtyPerLabel ?? material?.defaultQtyPerLabel ?? found.line.actualQty
    if (num(qtyPerLabel) <= 0) return fail('VALIDATION_ERROR', '每标签数量必须大于 0', 400)
    const parts = splitQuantities(found.line.actualQty, qtyPerLabel)
    const items = parts.map<PrintJobItem>((quantity) => ({
      labelType: 'B',
      content: `AWMS1:${JSON.stringify({ v: 1, t: 'B', s: found.line.materialCode, b: found.line.batchNo, q: quantity })}`,
      readableText: `物料：${found.line.materialCode} ${found.line.materialName}\n批次：${found.line.batchNo}\n数量：${quantity}\n库位：${found.receipt.stagingLocationCode}`,
      quantity,
    }))
    const job = printJob('BATCH_LABEL', items, { bizType: 'RECEIPT_LINE', bizId: found.line.id }, auth, num(qtyPerLabel) === 13)
    return remember(request, 201, job)
  }),

  http.post('/api/print/batch-label-one', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const body = (await request.json()) as { receiptLineId: string; quantity: string }
    const found = receiptLineForPrint(body.receiptLineId)
    if (isResponse(found)) return found
    const job = printJob('BATCH_LABEL_ONE', [{
      labelType: 'B',
      content: `AWMS1:${JSON.stringify({ v: 1, t: 'B', s: found.line.materialCode, b: found.line.batchNo, q: body.quantity })}`,
      readableText: `物料：${found.line.materialCode} ${found.line.materialName}\n批次：${found.line.batchNo}\n数量：${body.quantity}`,
      quantity: body.quantity,
    }], { bizType: 'RECEIPT_LINE', bizId: found.line.id }, auth)
    return remember(request, 201, job)
  }),

  http.post('/api/print/jobs/search', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const opts = await parseSearchBody(request, ['templateCode'], [{ field: 'createdAt', dir: 'desc' }])
    return ok(queryList(db.printJobs, opts))
  }),

  http.get('/api/print/jobs/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const job = getById(db.printJobs, String(params.id))
    if (!job) return fail('PRINT_JOB_NOT_FOUND', '打印作业不存在', 404)
    return ok(job)
  }),

  http.post('/api/print/jobs/:id/retry', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const hit = replay(request)
    if (hit) return hit
    const job = getById(db.printJobs, String(params.id))
    if (!job) return fail('PRINT_JOB_NOT_FOUND', '打印作业不存在', 404)
    if (job.status !== 'FAILED') return fail('PRINT_JOB_STATUS_INVALID', '当前打印作业不可重试', 409)
    job.status = 'READY'
    job.errorCode = null
    job.fileUrl = `/api/print/jobs/${job.id}/file`
    job.updatedAt = nowIso()
    return remember(request, 200, job)
  }),

  http.get('/api/print/jobs/:id/file', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (isResponse(auth)) return auth
    const job = getById(db.printJobs, String(params.id))
    if (!job) return fail('PRINT_JOB_NOT_FOUND', '打印作业不存在', 404)
    if (job.status === 'FAILED') return fail('PRINT_GENERATION_FAILED', '打印生成失败', 500)
    if (job.status !== 'READY' || !job.fileUrl) return fail('PRINT_JOB_NOT_READY', '打印文件未就绪', 409)
    return new HttpResponse(new Uint8Array([37, 80, 68, 70]), {
      status: 200,
      headers: { 'Content-Type': 'application/pdf' },
    })
  }),
]
