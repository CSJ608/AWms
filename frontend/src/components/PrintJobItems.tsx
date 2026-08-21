import { QRCodeSVG } from 'qrcode.react'
import type { PrintJobItem } from '@/api/types'

export function PrintJobItems({ items, limit }: { items: PrintJobItem[]; limit?: number }) {
  const visible = limit ? items.slice(0, limit) : items
  return (
    <>
      {visible.map((item, index) => (
        <div key={`${item.content}-${index}`} className="rounded-lg border bg-muted/30 p-3">
          <div className="mb-3 w-fit rounded-md border bg-white p-2" data-testid="print-qr-code">
            <QRCodeSVG
              value={item.content}
              size={144}
              level="M"
              marginSize={1}
              title={item.readableText.split('\n')[0] ?? '二维码'}
            />
          </div>
          <pre className="whitespace-pre-wrap text-xs">{item.readableText}</pre>
        </div>
      ))}
      {limit && items.length > limit && (
        <p className="text-xs text-muted-foreground">共 {items.length} 张，仅展示前 {limit} 张</p>
      )}
    </>
  )
}
