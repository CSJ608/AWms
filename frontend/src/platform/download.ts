/** 文件下载助手（Blob → <a download>） */
export function downloadBlob(res: Response, fallbackName: string): Promise<void> {
  return res.blob().then((blob) => {
    const cd = res.headers.get('Content-Disposition') ?? ''
    const m = cd.match(/filename="?([^";]+)"?/i)
    const name = m?.[1] ?? fallbackName
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = name
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  })
}
