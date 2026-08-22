import { useEffect, useState } from 'react'
import { apiFetchProtectedFile } from '@/api'

interface ProtectedObjectUrlState {
  url: string | null
  loading: boolean
  error: string | null
}

export function useProtectedObjectUrl(path: string | null): ProtectedObjectUrlState {
  const [state, setState] = useState<ProtectedObjectUrlState>({ url: null, loading: !!path, error: null })

  useEffect(() => {
    let active = true
    let objectUrl: string | null = null
    if (!path) {
      setState({ url: null, loading: false, error: null })
      return () => undefined
    }

    setState({ url: null, loading: true, error: null })
    void apiFetchProtectedFile(path)
      .then((response) => response.blob())
      .then((blob) => {
        if (!active) return
        objectUrl = URL.createObjectURL(blob)
        setState({ url: objectUrl, loading: false, error: null })
      })
      .catch((reason: unknown) => {
        if (!active) return
        setState({ url: null, loading: false, error: reason instanceof Error ? reason.message : String(reason) })
      })

    return () => {
      active = false
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [path])

  return state
}
