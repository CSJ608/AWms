import { useState } from 'react'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { useProtectedObjectUrl } from '@/platform/protected-media'

export function ProtectedImage({ path, alt, className }: { path: string; alt: string; className?: string }) {
  const media = useProtectedObjectUrl(path)
  if (media.loading) return <div className={`${className ?? ''} animate-pulse bg-muted`} aria-label={`${alt}加载中`} />
  if (!media.url) return <div className={`${className ?? ''} grid place-items-center bg-muted px-2 text-center text-xs text-destructive`}>{media.error ?? '图片加载失败'}</div>
  return <img src={media.url} alt={alt} className={className} />
}

export function ProtectedImagePreview({
  thumbnailPath,
  originalPath,
  alt,
  className,
}: {
  thumbnailPath: string
  originalPath: string
  alt: string
  className?: string
}) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button type="button" className="block size-full overflow-hidden" aria-label={`查看${alt}原图`} onClick={() => setOpen(true)}>
        <ProtectedImage path={thumbnailPath} alt={alt} className={className} />
      </button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-3xl">
          <DialogHeader><DialogTitle>{alt}</DialogTitle></DialogHeader>
          {open && <ProtectedImage path={originalPath} alt={alt} className="max-h-[75vh] w-full object-contain" />}
        </DialogContent>
      </Dialog>
    </>
  )
}
