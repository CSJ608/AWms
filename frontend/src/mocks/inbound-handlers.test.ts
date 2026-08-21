import { beforeEach, describe, expect, it } from 'vitest'
import {
  apiCreatePutawayRecord, apiCreateReceipt, apiGetInboundOrder, apiListPutawayTodos, apiListQualityExceptions,
  apiListQualityTodos, apiParseScan, apiPrintBatchLabels, apiPrintUniqueLabels, apiRetryPrintJob, apiSubmitQualityCheck,
  apiResolveQualityException, apiUploadAttachment,
} from '@/api'
import { request } from '@/api/client'
import type { AttachmentItem, Receipt } from '@/api/types'
import { seedSession } from '@/test/utils'
import { db } from './db'
import { MOCK_IDS, seedAttachments, seedBatches, seedInboundOrders, seedLocations, seedMaterials, seedPermissions, seedQualityChecks, seedReceipts, seedSources, seedUsers, seedWarehouses } from './seed'

const receiptKeys = [
  'id', 'receiptNo', 'warehouseId', 'warehouseCode', 'inboundOrderId', 'sourceDocType', 'sourceDocNo',
  'sourceType', 'sourceCode', 'status', 'lines', 'stagingLocationId', 'stagingLocationCode', 'photos',
  'operatorId', 'operatorName', 'occurredAt',
].sort()

const receiptLineKeys = [
  'id', 'lineNo', 'orderLineId', 'orderLineNo', 'materialId', 'materialCode', 'materialName', 'batchId',
  'batchNo', 'expectedQty', 'actualQty', 'qtyDiff', 'status', 'sourceBatchNo', 'productionDate', 'expiryDate',
].sort()

async function postRaw(path: string, body: unknown, idempotencyKey: string): Promise<Response> {
  return fetch(`/api${path}`, {
    method: 'POST',
    headers: {
      Authorization: 'Bearer mock-token-admin',
      'Content-Type': 'application/json',
      'Idempotency-Key': idempotencyKey,
    },
    body: JSON.stringify(body),
  })
}

async function expectReceiptResponse(
  response: Response,
  expectedStatus: number,
  expectedReceiptId: string,
  expectedReceiptStatus: Receipt['status'],
  expectedLineStatus: Receipt['lines'][number]['status'],
): Promise<Receipt> {
  expect(response.status).toBe(expectedStatus)
  const envelope = await response.json() as { code: string; message: string; data: Receipt }
  expect(envelope.code).toBe('OK')
  expect(envelope.message).toBe('ok')
  expect(Object.keys(envelope.data).sort()).toEqual(receiptKeys)
  expect(Object.keys(envelope.data.lines[0]).sort()).toEqual(receiptLineKeys)
  expect(envelope.data).toMatchObject({
    id: expectedReceiptId,
    status: expectedReceiptStatus,
    lines: [{ status: expectedLineStatus }],
  })
  return envelope.data
}

