import { useCallback, useMemo, useState } from 'react'
import { apiDeleteAttachment, apiUploadAttachment } from '@/api'
import type { AttachmentBizType, AttachmentItem } from '@/api/types'
import { parseApiError } from '@/api/client'
import { genIdempotencyKey } from '@/platform/format'

export type AttachmentUploadStatus = 'uploading' | 'uploaded' | 'upload-failed' | 'deleting' | 'delete-failed'

export interface AttachmentUploadEntry {
  id: string
  file: File
  status: AttachmentUploadStatus
  uploadKey: string
  deleteKey: string | null
  attachment: AttachmentItem | null
  error: string | null
}

export function useAttachmentUploads(bizType: AttachmentBizType, maxCount = 3) {
  const [entries, setEntries] = useState<AttachmentUploadEntry[]>([])

  const send = useCallback(async (entry: AttachmentUploadEntry) => {
    setEntries((current) => current.map((item) => item.id === entry.id
      ? { ...item, status: 'uploading', error: null }
      : item))
    try {
      const attachment = await apiUploadAttachment(entry.file, bizType, entry.uploadKey)
      setEntries((current) => current.map((item) => item.id === entry.id
        ? { ...item, status: 'uploaded', attachment, error: null }
        : item))
    } catch (reason) {
      const error = parseApiError(reason)
      setEntries((current) => current.map((item) => item.id === entry.id
        ? { ...item, status: 'upload-failed', error: error.message }
        : item))
    }
  }, [bizType])

  const add = useCallback((file: File) => {
    if (entries.length >= maxCount) return
    const entry: AttachmentUploadEntry = {
      id: crypto.randomUUID(),
      file,
      status: 'uploading',
      uploadKey: genIdempotencyKey(),
      deleteKey: null,
      attachment: null,
      error: null,
    }
    setEntries((current) => [...current, entry])
    void send(entry)
  }, [entries.length, maxCount, send])

  const retry = useCallback((id: string) => {
    const entry = entries.find((item) => item.id === id)
    if (entry?.status === 'upload-failed') void send(entry)
  }, [entries, send])

  const remove = useCallback(async (id: string) => {
    const entry = entries.find((item) => item.id === id)
    if (!entry || entry.status === 'uploading' || entry.status === 'deleting') return
    if (!entry.attachment) {
      setEntries((current) => current.filter((item) => item.id !== id))
      return
    }

    const deleteKey = entry.deleteKey ?? genIdempotencyKey()
    setEntries((current) => current.map((item) => item.id === id
      ? { ...item, status: 'deleting', deleteKey, error: null }
      : item))
    try {
      await apiDeleteAttachment(entry.attachment.id, deleteKey)
      setEntries((current) => current.filter((item) => item.id !== id))
    } catch (reason) {
      const error = parseApiError(reason)
      setEntries((current) => current.map((item) => item.id === id
        ? { ...item, status: 'delete-failed', deleteKey, error: error.message }
        : item))
    }
  }, [entries])

  const uploaded = useMemo(() => entries.flatMap((entry) => entry.attachment ? [entry.attachment] : []), [entries])
  const busy = entries.some((entry) => entry.status === 'uploading' || entry.status === 'deleting')
  const hasFailures = entries.some((entry) => entry.status === 'upload-failed' || entry.status === 'delete-failed')
  const clear = useCallback(() => setEntries([]), [])

  return { entries, uploaded, busy, hasFailures, maxCount, add, retry, remove, clear }
}
