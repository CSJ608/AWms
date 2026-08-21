import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { apiDownloadPrintJobFile } from '@/api'
import { downloadBlob } from '@/platform/download'
import { server } from '@/mocks/server'
import { MOCK_IDS } from '@/mocks/seed'
import { seedSession } from '@/test/utils'
import { ProtectedImagePreview } from './ProtectedMedia'

const PNG = new Uint8Array([137, 80, 78, 71])

describe('受保护媒体', () => {
  it('缩略图和原图均通过 Bearer fetch 转成 object URL，并在关闭/卸载时释放', async () => {
    seedSession()
    const authorization: string[] = []
    server.use(
      http.get(`/api/attachments/${MOCK_IDS.attachment1}/thumbnail`, ({ request }) => {
        authorization.push(request.headers.get('Authorization') ?? '')
        return new HttpResponse(PNG, { headers: { 'Content-Type': 'image/png' } })
      }),
      http.get(`/api/attachments/${MOCK_IDS.attachment1}`, ({ request }) => {
        authorization.push(request.headers.get('Authorization') ?? '')
        return new HttpResponse(PNG, { headers: { 'Content-Type': 'image/png' } })
      }),
    )

    const view = render(
      <ProtectedImagePreview
        thumbnailPath={`/api/attachments/${MOCK_IDS.attachment1}/thumbnail`}
        originalPath={`/api/attachments/${MOCK_IDS.attachment1}`}
        alt="受保护照片"
      />,
    )
    expect(await screen.findByRole('img', { name: '受保护照片' })).toHaveAttribute('src', 'blob:mock-url')
    fireEvent.click(screen.getByRole('button', { name: '查看受保护照片原图' }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByRole('img', { name: '受保护照片' })).toHaveAttribute('src', 'blob:mock-url')
    expect(authorization).toEqual(['Bearer mock-token-admin', 'Bearer mock-token-admin'])

    fireEvent.click(screen.getByRole('button', { name: 'Close' }))
    await waitFor(() => expect(URL.revokeObjectURL).toHaveBeenCalledTimes(1))
    view.unmount()
    expect(URL.revokeObjectURL).toHaveBeenCalledTimes(2)
  })

  it('PDF 通过 Bearer fetch 下载并释放临时 object URL', async () => {
    seedSession()
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    let authorization = ''
    server.use(http.get(`/api/print/jobs/${MOCK_IDS.receipt1}/file`, ({ request }) => {
      authorization = request.headers.get('Authorization') ?? ''
      return new HttpResponse(new Uint8Array([37, 80, 68, 70]), {
        headers: { 'Content-Type': 'application/pdf', 'Content-Disposition': 'attachment; filename="receipt.pdf"' },
      })
    }))

    const response = await apiDownloadPrintJobFile(MOCK_IDS.receipt1)
    await downloadBlob(response, 'fallback.pdf')
    expect(authorization).toBe('Bearer mock-token-admin')
    expect(URL.createObjectURL).toHaveBeenCalled()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:mock-url')
    click.mockRestore()
  })
})
