/**
 * 类型化 API 端点 —— 页面与测试只从这里 import。
 * 底层共用 client（MSW mock 拦截 fetch，VITE_USE_MOCK=false 时直达真实后端）。
 */
import { request } from './client'
import type {
  AttachmentItem, BatchItem, ExportRequest, FieldMeta, ImportTask, InboundOrder, InboundOrderCreateRequest,
  InboundOrderVoidRequest, ListQuery, LocationRecommendation, LoginRequest, LoginResponse,
  LocationCreateRequest, LocationItem, LocationUpdateRequest, MaterialCreateRequest, MaterialItem,
  MaterialUpdateRequest, PageResult, PrintJob, PutawayRecordCreateRequest, PutawayTodo, QualityCheckRequest,
  QualityExceptionItem, QualityResolveRequest, QualityTodo, Receipt, ReceiptCreateRequest, RefreshResponse,
  ScanParseRequest, ScanResult, SearchRequest, SourceCreateRequest, SourceItem, SourceUpdateRequest,
  WarehouseCreateRequest, WarehouseItem, WarehouseUpdateRequest,
} from './types'

// ── 认证 ──────────────────────────────────────────────
export const apiLogin = (body: LoginRequest) =>
  request<LoginResponse>('/auth/login', { method: 'POST', body, skipAuth: true })
export const apiRefresh = () => request<RefreshResponse>('/auth/refresh', { method: 'POST' })
export const apiLogout = () => request<void>('/auth/logout', { method: 'POST' })
export const apiMe = () => request<LoginResponse>('/auth/me')

// ── 运行时字段元数据 ───────────────────────────────────
export const apiMetaFields = (resource: string) =>
  request<FieldMeta[]>(`/meta/fields/${resource}`)

// ── 物料（列表标准查询 POST /search，通用规范 2.10 v1.9）──
export const apiListMaterials = (query: ListQuery) =>
  request<PageResult<MaterialItem>>('/materials/search', { method: 'POST', body: toSearchBody(query) })
/** 引用选择器快捷搜索：GET /api/materials?keyword=&pageSize=（轻量，pageSize≤10） */
export const apiQuickSearchMaterials = (keyword: string, pageSize = 10) =>
  request<PageResult<MaterialItem>>('/materials', { query: { keyword, pageSize } })
export const apiCreateMaterial = (body: MaterialCreateRequest, idempotencyKey: string) =>
  request<MaterialItem>('/materials', { method: 'POST', body, idempotencyKey })
export const apiUpdateMaterial = (id: string, body: MaterialUpdateRequest) =>
  request<void>(`/materials/${id}`, { method: 'PUT', body })
export const apiDeleteMaterial = (id: string) =>
  request<void>(`/materials/${id}`, { method: 'DELETE' })

// ── 仓库 ──────────────────────────────────────────────
export const apiListWarehouses = (query: ListQuery) =>
  request<PageResult<WarehouseItem>>('/warehouses/search', { method: 'POST', body: toSearchBody(query) })
export const apiQuickSearchWarehouses = (keyword: string, pageSize = 10) =>
  request<PageResult<WarehouseItem>>('/warehouses', { query: { keyword, pageSize } })
export const apiCreateWarehouse = (body: WarehouseCreateRequest, idempotencyKey: string) =>
  request<WarehouseItem>('/warehouses', { method: 'POST', body, idempotencyKey })
export const apiUpdateWarehouse = (id: string, body: WarehouseUpdateRequest) =>
  request<void>(`/warehouses/${id}`, { method: 'PUT', body })
export const apiDeleteWarehouse = (id: string) =>
  request<void>(`/warehouses/${id}`, { method: 'DELETE' })

// ── 库位（嵌套列表标准查询 POST .../locations/search）──
export const apiListLocations = (warehouseId: string, query: ListQuery) =>
  request<PageResult<LocationItem>>(`/warehouses/${warehouseId}/locations/search`, { method: 'POST', body: toSearchBody(query) })
export const apiCreateLocation = (warehouseId: string, body: LocationCreateRequest, idempotencyKey: string) =>
  request<LocationItem>(`/warehouses/${warehouseId}/locations`, { method: 'POST', body, idempotencyKey })
export const apiUpdateLocation = (id: string, body: LocationUpdateRequest) =>
  request<void>(`/locations/${id}`, { method: 'PUT', body })
export const apiDeleteLocation = (id: string) =>
  request<void>(`/locations/${id}`, { method: 'DELETE' })

