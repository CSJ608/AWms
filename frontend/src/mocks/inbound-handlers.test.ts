import { beforeEach, describe, expect, it } from 'vitest'
import {
  apiCreatePutawayRecord, apiCreateReceipt, apiGetInboundOrder, apiListPutawayTodos, apiListQualityExceptions,
  apiListQualityTodos, apiPrintBatchLabels, apiPrintUniqueLabels, apiRetryPrintJob, apiSubmitQualityCheck,
  apiUploadAttachment,
} from '@/api'
import { seedSession } from '@/test/utils'

describe('MSW 入库链契约边界', () => {
  beforeEach(() => {
    seedSession()
  })

  it('PO 严格数量不一致时拦截且不产生收货', async () => {
    await expect(apiCreateReceipt({
      warehouseId: 'wh-01',
      stagingLocationId: 'loc-01',
      inboundOrderId: 'io-001',
      lines: [{
        orderLineId: 'iol-001',
        materialId: 'mat-001',
        batchProps: { sourceBatchNo: 'PRD-STRICT-FAIL', productionDate: '2026-08-21' },
        quantity: '199.0000',
      }],
    }, 'strict-fail')).rejects.toMatchObject({ code: 'QTY_MISMATCH_STRICT' })
  })

  it('唯一码按登记 quantity 守恒，重复唯一码二次提交被拦截', async () => {
    const receipt = await apiCreateReceipt({
      warehouseId: 'wh-01',
      stagingLocationId: 'loc-01',
      inboundOrderId: 'io-001',
      lines: [{
        orderLineId: 'iol-002',
        materialId: 'mat-004',
        batchProps: { sourceBatchNo: 'UNIQUE-OK', productionDate: '2026-08-21' },
        quantity: '10.0000',
        uniqueCodes: ['BOX-20260820-0001', 'BOX-20260820-0002'],
      }],
    }, 'unique-ok')
    expect(receipt.lines[0].actualQty).toBe('10.0000')

    await expect(apiCreateReceipt({
      warehouseId: 'wh-01',
      stagingLocationId: 'loc-01',
      inboundOrderId: 'io-001',
      lines: [{
        orderLineId: 'iol-002',
        materialId: 'mat-004',
        batchProps: { sourceBatchNo: 'UNIQUE-DUP', productionDate: '2026-08-21' },
        quantity: '10.0000',
        uniqueCodes: ['BOX-20260820-0001', 'BOX-20260820-0002'],
      }],
    }, 'unique-dup')).rejects.toMatchObject({ code: 'UNIQUE_CODE_ALREADY_RECEIVED' })
  })

  it('附件失败、异常处理和 REJECT 后不上架', async () => {
    await expect(apiUploadAttachment(new File(['bad'], 'bad.txt', { type: 'text/plain' })))
      .rejects.toMatchObject({ code: 'ATTACHMENT_TYPE_INVALID' })

    const photo = await apiUploadAttachment(new File(['ok'], 'exception.jpg', { type: 'image/jpeg' }), 'EXCEPTION')
    await apiSubmitQualityCheck('rl-005', {
      result: 'EXCEPTION',
      checkedQty: '12.0000',
      exceptionReason: 'DAMAGED',
      note: '外箱破损',
      photoIds: [photo.id],
    }, 'qc-exception')

    const exceptions = await apiListQualityExceptions({ resolutionStatus: 'PENDING', keyword: 'RCP-20260819-0005', page: 1, pageSize: 10 })
    expect(exceptions.items).toHaveLength(1)

    const putawayBeforeResolve = await apiListPutawayTodos({ batchId: 'b-03', page: 1, pageSize: 10 })
    expect(putawayBeforeResolve.items.some((item) => item.receiptLineId === 'rl-005')).toBe(false)
  })

  it('非法库位和 VERSION_CONFLICT 均按契约返回', async () => {
    await expect(apiCreatePutawayRecord({
      receiptLineId: 'rl-002',
      toLocationId: 'loc-02',
      scannedLocationCode: 'STG-01',
      expectedInventoryVersion: 3,
    }, 'putaway-invalid-location')).rejects.toMatchObject({ code: 'PUTAWAY_LOCATION_INVALID' })

    await expect(apiCreatePutawayRecord({
      receiptLineId: 'rl-002',
      toLocationId: 'loc-02',
      scannedLocationCode: 'DEF-01',
      expectedInventoryVersion: 2,
    }, 'putaway-version-conflict')).rejects.toMatchObject({ code: 'VERSION_CONFLICT' })
  })

  it('打印失败作业可重试；生成类端点同一幂等键不重复登记唯一码', async () => {
    const failed = await apiPrintBatchLabels({ receiptLineId: 'rl-002', qtyPerLabel: '13.0000' }, 'print-fail')
    expect(failed.status).toBe('FAILED')
    expect(failed.errorCode).toBe('PRINT_GENERATION_FAILED')
    const retried = await apiRetryPrintJob(failed.id, 'print-retry')
    expect(retried.status).toBe('READY')

    const first = await apiPrintUniqueLabels({ inboundOrderLineId: 'iol-001', count: 1, qtyPerCode: '1.0000' }, 'idem-unique')
    const second = await apiPrintUniqueLabels({ inboundOrderLineId: 'iol-001', count: 1, qtyPerCode: '1.0000' }, 'idem-unique')
    expect(second.id).toBe(first.id)
    const order = await apiGetInboundOrder('io-001')
    expect(order.lines.find((line) => line.id === 'iol-001')?.uniqueCodes).toHaveLength(1)
  })

  it('同一质检幂等键重放只处理一次', async () => {
    await apiSubmitQualityCheck('rl-005', { result: 'PASS', checkedQty: '12.0000' }, 'qc-idem')
    await apiSubmitQualityCheck('rl-005', { result: 'PASS', checkedQty: '12.0000' }, 'qc-idem')
    const todos = await apiListQualityTodos({ receiptLineId: 'rl-005', page: 1, pageSize: 10 })
    expect(todos.items.some((item) => item.receiptLineId === 'rl-005')).toBe(false)
  })
})