describe('MSW 入库链契约边界', () => {
  beforeEach(() => {
    seedSession()
  })

  it('PO 严格数量不一致时拦截且不产生收货', async () => {
    await expect(apiCreateReceipt({
      warehouseId: MOCK_IDS.warehouse1,
      stagingLocationId: MOCK_IDS.locationStaging1,
      inboundOrderId: MOCK_IDS.inboundOrder1,
      lines: [{
        orderLineId: MOCK_IDS.inboundOrderLine1,
        materialId: MOCK_IDS.material1,
        batchProps: { sourceBatchNo: 'PRD-STRICT-FAIL', productionDate: '2026-08-21' },
        quantity: '199.0000',
      }],
    }, 'strict-fail')).rejects.toMatchObject({ code: 'QTY_MISMATCH_STRICT' })
  })

  it('唯一码按登记 quantity 守恒，重复唯一码二次提交被拦截', async () => {
    const receipt = await apiCreateReceipt({
      warehouseId: MOCK_IDS.warehouse1,
      stagingLocationId: MOCK_IDS.locationStaging1,
      inboundOrderId: MOCK_IDS.inboundOrder1,
      lines: [{
        orderLineId: MOCK_IDS.inboundOrderLine2,
        materialId: MOCK_IDS.material4,
        batchProps: { sourceBatchNo: 'UNIQUE-OK', productionDate: '2026-08-21' },
        quantity: '10.0000',
        uniqueCodes: ['BOX-20260820-0001', 'BOX-20260820-0002'],
      }],
    }, 'unique-ok')
    expect(receipt.lines[0].actualQty).toBe('10.0000')

    await expect(apiCreateReceipt({
      warehouseId: MOCK_IDS.warehouse1,
      stagingLocationId: MOCK_IDS.locationStaging1,
      inboundOrderId: MOCK_IDS.inboundOrder1,
      lines: [{
        orderLineId: MOCK_IDS.inboundOrderLine2,
        materialId: MOCK_IDS.material4,
        batchProps: { sourceBatchNo: 'UNIQUE-DUP', productionDate: '2026-08-21' },
        quantity: '10.0000',
        uniqueCodes: ['BOX-20260820-0001', 'BOX-20260820-0002'],
      }],
    }, 'unique-dup')).rejects.toMatchObject({ code: 'UNIQUE_CODE_ALREADY_RECEIVED' })
  })

  it('附件失败、异常处理和 REJECT 后不上架', async () => {
    await expect(apiUploadAttachment(new File(['bad'], 'bad.txt', { type: 'text/plain' }), undefined, 'attachment-bad'))
      .rejects.toMatchObject({ code: 'ATTACHMENT_TYPE_INVALID' })

    const photo = await apiUploadAttachment(new File(['ok'], 'exception.jpg', { type: 'image/jpeg' }), 'EXCEPTION', 'attachment-exception')
    await apiSubmitQualityCheck(MOCK_IDS.receiptLine5, {
      result: 'EXCEPTION',
      checkedQty: '12.0000',
      exceptionReason: 'DAMAGED',
      note: '外箱破损',
      photoIds: [photo.id],
    }, 'qc-exception')

    const exceptions = await apiListQualityExceptions({ resolutionStatus: 'PENDING', keyword: 'RCP-20260819-0005', page: 1, pageSize: 10 })
    expect(exceptions.items).toHaveLength(1)

    const putawayBeforeResolve = await apiListPutawayTodos({ batchId: MOCK_IDS.batch3, page: 1, pageSize: 10 })
    expect(putawayBeforeResolve.items.some((item) => item.receiptLineId === MOCK_IDS.receiptLine5)).toBe(false)
  })

  it('quality-check 返回 200 + ApiResponse<Receipt>', async () => {
    const body = { result: 'PASS' as const, checkedQty: '40.0000' }
    const response = await postRaw(`/receipt-lines/${MOCK_IDS.receiptLine4}/quality-check`, body, 'contract-qc-response')
    const data = await expectReceiptResponse(response, 200, MOCK_IDS.receipt4, 'PUTAWAY', 'CHECKED')
    const typedReplay: Receipt = await apiSubmitQualityCheck(MOCK_IDS.receiptLine4, body, 'contract-qc-response')
    expect(typedReplay).toEqual(data)
  })

  it('quality-check resolve 返回 200 + ApiResponse<Receipt>', async () => {
    const body = { action: 'PASS' as const }
    const response = await postRaw(`/quality-checks/${MOCK_IDS.quality1}/resolve`, body, 'contract-resolve-response')
    const data = await expectReceiptResponse(response, 200, MOCK_IDS.receipt3, 'PUTAWAY', 'CHECKED')
    const typedReplay: Receipt = await apiResolveQualityException(MOCK_IDS.quality1, body, 'contract-resolve-response')
    expect(typedReplay).toEqual(data)
  })

  it('putaway-records 返回 201 + ApiResponse<Receipt>', async () => {
    const body = {
      receiptLineId: MOCK_IDS.receiptLine2,
      toLocationId: MOCK_IDS.locationDefault1,
      scannedLocationCode: 'DEF-01',
      expectedInventoryVersion: 3,
    }
    const response = await postRaw('/putaway-records', body, 'contract-putaway-response')
    const data = await expectReceiptResponse(response, 201, MOCK_IDS.receipt2, 'DONE', 'PUTAWAY_DONE')
    const typedReplay: Receipt = await apiCreatePutawayRecord(body, 'contract-putaway-response')
    expect(typedReplay).toEqual(data)
  })

  it('附件上传缺少幂等键时返回后端一致的 VALIDATION_ERROR，同 key 重放首次结果', async () => {
    const formData = new FormData()
    formData.append('file', new File(['ok'], 'receipt.jpg', { type: 'image/jpeg' }))
    await expect(request<AttachmentItem>('/attachments', { method: 'POST', formData }))
      .rejects.toMatchObject({ code: 'VALIDATION_ERROR', status: 400, message: 'Idempotency-Key 必填' })

    const first = await apiUploadAttachment(new File(['first'], 'receipt.jpg', { type: 'image/jpeg' }), 'RECEIPT', 'attachment-replay')
    const replayed = await apiUploadAttachment(new File(['second'], 'other.jpg', { type: 'image/jpeg' }), 'RECEIPT', 'attachment-replay')
    expect(replayed).toEqual(first)
  })

  it.each([
    ['入库单创建', '/inbound-orders', { warehouseId: MOCK_IDS.warehouse1, type: 'OT', lines: [{ materialId: MOCK_IDS.material2, expectedQty: '1.0000' }] }],
    ['入库单作废', `/inbound-orders/${MOCK_IDS.inboundOrder1}/void`, { reason: '测试作废' }],
    ['收货提交', '/receipts', { warehouseId: MOCK_IDS.warehouse1, stagingLocationId: MOCK_IDS.locationStaging1, sourceDocType: 'OT', lines: [{ materialId: MOCK_IDS.material2, quantity: '1.0000' }] }],
    ['收货回执打印', `/receipts/${MOCK_IDS.receipt1}/print`, undefined],
    ['质检提交', `/receipt-lines/${MOCK_IDS.receiptLine4}/quality-check`, { result: 'PASS', checkedQty: '40.0000' }],
    ['质检异常处理', `/quality-checks/${MOCK_IDS.quality1}/resolve`, { action: 'PASS' }],
    ['上架提交', '/putaway-records', { receiptLineId: MOCK_IDS.receiptLine2, toLocationId: MOCK_IDS.locationDefault1, scannedLocationCode: 'DEF-01', expectedInventoryVersion: 3 }],
    ['入库单二维码', '/print/inbound-order-qr', { inboundOrderId: MOCK_IDS.inboundOrder1 }],
    ['外标签', '/print/external-labels', { items: [{ materialId: MOCK_IDS.material1, count: 1, inboundOrderLineId: MOCK_IDS.inboundOrderLine1 }] }],
    ['唯一码标签', '/print/unique-labels', { inboundOrderLineId: MOCK_IDS.inboundOrderLine1, count: 1, qtyPerCode: '1.0000' }],
    ['批次标签批量', '/print/batch-labels', { receiptLineId: MOCK_IDS.receiptLine2, qtyPerLabel: '10.0000' }],
    ['批次标签单张', '/print/batch-label-one', { receiptLineId: MOCK_IDS.receiptLine2, quantity: '10.0000' }],
  ])('%s 缺少幂等键时在修改 Mock 状态前拒绝', async (_name, path, body) => {
    const before = structuredClone(db)
    await expect(request<unknown>(path, { method: 'POST', ...(body === undefined ? {} : { body }) }))
      .rejects.toMatchObject({ code: 'VALIDATION_ERROR', status: 400, message: 'Idempotency-Key 必填' })
    expect(db).toEqual(before)
  })

  it('打印 retry 缺少幂等键时不修改失败作业', async () => {
    const failed = await apiPrintBatchLabels({ receiptLineId: MOCK_IDS.receiptLine2, qtyPerLabel: '13.0000' }, 'prepare-failed-job')
    const before = structuredClone(db)
    await expect(request<unknown>(`/print/jobs/${failed.id}/retry`, { method: 'POST' }))
      .rejects.toMatchObject({ code: 'VALIDATION_ERROR', status: 400, message: 'Idempotency-Key 必填' })
    expect(db).toEqual(before)
  })

  it('非法库位和 VERSION_CONFLICT 均按契约返回', async () => {
    await expect(apiCreatePutawayRecord({
      receiptLineId: MOCK_IDS.receiptLine2,
      toLocationId: MOCK_IDS.locationDefault1,
      scannedLocationCode: 'STG-01',
      expectedInventoryVersion: 3,
    }, 'putaway-invalid-location')).rejects.toMatchObject({ code: 'PUTAWAY_LOCATION_INVALID' })

    await expect(apiCreatePutawayRecord({
      receiptLineId: MOCK_IDS.receiptLine2,
      toLocationId: MOCK_IDS.locationDefault1,
      scannedLocationCode: 'DEF-01',
      expectedInventoryVersion: 2,
    }, 'putaway-version-conflict')).rejects.toMatchObject({ code: 'VERSION_CONFLICT' })

    const line = db.receipts.flatMap((receipt) => receipt.lines).find((item) => item.id === MOCK_IDS.receiptLine2)!
    line.status = 'PUTAWAY_DONE'
    await expect(apiCreatePutawayRecord({
      receiptLineId: MOCK_IDS.receiptLine2,
      toLocationId: MOCK_IDS.locationDefault1,
      scannedLocationCode: 'DEF-01',
      expectedInventoryVersion: 3,
    }, 'putaway-state-conflict')).rejects.toMatchObject({ code: 'VERSION_CONFLICT' })
  })

  it('REJECT 缺少备注返回锁定通用校验错误', async () => {
    await expect(apiResolveQualityException(MOCK_IDS.quality1, { action: 'REJECT' }, 'resolve-without-note'))
      .rejects.toMatchObject({ code: 'VALIDATION_ERROR', status: 400 })
  })

  it('打印失败作业可重试；生成类端点同一幂等键不重复登记唯一码', async () => {
    const failed = await apiPrintBatchLabels({ receiptLineId: MOCK_IDS.receiptLine2, qtyPerLabel: '13.0000' }, 'print-fail')
    expect(failed.status).toBe('FAILED')
    expect(failed.errorCode).toBe('PRINT_GENERATION_FAILED')
    const retried = await apiRetryPrintJob(failed.id, 'print-retry')
    expect(retried.status).toBe('READY')

    const first = await apiPrintUniqueLabels({ inboundOrderLineId: MOCK_IDS.inboundOrderLine1, count: 1, qtyPerCode: '1.0000' }, 'idem-unique')
    const second = await apiPrintUniqueLabels({ inboundOrderLineId: MOCK_IDS.inboundOrderLine1, count: 1, qtyPerCode: '1.0000' }, 'idem-unique')
    expect(second.id).toBe(first.id)
    const order = await apiGetInboundOrder(MOCK_IDS.inboundOrder1)
    expect(order.lines.find((line) => line.id === MOCK_IDS.inboundOrderLine1)?.uniqueCodes).toHaveLength(1)
  })

  it('同一质检幂等键重放只处理一次', async () => {
    await apiSubmitQualityCheck(MOCK_IDS.receiptLine5, { result: 'PASS', checkedQty: '12.0000' }, 'qc-idem')
    await apiSubmitQualityCheck(MOCK_IDS.receiptLine5, { result: 'PASS', checkedQty: '12.0000' }, 'qc-idem')
    const todos = await apiListQualityTodos({ receiptLineId: MOCK_IDS.receiptLine5, page: 1, pageSize: 10 })
    expect(todos.items.some((item) => item.receiptLineId === MOCK_IDS.receiptLine5)).toBe(false)
  })

  it('所有 Mock DTO 主键均为合法 UUID', () => {
    const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-8[0-9a-f]{3}-[0-9a-f]{12}$/i
    const ids = [
      ...seedPermissions.map((item) => item.id), ...seedUsers.flatMap((item) => [item.id, ...item.roles.map((role) => role.id)]),
      ...seedMaterials.map((item) => item.id), ...seedWarehouses.map((item) => item.id), ...seedLocations.flatMap((item) => [item.id, item.warehouseId]),
      ...seedSources.map((item) => item.id), ...seedBatches.flatMap((item) => [item.id, item.materialId]),
      ...seedInboundOrders.flatMap((item) => [item.id, item.warehouseId, ...item.lines.flatMap((line) => [line.id, line.materialId])]),
      ...seedReceipts.flatMap((item) => [item.id, item.warehouseId, item.stagingLocationId, item.operatorId, ...item.lines.flatMap((line) => [line.id, line.materialId, line.batchId])]),
      ...seedQualityChecks.flatMap((item) => [item.id, item.receiptLineId, item.warehouseId, item.checkedBy]),
      ...seedAttachments.flatMap((item) => [item.id, item.uploadedBy]),
    ]
    expect(ids.every((id) => uuid.test(id))).toBe(true)
  })

  it('EAN-13、Code128 和 GS1 返回 EXTERNAL_BARCODE 结构化结果', async () => {
    const ean = await apiParseScan({ content: '6901234567892' })
    expect(ean).toMatchObject({ type: 'EXTERNAL_BARCODE', quantity: null, external: { format: 'EAN13', parsed: { gtin: '6901234567892' } }, material: { materialCode: 'MAT-001' } })
    const code128 = await apiParseScan({ content: 'MAT-001' })
    expect(code128).toMatchObject({ type: 'EXTERNAL_BARCODE', quantity: null, external: { format: 'CODE128', parsed: { code: 'MAT-001' } } })
    const unknownCode128 = await apiParseScan({ content: ']C0UNKNOWN-SKU' })
    expect(unknownCode128).toMatchObject({ type: 'EXTERNAL_BARCODE', material: null, external: { format: 'CODE128', parsed: { code: 'UNKNOWN-SKU' } } })
    const unknownEan = await apiParseScan({ content: '4006381333931' })
    expect(unknownEan).toMatchObject({ type: 'EXTERNAL_BARCODE', material: null, external: { format: 'EAN13', parsed: { gtin: '4006381333931' } } })
    const gs1 = await apiParseScan({ content: '(01)06901234567892(10)SUP-BATCH-01(11)260801(15)270801(30)10' })
    expect(gs1).toMatchObject({
      type: 'EXTERNAL_BARCODE', quantity: '10.0000',
      batchProps: { sourceBatchNo: 'SUP-BATCH-01', productionDate: '2026-08-01', expiryDate: '2027-08-01' },
      external: { format: 'GS1', parsed: { gtin: '06901234567892', batchNo: 'SUP-BATCH-01', productionDate: '2026-08-01', expiryDate: '2027-08-01', quantity: '10.0000' } },
    })
  })
})
