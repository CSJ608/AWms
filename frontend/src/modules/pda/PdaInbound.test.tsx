import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderApp, seedSession, makeOperatorSession, makeSupervisorSession } from '@/test/utils'
import { server } from '@/mocks/server'
import { MOCK_IDS } from '@/mocks/seed'
import { db } from '@/mocks/db'
import { seedAttachments, seedReceipts } from '@/mocks/seed'

const label = (payload: Record<string, unknown>) => `AWMS1:${JSON.stringify(payload)}`

async function scan(user: ReturnType<typeof userEvent.setup>, content: string) {
  const input = await screen.findByTestId('pda-scan-input')
  await user.clear(input)
  fireEvent.change(input, { target: { value: content } })
  await user.click(screen.getByTestId('pda-scan-submit'))
}

describe('PDA 菜单与入库作业', () => {
  it('收货、质检、上架角色均可看到对应 PDA 入口', async () => {
    seedSession(makeOperatorSession())
    renderApp('/pda')

    const menu = await screen.findByTestId('pda-menu')
    expect(within(menu).getByRole('button', { name: '收货' })).toBeInTheDocument()
    expect(within(menu).getByRole('button', { name: '质检' })).toBeInTheDocument()
    expect(within(menu).getByRole('button', { name: '上架' })).toBeInTheDocument()
  })

  it('PDA 菜单同时受 menus.pda 与 action 权限过滤', async () => {
    const supervisor = makeSupervisorSession()
    const receivingOnly = {
      ...supervisor,
      permissions: ['route.inbound', 'action.receiving.create'],
      menus: {
        ...supervisor.menus,
        pda: [
          { code: 'receiving', titleKey: 'pda.receiving', moduleCode: 'inbound', sort: 10 },
          { code: 'qc', titleKey: 'pda.qc', moduleCode: 'inbound', sort: 20 },
          { code: 'putaway', titleKey: 'pda.putaway', moduleCode: 'inbound', sort: 30 },
        ],
      },
    }
    seedSession(receivingOnly)
    server.use(http.get('/api/auth/me', () => HttpResponse.json({ code: 'OK', message: 'ok', data: receivingOnly })))
    renderApp('/pda')
    const menu = await screen.findByTestId('pda-menu')
    expect(within(menu).getByRole('button', { name: '收货' })).toBeInTheDocument()
    expect(within(menu).queryByRole('button', { name: '质检' })).not.toBeInTheDocument()
    expect(within(menu).queryByRole('button', { name: '上架' })).not.toBeInTheDocument()
  })

  it('收货扫 t=D 直接使用完整上下文，唯一码按 quantity 累加并防本地重复', async () => {
    seedSession(makeOperatorSession())
    const spy = vi.spyOn(globalThis, 'fetch')
    renderApp('/pda/receiving')
    const user = userEvent.setup()

    await scan(user, label({ v: 1, t: 'D', ty: 'PO', d: 'PO-20260819-0001', wh: 'WH-01' }))
    expect(await screen.findByText(/PO-20260819-0001/)).toBeInTheDocument()
    expect(spy.mock.calls.some(([url]) => String(url).includes('/api/inbound-orders/search'))).toBe(false)

    await user.selectOptions(screen.getByLabelText('单据行'), MOCK_IDS.inboundOrderLine2)
    expect(screen.getByTestId('receiving-qty')).toHaveValue('0.0000')

    await scan(user, label({ v: 1, t: 'U', s: 'MAT-004', u: 'BOX-20260820-0001', q: '5.0000' }))
    expect(await screen.findByTestId('receiving-qty')).toHaveValue('5.0000')
    await scan(user, label({ v: 1, t: 'U', s: 'MAT-004', u: 'BOX-20260820-0002', q: '5.0000' }))
    expect(screen.getByTestId('receiving-qty')).toHaveValue('10.0000')

    await scan(user, label({ v: 1, t: 'U', s: 'MAT-004', u: 'BOX-20260820-0002', q: '5.0000' }))
    expect(await screen.findByText('已收过')).toBeInTheDocument()
    spy.mockRestore()
  })

  it('收货照片上传失败保留当前输入并提示附件错误', async () => {
    seedSession(makeOperatorSession())
    renderApp('/pda/receiving')
    const user = userEvent.setup()

    await scan(user, label({ v: 1, t: 'D', ty: 'PO', d: 'PO-20260819-0001', wh: 'WH-01' }))
    await screen.findByText(/PO-20260819-0001/)
    await user.selectOptions(screen.getByLabelText('单据行'), MOCK_IDS.inboundOrderLine1)
    await scan(user, label({ v: 1, t: 'S', s: 'MAT-001', q: '200.0000', rb: 'REAL-BATCH-01', pd: '2026-08-20' }))
    await user.click(screen.getByTestId('review-receipt'))
    await screen.findByTestId('receiving-confirmation')

    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    fireEvent.change(fileInput, { target: { files: [new File(['bad'], 'bad.txt', { type: 'text/plain' })] } })
    expect(await screen.findByText('附件类型不支持')).toBeInTheDocument()
    expect(screen.getByTestId('attachment-upload-failed')).toBeInTheDocument()
    expect(screen.getByTestId('submit-receipt')).toBeDisabled()
    expect(screen.getByText(/PO-20260819-0001/)).toBeInTheDocument()
  })

  it('附件失败重传复用稳定 key，可删除并重新选择同一文件', async () => {
    seedSession(makeOperatorSession())
    const keys: string[] = []
    let uploads = 0
    server.use(
      http.post('/api/attachments', async ({ request }) => {
        uploads += 1
        keys.push(request.headers.get('Idempotency-Key') ?? '')
        if (uploads === 1) return HttpResponse.json({ code: 'INTERNAL_ERROR', message: '上传暂时失败', data: null }, { status: 500 })
        return HttpResponse.json({ code: 'OK', message: 'ok', data: { ...seedAttachments[0], bizType: null, bizId: null, fileName: 'receipt.jpg' } }, { status: 201 })
      }),
      http.delete(`/api/attachments/${MOCK_IDS.attachment1}`, () => new HttpResponse(null, { status: 204 })),
    )
    renderApp('/pda/receiving')
    const user = userEvent.setup()

    await scan(user, label({ v: 1, t: 'D', ty: 'PO', d: 'PO-20260819-0001', wh: 'WH-01' }))
    await screen.findByText(/PO-20260819-0001/)
    await user.selectOptions(screen.getByLabelText('单据行'), MOCK_IDS.inboundOrderLine1)
    await scan(user, label({ v: 1, t: 'S', s: 'MAT-001', q: '200.0000', rb: 'REAL-BATCH-01', pd: '2026-08-20' }))
    await user.click(screen.getByTestId('review-receipt'))

    const file = new File(['photo'], 'receipt.jpg', { type: 'image/jpeg' })
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    fireEvent.change(fileInput, { target: { files: [file] } })
    await screen.findByText('上传暂时失败')
    await user.click(screen.getByRole('button', { name: '重传 receipt.jpg' }))
    await screen.findByRole('img', { name: 'receipt.jpg' })
    expect(keys[0]).toBe(keys[1])

    await user.click(screen.getByRole('button', { name: '删除 receipt.jpg' }))
    await waitFor(() => expect(screen.queryByRole('img', { name: 'receipt.jpg' })).not.toBeInTheDocument())
    fireEvent.change(fileInput, { target: { files: [file] } })
    await screen.findByRole('img', { name: 'receipt.jpg' })
    expect(uploads).toBe(3)
    expect(keys[2]).not.toBe(keys[1])
  })

  it('质检任务 A 并发失效后隔离草稿，删除失败可重试且任务 B 不继承附件', async () => {
    seedSession(makeOperatorSession())
    let deleteAttempts = 0
    const submitted: Array<{ lineId: string; body: { exceptionReason?: string; note?: string | null; photoIds?: string[] } }> = []
    server.use(
      http.post('/api/receipt-lines/:lineId/quality-check', async ({ request, params }) => {
        const lineId = String(params.lineId)
        if (lineId === MOCK_IDS.receiptLine4) {
          const line = db.receipts.flatMap((receipt) => receipt.lines).find((item) => item.id === lineId)!
          line.status = 'CHECKED'
          return HttpResponse.json({ code: 'QC_STATUS_INVALID', message: '该行已质检', data: null }, { status: 409 })
        }
        submitted.push({ lineId, body: await request.json() as { exceptionReason?: string; note?: string | null; photoIds?: string[] } })
        return new HttpResponse(null, { status: 204 })
      }),
      http.delete('/api/attachments/:id', ({ params }) => {
        deleteAttempts += 1
        if (deleteAttempts === 1) {
          return HttpResponse.json({ code: 'INTERNAL_ERROR', message: '清理暂时失败', data: null }, { status: 500 })
        }
        db.attachments = db.attachments.filter((item) => item.id !== String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderApp('/pda/qc')
    const user = userEvent.setup()

    await user.click(await screen.findByText('RCP-20260819-0004'))
    await user.click(screen.getByRole('button', { name: '上报异常' }))
    await user.selectOptions(screen.getByRole('combobox'), 'OTHER')
    await user.type(screen.getByPlaceholderText('备注'), '任务 A 外箱异常')
    const firstInput = document.querySelector('input[type="file"]') as HTMLInputElement
    fireEvent.change(firstInput, { target: { files: [new File(['a'], 'task-a.jpg', { type: 'image/jpeg' })] } })
    await screen.findByRole('img', { name: 'task-a.jpg' })
    const taskAAttachmentId = db.attachments.find((item) => item.fileName === 'task-a.jpg')!.id

    await user.click(screen.getByTestId('quality-exception-submit'))
    expect(await screen.findByText('该行已质检')).toBeInTheDocument()
    expect(await screen.findByText('清理暂时失败')).toBeInTheDocument()

    await user.click(screen.getByText('RCP-20260819-0005'))
    await user.click(screen.getByRole('button', { name: '上报异常' }))
    expect(screen.getByRole('combobox')).toHaveValue('DAMAGED')
    expect(screen.getByPlaceholderText('备注')).toHaveValue('')
    expect(screen.getByText('照片 0/3')).toBeInTheDocument()
    expect(screen.queryByRole('img', { name: 'task-a.jpg' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '重试清理' }))
    await waitFor(() => expect(screen.queryByTestId('attachment-cleanup')).not.toBeInTheDocument())
    expect(db.attachments.some((item) => item.id === taskAAttachmentId)).toBe(false)

    const secondInput = document.querySelector('input[type="file"]') as HTMLInputElement
    fireEvent.change(secondInput, { target: { files: [new File(['b'], 'task-b.jpg', { type: 'image/jpeg' })] } })
    await screen.findByRole('img', { name: 'task-b.jpg' })
    const taskBAttachmentId = db.attachments.find((item) => item.fileName === 'task-b.jpg')!.id
    await user.click(screen.getByTestId('quality-exception-submit'))
    await screen.findByText('异常已上报')

    expect(submitted).toEqual([{
      lineId: MOCK_IDS.receiptLine5,
      body: expect.objectContaining({ exceptionReason: 'DAMAGED', note: null, photoIds: [taskBAttachmentId] }),
    }])
    expect(submitted[0].body.photoIds).not.toContain(taskAAttachmentId)
  })

  it('质检扫批次标签按多条选择、1 条直达、0 条提示处理', async () => {
    seedSession(makeOperatorSession())
    renderApp('/pda/qc')
    const user = userEvent.setup()

    await scan(user, label({ v: 1, t: 'B', s: 'MAT-001', b: '260810001', q: '10.0000' }))
    expect(await screen.findByText('选择待质检任务')).toBeInTheDocument()
    expect(screen.getAllByText(/RCP-20260819-0004/).length).toBeGreaterThan(0)

    await scan(user, label({ v: 1, t: 'B', s: 'MAT-002', b: '260809001', q: '12.0000' }))
    expect(await screen.findByTestId('quality-pass')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '返回' }))
    await user.click(screen.getByRole('button', { name: '质检' }))
    await scan(user, label({ v: 1, t: 'B', s: 'MAT-001', b: '260810002', q: '10.0000' }))
    expect(await screen.findByText('未找到待质检任务')).toBeInTheDocument()
  })

  it('上架只允许扫描推荐库位，非推荐库位要求取消并重新扫描', async () => {
    seedSession(makeOperatorSession())
    renderApp('/pda/putaway')
    const user = userEvent.setup()

    await user.click(await screen.findByText(/RCP-20260819-0002/))
    expect(await screen.findByText('DEF-01')).toBeInTheDocument()
    await scan(user, 'STG-01')
    expect(await screen.findByText(/扫描库位不在推荐列表/)).toBeInTheDocument()
    expect(screen.getByTestId('submit-putaway')).toBeDisabled()
  })

  it('PDA 深链接同时受 route 与 action 权限守卫', async () => {
    const supervisor = makeSupervisorSession()
    const receivingOnly = { ...supervisor, permissions: ['route.inbound', 'action.receiving.create'] }
    seedSession(receivingOnly)
    server.use(http.get('/api/auth/me', () => HttpResponse.json({ code: 'OK', message: 'ok', data: receivingOnly })))
    renderApp('/pda/putaway')
    expect(await screen.findByText('无权限')).toBeInTheDocument()
  })

  it('批次标签预览使用 PrintJobItem.content 渲染二维码', async () => {
    seedSession(makeOperatorSession())
    renderApp('/pda/putaway')
    const user = userEvent.setup()
    await user.click(await screen.findByText(/RCP-20260819-0002/))
    await user.click(screen.getByRole('button', { name: /预览标签二维码/ }))
    const qr = await screen.findAllByTestId('print-qr-code')
    expect(qr[0].querySelector('svg')).toBeInTheDocument()
  })

  it('同一次收货操作网络重试复用稳定 Idempotency-Key', async () => {
    seedSession(makeOperatorSession())
    const keys: string[] = []
    server.use(http.post('/api/receipts', async ({ request }) => {
      keys.push(request.headers.get('Idempotency-Key') ?? '')
      if (keys.length === 1) return HttpResponse.json({ code: 'NETWORK_ERROR', message: '响应中断', data: null }, { status: 503 })
      return HttpResponse.json({ code: 'OK', message: 'ok', data: seedReceipts[0] }, { status: 201 })
    }))
    renderApp('/pda/receiving')
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: '其他入库（OT）' }))
    await scan(user, 'MAT-002')
    expect((await screen.findAllByText(/MAT-002 垫片 8mm/)).length).toBeGreaterThan(0)
    await user.selectOptions(screen.getByLabelText('暂存库位'), MOCK_IDS.locationStaging1)
    await user.click(screen.getByTestId('review-receipt'))
    await screen.findByTestId('receiving-confirmation')
    await user.click(screen.getByTestId('submit-receipt'))
    expect(await screen.findByText('响应中断')).toBeInTheDocument()
    await user.click(screen.getByTestId('submit-receipt'))
    expect(await screen.findByText('收货成功')).toBeInTheDocument()
    expect(keys).toHaveLength(2)
    expect(keys[0]).toBe(keys[1])
  })

  it('VERSION_CONFLICT 后刷新库存版本并使用新版本重试', async () => {
    seedSession(makeOperatorSession())
    const versions: number[] = []
    server.use(http.post('/api/putaway-records', async ({ request }) => {
      const body = await request.json() as { expectedInventoryVersion: number }
      versions.push(body.expectedInventoryVersion)
      if (versions.length === 1) {
        db.putawayVersions[MOCK_IDS.receiptLine2] = 4
        return HttpResponse.json({ code: 'VERSION_CONFLICT', message: '库存版本已变化，请刷新后重试', data: null }, { status: 409 })
      }
      return new HttpResponse(null, { status: 204 })
    }))
    renderApp('/pda/putaway')
    const user = userEvent.setup()
    await user.click(await screen.findByText(/RCP-20260819-0002/))
    await scan(user, 'DEF-01')
    await user.click(screen.getByTestId('submit-putaway'))
    expect(await screen.findByText(/库存版本已变化/)).toBeInTheDocument()
    await scan(user, 'DEF-01')
    await user.click(screen.getByTestId('submit-putaway'))
    expect(await screen.findByText('上架完成')).toBeInTheDocument()
    expect(versions).toEqual([3, 4])
  })
})
