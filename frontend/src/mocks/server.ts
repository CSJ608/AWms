/** 测试端 MSW server（Vitest 复用同一套 handlers，严格按契约 DTO） */
import { setupServer } from 'msw/node'
import { handlers } from './handlers'

export const server = setupServer(...handlers)
