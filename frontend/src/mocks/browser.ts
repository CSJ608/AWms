/** 浏览器端 MSW worker（开发模式 mock） */
import { setupWorker } from 'msw/browser'
import { handlers } from './handlers'

export const worker = setupWorker(...handlers)