// ── 来源 ──────────────────────────────────────────────
export const apiListSources = (query: ListQuery) =>
  request<PageResult<SourceItem>>('/sources/search', { method: 'POST', body: toSearchBody(query) })
export const apiQuickSearchSources = (keyword: string, pageSize = 10) =>
  request<PageResult<SourceItem>>('/sources', { query: { keyword, pageSize } })
export const apiCreateSource = (body: SourceCreateRequest, idempotencyKey: string) =>
  request<SourceItem>('/sources', { method: 'POST', body, idempotencyKey })
export const apiUpdateSource = (id: string, body: SourceUpdateRequest) =>
  request<void>(`/sources/${id}`, { method: 'PUT', body })
export const apiDeleteSource = (id: string) =>
  request<void>(`/sources/${id}`, { method: 'DELETE' })

// ── 批次（只读）────────────────────────────────────────
export const apiListBatches = (query: ListQuery) =>
  request<PageResult<BatchItem>>('/batches/search', { method: 'POST', body: toSearchBody(query) })
export const apiQuickSearchBatches = (keyword: string, pageSize = 10) =>
  request<PageResult<BatchItem>>('/batches', { query: { keyword, pageSize } })
export const apiGetBatch = (id: string) => request<BatchItem>(`/batches/${id}`)
export const apiListMaterialBatches = (materialId: string, query: ListQuery) =>
  request<PageResult<BatchItem>>(`/materials/${materialId}/batches/search`, { method: 'POST', body: toSearchBody(query) })

// ── 入库单 ────────────────────────────────────────────
export const apiCreateInboundOrder = (body: InboundOrderCreateRequest, idempotencyKey: string) =>
  request<InboundOrder>('/inbound-orders', { method: 'POST', body, idempotencyKey })
export const apiListInboundOrders = (query: ListQuery) =>
  request<PageResult<InboundOrder>>('/inbound-orders/search', { method: 'POST', body: toSearchBody(query) })
export const apiGetInboundOrder = (id: string) =>
  request<InboundOrder>(`/inbound-orders/${id}`)
export const apiVoidInboundOrder = (id: string, body: InboundOrderVoidRequest, idempotencyKey: string) =>
  request<InboundOrder>(`/inbound-orders/${id}/void`, { method: 'POST', body, idempotencyKey })

// ── 收货 / 质检 / 上架 ────────────────────────────────
export const apiCreateReceipt = (body: ReceiptCreateRequest, idempotencyKey: string) =>
  request<Receipt>('/receipts', { method: 'POST', body, idempotencyKey })
export const apiListReceipts = (query: ListQuery) =>
  request<PageResult<Receipt>>('/receipts/search', { method: 'POST', body: toSearchBody(query) })
export const apiGetReceipt = (id: string) =>
  request<Receipt>(`/receipts/${id}`)
export const apiPrintReceipt = (id: string, idempotencyKey: string) =>
  request<PrintJob>(`/receipts/${id}/print`, { method: 'POST', idempotencyKey })
export const apiListQualityTodos = (query: ListQuery) =>
  request<PageResult<QualityTodo>>('/quality-todos/search', { method: 'POST', body: toSearchBody(query) })
export const apiSubmitQualityCheck = (lineId: string, body: QualityCheckRequest, idempotencyKey: string) =>
  request<unknown>(`/receipt-lines/${lineId}/quality-check`, { method: 'POST', body, idempotencyKey })
export const apiListQualityExceptions = (query: ListQuery) =>
  request<PageResult<QualityExceptionItem>>('/quality-checks/search', { method: 'POST', body: toSearchBody(query) })
export const apiResolveQualityException = (checkId: string, body: QualityResolveRequest, idempotencyKey: string) =>
  request<unknown>(`/quality-checks/${checkId}/resolve`, { method: 'POST', body, idempotencyKey })
export const apiListPutawayTodos = (query: ListQuery) =>
  request<PageResult<PutawayTodo>>('/putaway-todos/search', { method: 'POST', body: toSearchBody(query) })
export const apiGetPutawayRecommendations = (receiptLineId: string) =>
  request<LocationRecommendation[]>(`/putaway-todos/${receiptLineId}/recommendations`)
export const apiCreatePutawayRecord = (body: PutawayRecordCreateRequest, idempotencyKey: string) =>
  request<unknown>('/putaway-records', { method: 'POST', body, idempotencyKey })

// ── 扫码 / 附件 / 打印 ────────────────────────────────
export const apiParseScan = (body: ScanParseRequest) =>
  request<ScanResult>('/scan/parse', { method: 'POST', body })
