import path from 'node:path'
import { playwright } from '@vitest/browser-playwright'
import { defineConfig } from 'vitest/config'

const SAMPLE_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP4z8DwHwAFgAIB/1j3WQAAAABJRU5ErkJggg==',
  'base64',
)

export default defineConfig({
  plugins: [{
    name: 'protected-media-browser-test-endpoint',
    configureServer(server) {
      server.middlewares.use('/api/browser-test/protected.png', (request, response) => {
        if (request.headers.authorization !== 'Bearer browser-test-token') {
          response.statusCode = 401
          response.setHeader('Content-Type', 'application/json')
          response.end(JSON.stringify({ code: 'UNAUTHORIZED', message: '未登录或会话已过期', data: null }))
          return
        }
        response.statusCode = 200
        response.setHeader('Content-Type', 'image/png')
        response.end(SAMPLE_PNG)
      })
    },
  }],
  resolve: {
    alias: { '@': path.resolve(import.meta.dirname, './src') },
  },
  test: {
    include: ['src/**/*.browser.ts'],
    browser: {
      enabled: true,
      headless: true,
      provider: playwright({
        launchOptions: process.platform === 'win32'
          ? { executablePath: process.env.CHROME_PATH ?? 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe' }
          : undefined,
      }),
      instances: [{ browser: 'chromium' }],
    },
  },
})
