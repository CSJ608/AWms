/**
 * 导入 / 导出弹窗 —— 平台能力（导入导出契约 v0.2）：
 * 导入：下载模板 → 上传 precheck（只校验不落库）→ 预校验报告 → 全部通过才可执行 → 结果报告；
 * 导出：创建异步任务 → 轮询完成 → 下载文件（导出当前筛选结果，与模板同结构）。
 */
import { useMutation, useQuery } from '@tanstack/react-query'
import { Download, Loader2, Upload } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import {
  apiCreateExportTask, apiDownloadImportTemplate, apiDownloadTaskFile, apiExecuteImport, apiPrecheckImport,
} from '@/api'
import type { FilterDsl, ImportTask, SortSpec } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { downloadBlob } from '@/platform/download'
import { enumLabelKey } from '@/platform/labels'

export interface ImportExportDialogProps {
  moduleCode: string
  open: boolean
  onOpenChange: (open: boolean) => void
  /** 导出：当前列表筛选（导出当前筛选结果） */
  listFilter?: FilterDsl
  listSort?: SortSpec[]
}

export function ImportExportDialog({
  moduleCode, open, onOpenChange, listFilter, listSort,
}: ImportExportDialogProps) {
  const { t } = useTranslation()
  const fileRef = useRef<HTMLInputElement>(null)
  const [task, setTask] = useState<ImportTask | null>(null)

  const reset = () => {
    setTask(null)
    if (fileRef.current) fileRef.current.value = ''
  }

  // ── 导入 ─────────────────────────────────────────────
  const templateQuery = useQuery({
    queryKey: ['import-template', moduleCode],
    queryFn: () => apiDownloadImportTemplate(moduleCode),
    enabled: false,
  })

  const downloadTemplate = async () => {
    try {
      const res = await templateQuery.refetch()
      if (res.data) await downloadBlob(res.data, `${moduleCode}-import-template.xlsx`)
    } catch (e) {
      toast.error((e as Error).message)
    }
  }

  const precheckMutation = useMutation({
    mutationFn: (file: File) => apiPrecheckImport(moduleCode, file),
    onSuccess: (data) => {
      setTask(data)
      if (data.canExecute) {
        toast.success(t('importExport.precheckResult', { total: data.totalCount, success: data.successCount, fail: data.failCount }))
      } else {
        toast.error(t('importExport.precheckFail'))
      }
    },
    onError: (e) => toast.error((e as Error).message),
  })

  const executeMutation = useMutation({
    mutationFn: (taskId: string) => apiExecuteImport(taskId),
    onSuccess: (data) => {
      setTask(data)
      toast.success(t('importExport.executeResult', { success: data.successCount }))
    },
    onError: (e) => toast.error((e as Error).message),
  })

  const onFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    precheckMutation.mutate(file)
  }

  // ── 导出 ─────────────────────────────────────────────
  const exportMutation = useMutation({
    mutationFn: () => apiCreateExportTask({ moduleCode, filter: listFilter, sort: listSort, pageSize: 0 }),
    onSuccess: (data) => {
      setTask(data)
      toast.success(t('importExport.exportCreating'))
    },
    onError: (e) => toast.error((e as Error).message),
  })

  const downloadFile = async (url: string, fallback: string) => {
    try {
      const res = await apiDownloadTaskFile(url)
      await downloadBlob(res, fallback)
    } catch (e) {
      toast.error((e as Error).message)
    }
  }

  const failures = task?.failures ?? []

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        onOpenChange(o)
        if (!o) reset()
      }}
    >
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('importExport.title')}</DialogTitle>
        </DialogHeader>
        <Tabs defaultValue="import">
          <TabsList>
            <TabsTrigger value="import" data-testid="tab-import">{t('importExport.importTab')}</TabsTrigger>
            <TabsTrigger value="export" data-testid="tab-export">{t('importExport.exportTab')}</TabsTrigger>
          </TabsList>

          <TabsContent value="import" className="space-y-3">
            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" onClick={downloadTemplate} data-testid="dl-template">
                <Download className="size-3.5" data-icon />
                {t('importExport.downloadTemplate')}
              </Button>
              <input
                ref={fileRef}
                type="file"
                accept=".xlsx"
                className="hidden"
                data-testid="import-file-input"
                onChange={onFileChange}
              />
              <Button variant="outline" size="sm" onClick={() => fileRef.current?.click()} data-testid="choose-file">
                <Upload className="size-3.5" data-icon />
                {t('importExport.uploadFile')}
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">{t('importExport.uploadHint')}</p>

            {precheckMutation.isPending && (
              <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
                <Loader2 className="size-3.5 animate-spin" data-icon />
                {t('importExport.prechecking')}
              </p>
            )}

            {task && task.direction === 'IMPORT' && (
              <div className="space-y-3">
                <div className="flex items-center gap-2 text-sm">
                  <span className="text-muted-foreground">
                    {t('importExport.precheckResult', { total: task.totalCount, success: task.successCount, fail: task.failCount })}
                  </span>
                  {task.canExecute ? <Badge variant="success">{t('common.ready', { defaultValue: '✓' })}</Badge> : <Badge variant="destructive">{t('importExport.precheckFail')}</Badge>}
                </div>

                {failures.length > 0 && (
                  <div className="space-y-1.5">
                    <p className="text-xs text-muted-foreground">{t('importExport.failTable')}</p>
                    <div className="max-h-48 overflow-auto rounded-md border">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead className="w-14">{t('importExport.rowNo')}</TableHead>
                            <TableHead>{t('importExport.columnName')}</TableHead>
                            <TableHead>{t('importExport.rawValue')}</TableHead>
                            <TableHead>{t('importExport.reason')}</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {failures.map((f) => (
                            <TableRow key={`${f.rowNo}-${f.columnCode}`}>
                              <TableCell className="tabular-nums">{f.rowNo}</TableCell>
                              <TableCell>{f.columnName}</TableCell>
                              <TableCell className="tabular-nums">{f.rawValue}</TableCell>
                              <TableCell className="text-destructive">{f.errorMsg}</TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </div>
                  </div>
                )}

                <Button
                  disabled={!task.canExecute || executeMutation.isPending}
                  onClick={() => executeMutation.mutate(task.id)}
                  data-testid="execute-import"
                >
                  {executeMutation.isPending
                    ? <><Loader2 className="size-3.5 animate-spin" data-icon />{t('importExport.executing')}</>
                    : t('importExport.execute')}
                </Button>

                {executeMutation.isSuccess && (
                  <p className="text-sm text-success" data-testid="import-done">
                    {t('importExport.executeResult', { success: task.successCount })}
                  </p>
                )}
              </div>
            )}
          </TabsContent>

          <TabsContent value="export" className="space-y-3">
            <Button
              size="sm"
              disabled={exportMutation.isPending}
              onClick={() => exportMutation.mutate()}
              data-testid="create-export"
            >
              {exportMutation.isPending
                ? <><Loader2 className="size-3.5 animate-spin" data-icon />{t('importExport.exportCreating')}</>
                : t('importExport.exportCreate')}
            </Button>
            {exportMutation.isSuccess && task?.fileUrl && (
              <Button variant="outline" size="sm" onClick={() => downloadFile(task.fileUrl!, 'export.xlsx')} data-testid="download-export">
                <Download className="size-3.5" data-icon />
                {t('importExport.downloadFile')}
              </Button>
            )}
          </TabsContent>
        </Tabs>

        {task && (
          <div className="flex flex-wrap gap-x-4 gap-y-1 border-t pt-2 text-xs text-muted-foreground">
            <span>{t('importExport.taskNo')}: <span className="tabular-nums">{task.taskNo}</span></span>
            <span>{t('importExport.fileName')}: {task.fileName}</span>
            <span>{t('importExport.status')}: {enumLabelKey('importTaskStatus', task.status) ?? task.status}</span>
            {task.failReportUrl && (
              <button
                type="button"
                className="text-primary underline-offset-2 hover:underline"
                onClick={() => downloadFile(task.failReportUrl!, 'fail-report.csv')}
              >
                {t('importExport.downloadFile')}
              </button>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