export const apiUploadAttachment = (file: File, bizType: string | undefined, idempotencyKey: string) => {
  const fd = new FormData()
  fd.append('file', file)
  if (bizType) fd.append('bizType', bizType)
  return request<AttachmentItem>('/attachments', { method: 'POST', formData: fd, idempotencyKey })
}
export const apiDeleteAttachment = (id: string, idempotencyKey: string) =>
  request<void>(`/attachments/${id}`, { method: 'DELETE', idempotencyKey })
export const apiFetchProtectedFile = (path: string) =>
  request<Response>(path, { blob: true })
export const apiListAttachments = (query: {
  bizType?: string
  bizId?: string
  uploadedBy?: string
  dateFrom?: string
  dateTo?: string
  page?: number
  pageSize?: number
}) => request<PageResult<AttachmentItem>>('/attachments', { query })
export const apiPrintInboundOrderQr = (inboundOrderId: string, idempotencyKey: string) =>
  request<PrintJob>('/print/inbound-order-qr', { method: 'POST', body: { inboundOrderId }, idempotencyKey })
export const apiPrintExternalLabels = (
  body: { items: Array<{ materialId: string; count: number; inboundOrderLineId?: string; rt?: 'S' | 'W'; rc?: string }> },
  idempotencyKey: string,
) => request<PrintJob>('/print/external-labels', { method: 'POST', body, idempotencyKey })
export const apiPrintUniqueLabels = (
  body: { inboundOrderLineId: string; count: number; qtyPerCode?: string },
  idempotencyKey: string,
) => request<PrintJob>('/print/unique-labels', { method: 'POST', body, idempotencyKey })
export const apiPrintBatchLabels = (body: { receiptLineId: string; qtyPerLabel?: string }, idempotencyKey: string) =>
  request<PrintJob>('/print/batch-labels', { method: 'POST', body, idempotencyKey })
export const apiPrintBatchLabelOne = (body: { receiptLineId: string; quantity: string }, idempotencyKey: string) =>
  request<PrintJob>('/print/batch-label-one', { method: 'POST', body, idempotencyKey })
export const apiSearchPrintJobs = (query: ListQuery) =>
  request<PageResult<PrintJob>>('/print/jobs/search', { method: 'POST', body: toSearchBody(query) })
export const apiGetPrintJob = (id: string) =>
  request<PrintJob>(`/print/jobs/${id}`)
export const apiRetryPrintJob = (id: string, idempotencyKey: string) =>
  request<PrintJob>(`/print/jobs/${id}/retry`, { method: 'POST', idempotencyKey })
export const apiDownloadPrintJobFile = (id: string) =>
  request<Response>(`/print/jobs/${id}/file`, { blob: true })

// ── 导入导出 ──────────────────────────────────────────
export const apiDownloadImportTemplate = (moduleCode: string) =>
  request<Response>(`/import-export/templates/${moduleCode}`, { blob: true })
export const apiPrecheckImport = (moduleCode: string, file: File) => {
  const fd = new FormData()
  fd.append('moduleCode', moduleCode)
  fd.append('file', file)
  return request<ImportTask>('/import-export/precheck', { method: 'POST', formData: fd })
}
export const apiExecuteImport = (taskId: string) =>
  request<ImportTask>('/import-export/execute', { method: 'POST', body: { taskId } })
export const apiCreateExportTask = (body: ExportRequest) =>
  request<ImportTask>('/import-export/export', { method: 'POST', body })
export const apiGetImportTask = (id: string) =>
  request<ImportTask>(`/import-export/tasks/${id}`)
export const apiDownloadTaskFile = (url: string) =>
  request<Response>(url, { blob: true })

/** ListQuery → POST /{resource}/search 请求体（通用规范 2.10 v1.9：filter/sort 走 body，不进 URL query） */
export function toSearchBody(query: ListQuery): SearchRequest {
  const body: SearchRequest = {}
  if (query.page !== undefined) body.page = query.page
  if (query.pageSize !== undefined) body.pageSize = query.pageSize
  if (query.keyword) body.keyword = query.keyword
  if (query.filter) body.filter = query.filter
  if (query.sort && query.sort.length > 0) body.sort = query.sort
  for (const [k, v] of Object.entries(query)) {
    if (['page', 'pageSize', 'keyword', 'filter', 'sort'].includes(k)) continue
    // 筛选空值不发送（通用规范 2.2）
    if (v === undefined || v === null || v === '') continue
    body[k] = v
  }
  return body
}
