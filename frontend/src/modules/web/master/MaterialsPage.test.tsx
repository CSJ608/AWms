/**
 * 物料页关键交互（F-05）：列表渲染、keyword 搜索、新建（校验/成功）、编辑（编码只读）、
 * 删除确认、引用保护拦截（MATERIAL_IN_USE toast + 记录不消失）。
 */
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { changeLanguage } from '@/i18n'
import { renderAuthed } from '@/test/utils'

function rowOf(code: string): HTMLElement {
  const cell = screen.getByText(code)
  return cell.closest('tr')!
}

describe('物料页', () => {
  it('列表渲染 + 分页计数', async () => {
    renderAuthed('/web/master/materials')
    await screen.findByText('螺母 M6')
    expect(screen.getByText(/共 25 条/)).toBeInTheDocument()
    // 空态/加载骨架不出现
    expect(screen.queryByText('暂无物料，点击新建或导入第一批数据')).not.toBeInTheDocument()
  })

  it('keyword 搜索（编码/名称/助记码）', async () => {
    renderAuthed('/web/master/materials')
    await screen.findByText('螺母 M6')

    const user = userEvent.setup()
    await user.type(screen.getByTestId('search-keyword'), '垫片')
    fireEvent.click(screen.getByTestId('search-submit'))

    await screen.findByText('垫片 8mm')
    expect(screen.queryByText('螺母 M6')).not.toBeInTheDocument()
    // 查询重置
    fireEvent.click(screen.getByTestId('search-reset'))
    await screen.findByText('螺母 M6')
  })

  it('新建：必填校验 inline → 成功后列表刷新', async () => {
    renderAuthed('/web/master/materials')
    await screen.findByText('螺母 M6')

    fireEvent.click(screen.getByTestId('btn-create'))
    await screen.findByText('新建物料')

    // 空提交 → inline 校验
    fireEvent.click(screen.getByTestId('form-submit'))
    await screen.findByText('请输入编码')
    expect(screen.getByText('请输入名称')).toBeInTheDocument()

    const user = userEvent.setup()
    await user.type(screen.getByTestId('f-code'), 'MAT-777')
    await user.type(screen.getByTestId('f-name'), '测试物料')
    fireEvent.click(screen.getByTestId('form-submit'))

    await screen.findByText('保存成功')
    // 新物料按编码排序在后页 → 用 keyword 搜索验证（服务端分页）
    await user.type(screen.getByTestId('search-keyword'), 'MAT-777')
    fireEvent.click(screen.getByTestId('search-submit'))
    await screen.findByText('MAT-777')
  })

  it('新建重复编码 → toast 后端 message（MATERIAL_CODE_DUPLICATED）', async () => {
    renderAuthed('/web/master/materials')
    await screen.findByText('螺母 M6')

    fireEvent.click(screen.getByTestId('btn-create'))
    await screen.findByText('新建物料')
    const user = userEvent.setup()
    await user.type(screen.getByTestId('f-code'), 'MAT-001')
    await user.type(screen.getByTestId('f-name'), '重复物料')
    fireEvent.click(screen.getByTestId('form-submit'))

    await screen.findByText('物料编码 MAT-001 已存在')
  })

  it('编辑：编码只读，保存后更新', async () => {
    renderAuthed('/web/master/materials')
    await screen.findByText('螺母 M6')

    fireEvent.click(within(rowOf('MAT-002')).getByTestId('btn-edit'))
    await screen.findByText('编辑物料')
    expect(screen.getByTestId('f-code')).toBeDisabled()

    const nameInput = screen.getByTestId('f-name')
    fireEvent.change(nameInput, { target: { value: '垫片 8mm 新版' } })
    fireEvent.click(screen.getByTestId('form-submit'))

    await screen.findByText('保存成功')
    await screen.findByText('垫片 8mm 新版')
  })

  it('删除：AlertDialog 二次确认 → 成功移除', async () => {
    renderAuthed('/web/master/materials')
    await screen.findByText('螺母 M6')

    fireEvent.click(within(rowOf('MAT-005')).getByTestId('btn-delete'))
    await screen.findByText('确认删除')
    fireEvent.click(screen.getByTestId('confirm-delete'))

    await screen.findByText('删除成功')
    await waitFor(() => {
      expect(screen.queryByText('MAT-005')).not.toBeInTheDocument()
    })
  })

  it('删除被引用物料：MATERIAL_IN_USE toast，记录不消失', async () => {
    renderAuthed('/web/master/materials')
    await screen.findByText('螺母 M6')

    fireEvent.click(within(rowOf('MAT-001')).getByTestId('btn-delete'))
    await screen.findByText('确认删除')
    fireEvent.click(screen.getByTestId('confirm-delete'))

    await screen.findByText('物料已被批次引用，禁止删除')
    expect(screen.getByText('MAT-001')).toBeInTheDocument()
  })

  it('批控列显示 是/否（中英随语言，不显示 true/false 原文）——验收 F-03', async () => {
    renderAuthed('/web/master/materials')
    await screen.findByText('螺母 M6')

    // MAT-001 批控=true → 是；MAT-002 批控=false → 否
    const row1 = rowOf('MAT-001')
    const row2 = rowOf('MAT-002')
    expect(within(row1).getByText('是')).toBeInTheDocument()
    expect(within(row2).getByText('否')).toBeInTheDocument()
    // 不泄漏布尔原文
    expect(screen.queryByText('true')).not.toBeInTheDocument()
    expect(screen.queryByText('false')).not.toBeInTheDocument()

    // 切英文 → Yes / No（语言恢复中文，避免影响同文件后续用例）
    changeLanguage('en')
    await waitFor(() => {
      expect(within(row1).getByText('Yes')).toBeInTheDocument()
    })
    expect(within(row2).getByText('No')).toBeInTheDocument()
    changeLanguage('zh')
  })

  it('高级筛选展开/收起', async () => {
    renderAuthed('/web/master/materials')
    await screen.findByText('螺母 M6')

    expect(screen.queryByTestId('search-advanced')).not.toBeInTheDocument()
    fireEvent.click(screen.getByTestId('search-advanced-toggle'))
    await screen.findByTestId('search-advanced')
  })
})
