/**
 * 导入导出弹窗交互（F-04 平台能力，导入导出契约 v0.2）：
 * 模板下载、两阶段导入（预校验报告 → 全部通过才可执行 → 结果）、异步导出 + 文件下载。
 */
import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { renderAuthed } from '@/test/utils'

function makeXlsxFile(text: string, name = 'materials-import.xlsx'): File {
  return new File([text], name, { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
}

async function openImportDialog() {
  renderAuthed('/web/master/materials')
  await screen.findByText('螺母 M6')
  fireEvent.click(screen.getByTestId('btn-import-export'))
  await screen.findByTestId('tab-import')
}

describe('导入导出弹窗', () => {
  afterEach(() => {
    vi.mocked(URL.createObjectURL).mockClear()
  })

  it('下载标准模板（GET templates/materials）', async () => {
    await openImportDialog()
    fireEvent.click(screen.getByTestId('dl-template'))
    await waitFor(() => {
      expect(URL.createObjectURL).toHaveBeenCalled()
    })
  })

  it('上传含重复编码文件 → 预校验报告（失败明细 + 执行按钮禁用）', async () => {
    await openImportDialog()
    const input = screen.getByTestId('import-file-input')
    fireEvent.change(input, { target: { files: [makeXlsxFile('编码,名称\nMAT-001,重复物料\nMAT-002,也重复')] } })

    // 两行重复 → 两行失败明细
    await screen.findAllByText('物料编码已存在')
    expect(screen.getByText('共 2 行：成功 0，失败 2')).toBeInTheDocument()
    expect(screen.getByTestId('execute-import')).toBeDisabled()
  })

  it('上传干净文件 → 执行导入 → 成功报告', async () => {
    await openImportDialog()
    const input = screen.getByTestId('import-file-input')
    fireEvent.change(input, { target: { files: [makeXlsxFile('编码,名称\nMAT-777,新物料A\nMAT-888,新物料B')] } })

    await screen.findByText('共 2 行：成功 2，失败 0')
    const executeBtn = screen.getByTestId('execute-import')
    expect(executeBtn).toBeEnabled()
    fireEvent.click(executeBtn)

    await screen.findByText('导入完成：成功 2 行')
  })

  it('导入任务状态走 i18n 翻译，不泄漏 key 原文（验收 F-04）', async () => {
    await openImportDialog()
    const input = screen.getByTestId('import-file-input')
    fireEvent.change(input, { target: { files: [makeXlsxFile('编码,名称\nMAT-777,新物料A')] } })

    // precheck 后状态 = PRECHECKED → 校验完成
    await screen.findByText('共 1 行：成功 1，失败 0')
    expect(screen.getByText(/校验完成/)).toBeInTheDocument()

    // 执行后状态 = DONE → 已完成
    fireEvent.click(screen.getByTestId('execute-import'))
    await screen.findByText('导入完成：成功 1 行')
    expect(screen.getByText(/已完成/)).toBeInTheDocument()

    // 全程不泄漏 enums.importTaskStatus.* key 原文
    expect(screen.queryByText(/enums\.importTaskStatus/)).not.toBeInTheDocument()
  })

  it('导出：创建任务 → 下载文件', async () => {
    await openImportDialog()
    // Radix Tabs 需真实 pointer 事件链（合成 click 不触发切换）
    const user = userEvent.setup()
    await user.click(screen.getByTestId('tab-export'))
    fireEvent.click(screen.getByTestId('create-export'))

    await screen.findByTestId('download-export')
    fireEvent.click(screen.getByTestId('download-export'))
    await waitFor(() => {
      expect(URL.createObjectURL).toHaveBeenCalled()
    })
  })
})
