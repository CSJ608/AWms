/**
 * MSW handlers —— 严格按契约端点与 DTO（docs/api/）。
 * 覆盖：认证（登录/刷新/登出/me）、运行时元数据、主数据 CRUD（物料/仓库/库位/来源/批次）、
 * 导入导出（模板/预检/执行/导出/任务/文件）。
 * 列表查询（通用规范 2.10 v1.9）：标准列表 POST /{resource}/search（body 含 keyword/固定参数/filter/sort）；
 * GET /{resource}?keyword=&pageSize= 仅作引用选择器快捷搜索。
 * 认证语义：mock-token-<username>；可整体失效 token（invalidateMockTokens）模拟 401 刷新链路。
 */
import { delay, http, HttpResponse } from 'msw'
import type { FilterOp, ImportTask, LocationItem, LoginResponse, MaterialItem, SortSpec, SourceItem, UserItem, WarehouseItem } from '../api/types'
import {
  db, findUser, getByCode, getById, newId, nowIso, queryList,
} from './db'
import type { QueryOptions } from './db'
import { RESOURCE_DEFAULT_SORT, RESOURCE_FIXED_PARAMS, RESOURCE_KEYWORD_FIELDS, RESOURCE_META } from './meta'
import {
  MOCK_EXPIRED_TOKEN, MOCK_TOKEN_PREFIX, SEED_PASSWORDS, menusFor, permissionsOf, seedPermissions,
} from './seed'

// ── mock 状态（测试可控）───────────────────────────────
export const mockState = {
  /** 刷新端点被调用次数（401 单飞刷新测试断言用） */
  refreshCount: 0,
  tokensInvalidated: false,
  /** 刷新轮次（签发新 token 的 nonce，F-01 恢复链路守门用） */
  tokenSeq: 0,
}

/** 使所有 mock token 失效（模拟服务端会话过期），调用刷新后恢复 */
export function invalidateMockTokens(on: boolean): void {
  mockState.tokensInvalidated = on
}

export function resetMockState(): void {
  mockState.refreshCount = 0
  mockState.tokensInvalidated = false
  mockState.tokenSeq = 0
}

const mockDelay = () => delay(import.meta.env.MODE === 'test' ? 0 : 80)

// ── 响应封装（通用规范 2.1）────────────────────────────
const ok = (data: unknown) => HttpResponse.json({ code: 'OK', message: 'ok', data })
const fail = (code: string, message: string, status: number) =>
  HttpResponse.json({ code, message, data: null }, { status })

// ── 认证 ──────────────────────────────────────────────
/** token 形如 mock-token-<username>[#<刷新轮次>]：刷新签发新 token（对齐真实后端每次
 * refresh 换发新 token），# 后 nonce 仅用于区分轮次，解析用户名时剥掉。 */
function tokenUsername(req: Request): string | null {
  const h = req.headers.get('Authorization')
  if (!h?.startsWith('Bearer ')) return null
  const token = h.slice(7)
  if (token === MOCK_EXPIRED_TOKEN || !token.startsWith(MOCK_TOKEN_PREFIX)) return null
  return token.slice(MOCK_TOKEN_PREFIX.length).split('#')[0]
}

/** 受保护端点鉴权：无效/已失效 token → 401 envelope */
function requireAuth(req: Request): { user: UserItem } | Response {
  const username = tokenUsername(req)
  if (!username || mockState.tokensInvalidated) {
    return fail('UNAUTHORIZED', '未登录或会话已过期', 401)
  }
  const user = findUser(username)
  if (!user) return fail('UNAUTHORIZED', '未登录或会话已过期', 401)
  return { user }
}

function buildLoginResponse(user: UserItem): LoginResponse {
  return {
    token: `${MOCK_TOKEN_PREFIX}${user.username}`,
    expiresAt: new Date(Date.now() + 2 * 3600_000).toISOString(),
    user,
    permissions: permissionsOf(user.username),
    menus: menusFor(user.username),
  }
}

// ── 查询参数解析（通用规范 2.10 v1.9）──────────────────
/** 列表查询规格：keyword 模糊字段集 + 固定参数声明 + 默认排序（契约各接口） */
interface SearchSpec {
  fuzzyFields?: string[]
  fixedParams: Array<{ param: string; op: FilterOp }>
  defaultSort?: SortSpec[]
}

/** POST /{resource}/search 请求体 → QueryOptions（标准列表查询；filter/sort 原生 JSON，不解析字符串） */
async function parseSearchBody(req: Request, spec: SearchSpec): Promise<QueryOptions> {
  const body = (await req.json()) as Record<string, unknown>
  const opts: QueryOptions = { fuzzyFields: spec.fuzzyFields }
  const kw = body.keyword
  if (typeof kw === 'string' && kw) opts.keyword = kw
  const fixed: Record<string, string | boolean | number | undefined> = {}
  for (const { param } of spec.fixedParams) {
    const v = body[param]
    if (v !== undefined && v !== null && v !== '') fixed[param] = v as string | boolean | number
  }
  opts.fixed = fixed
  if (body.filter !== undefined) opts.filter = body.filter as QueryOptions['filter']
  if (Array.isArray(body.sort) && body.sort.length > 0) opts.sort = body.sort as QueryOptions['sort']
  const page = Number(body.page ?? 1)
  const pageSize = Number(body.pageSize ?? 20)
  opts.page = Number.isFinite(page) ? page : 1
  opts.pageSize = Number.isFinite(pageSize) ? pageSize : 20
  // 默认排序（v2.1：主数据业务码 asc；时间性列表 createdAt DESC 最新在前）
  if (!opts.sort && spec.defaultSort) opts.sort = spec.defaultSort
  return opts
}

/** GET /{resource}?keyword=&pageSize=（引用选择器快捷搜索，轻量；不接收 filter/sort） */
function parseQuickQuery(req: Request, resource: string): QueryOptions {
  const sp = new URL(req.url).searchParams
  const opts: QueryOptions = { fuzzyFields: RESOURCE_KEYWORD_FIELDS[resource] }
  const kw = sp.get('keyword')
  if (kw) opts.keyword = kw
  const page = Number(sp.get('page') ?? '1')
  const pageSize = Number(sp.get('pageSize') ?? '10')
  opts.page = Number.isFinite(page) ? page : 1
  opts.pageSize = Number.isFinite(pageSize) ? pageSize : 10
  return opts
}

/** 主数据列表资源规格（契约各接口 search body 行） */
function listSpec(resource: string): SearchSpec {
  return {
    fuzzyFields: RESOURCE_KEYWORD_FIELDS[resource],
    fixedParams: RESOURCE_FIXED_PARAMS[resource] ?? [],
    defaultSort: RESOURCE_DEFAULT_SORT[resource],
  }
}

// ── 导入导出任务存储 ──────────────────────────────────
const importTasks = new Map<string, ImportTask>()
let taskSeq = 0

function makeTaskNo(): string {
  taskSeq += 1
  return `IMP-20260810-${String(taskSeq).padStart(4, '0')}`
}

// ── handlers ──────────────────────────────────────────

export const handlers = [
  // 认证
  http.post('/api/auth/login', async ({ request }) => {
    await mockDelay()
    const body = (await request.json()) as { username?: string; password?: string }
    const user = findUser(body?.username ?? '')
    if (!user || SEED_PASSWORDS[user.username] !== body?.password) {
      return fail('LOGIN_FAILED', '用户名或密码错误', 401)
    }
    if (user.status === 'DISABLED') {
      return fail('USER_DISABLED', '账号已停用', 401)
    }
    return ok(buildLoginResponse(user))
  }),

  http.post('/api/auth/refresh', async ({ request }) => {
    await mockDelay()
    mockState.refreshCount += 1
    const username = tokenUsername(request)
    if (!username) return fail('UNAUTHORIZED', '未登录或会话已过期', 401)
    // 刷新视为会话续期：恢复 token 有效性 + 换发新 token（对齐真实后端每次 refresh 新签发）
    mockState.tokensInvalidated = false
    mockState.tokenSeq += 1
    const user = findUser(username)
    return ok({
      token: `${MOCK_TOKEN_PREFIX}${username}#${mockState.tokenSeq}`,
      expiresAt: new Date(Date.now() + 2 * 3600_000).toISOString(),
      user: user ?? undefined,
    })
  }),

  http.post('/api/auth/logout', async () => {
    await mockDelay()
    return new HttpResponse(null, { status: 204 })
  }),

  http.get('/api/auth/me', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    // /auth/me 回显请求头中的当前 token（对齐真实后端：契约约定 me 返回当前请求 token）
    const token = request.headers.get('Authorization')?.slice(7) ?? ''
    return ok({ ...buildLoginResponse((auth as { user: UserItem }).user), token })
  }),

  // 运行时字段元数据
  http.get('/api/meta/fields/:resource', async ({ params }) => {
    await mockDelay()
    const meta = RESOURCE_META[String(params.resource)]
    if (!meta) return fail('NOT_FOUND', '资源不存在', 404)
    return ok(meta)
  }),

  // ── 物料 ─────────────────────────────────────────────
  http.post('/api/materials/search', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    try {
      return ok(queryList(db.materials, await parseSearchBody(request, listSpec('materials'))))
    } catch (e) {
      return fail((e as { code: string }).code, (e as { message: string }).message, 400)
    }
  }),

  http.get('/api/materials', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    return ok(queryList(db.materials, parseQuickQuery(request, 'materials')))
  }),

  http.post('/api/materials', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const body = (await request.json()) as { code: string; name: string; searchCode?: string | null; batchControlled: boolean; labelType: string; defaultUom: string; defaultQtyPerLabel?: string | null; status?: string }
    if (!body.code || !body.name) return fail('VALIDATION_ERROR', '编码和名称为必填', 400)
    if (getByCode(db.materials, body.code)) {
      return fail('MATERIAL_CODE_DUPLICATED', `物料编码 ${body.code} 已存在`, 409)
    }
    const item = {
      id: newId('mat'),
      code: body.code,
      name: body.name,
      searchCode: body.searchCode ?? null,
      batchControlled: body.batchControlled,
      labelType: body.labelType,
      defaultUom: body.defaultUom,
      defaultQtyPerLabel: body.defaultQtyPerLabel ?? null,
      status: body.status ?? 'ENABLED',
      createdAt: nowIso(),
      updatedAt: nowIso(),
    }
    db.materials.push(item as MaterialItem)
    return HttpResponse.json({ code: 'OK', message: 'ok', data: item }, { status: 201 })
  }),

  http.put('/api/materials/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const row = getById(db.materials, String(params.id))
    if (!row) return fail('MATERIAL_NOT_FOUND', '物料不存在', 404)
    const body = (await request.json()) as { name: string; searchCode?: string | null; batchControlled: boolean; labelType: string; defaultUom: string; defaultQtyPerLabel?: string | null; status: string }
    Object.assign(row, {
      name: body.name,
      searchCode: body.searchCode ?? null,
      batchControlled: body.batchControlled,
      labelType: body.labelType,
      defaultUom: body.defaultUom,
      defaultQtyPerLabel: body.defaultQtyPerLabel ?? null,
      status: body.status,
      updatedAt: nowIso(),
    })
    return ok(row)
  }),

  http.delete('/api/materials/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const id = String(params.id)
    const row = getById(db.materials, id)
    if (!row) return fail('MATERIAL_NOT_FOUND', '物料不存在', 404)
    // 引用保护：被批次引用（契约：删除保护=被引用拒绝）
    if (db.batches.some((b) => b.materialId === id)) {
      return fail('MATERIAL_IN_USE', '物料已被批次引用，禁止删除', 409)
    }
    db.materials = db.materials.filter((m) => m.id !== id)
    return new HttpResponse(null, { status: 204 })
  }),

  // ── 仓库 ─────────────────────────────────────────────
  http.post('/api/warehouses/search', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    return ok(queryList(db.warehouses, await parseSearchBody(request, listSpec('warehouses'))))
  }),

  http.get('/api/warehouses', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    return ok(queryList(db.warehouses, parseQuickQuery(request, 'warehouses')))
  }),

  http.post('/api/warehouses', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const body = (await request.json()) as { code: string; name: string; searchCode?: string | null; status?: string; mgmtMode?: string }
    if (!body.code || !body.name) return fail('VALIDATION_ERROR', '编码和名称为必填', 400)
    if (getByCode(db.warehouses, body.code)) {
      return fail('WAREHOUSE_CODE_DUPLICATED', `仓库编码 ${body.code} 已存在`, 409)
    }
    const item = {
      id: newId('wh'),
      code: body.code,
      name: body.name,
      searchCode: body.searchCode ?? null,
      status: body.status ?? 'ENABLED',
      mgmtMode: body.mgmtMode ?? 'MANUAL',
      createdAt: nowIso(),
    }
    db.warehouses.push(item as WarehouseItem)
    return HttpResponse.json({ code: 'OK', message: 'ok', data: item }, { status: 201 })
  }),

  http.put('/api/warehouses/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const row = getById(db.warehouses, String(params.id))
    if (!row) return fail('WAREHOUSE_NOT_FOUND', '仓库不存在', 404)
    const body = (await request.json()) as { name: string; searchCode?: string | null; status: string }
    Object.assign(row, { name: body.name, searchCode: body.searchCode ?? null, status: body.status })
    return ok(row)
  }),

  http.delete('/api/warehouses/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const id = String(params.id)
    if (!getById(db.warehouses, id)) return fail('WAREHOUSE_NOT_FOUND', '仓库不存在', 404)
    if (db.locations.some((l) => l.warehouseId === id)) {
      return fail('WAREHOUSE_IN_USE', '仓库下存在库位，禁止删除', 409)
    }
    db.warehouses = db.warehouses.filter((w) => w.id !== id)
    return new HttpResponse(null, { status: 204 })
  }),

  // ── 库位 ─────────────────────────────────────────────
  http.post('/api/warehouses/:warehouseId/locations/search', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const wid = String(params.warehouseId)
    if (!getById(db.warehouses, wid)) return fail('WAREHOUSE_NOT_FOUND', '仓库不存在', 404)
    const opts = await parseSearchBody(request, listSpec('locations'))
    return ok(queryList(db.locations.filter((l) => l.warehouseId === wid), opts))
  }),

  http.get('/api/warehouses/:warehouseId/locations', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const wid = String(params.warehouseId)
    if (!getById(db.warehouses, wid)) return fail('WAREHOUSE_NOT_FOUND', '仓库不存在', 404)
    const opts = parseQuickQuery(request, 'locations')
    return ok(queryList(db.locations.filter((l) => l.warehouseId === wid), opts))
  }),

  http.post('/api/warehouses/:warehouseId/locations', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const wid = String(params.warehouseId)
    const wh = getById(db.warehouses, wid)
    if (!wh) return fail('WAREHOUSE_NOT_FOUND', '仓库不存在', 404)
    const body = (await request.json()) as { code: string; searchCode?: string | null; type: string; status?: string }
    if (!body.code || !body.type) return fail('VALIDATION_ERROR', '编码和类型为必填', 400)
    if (db.locations.some((l) => l.warehouseId === wid && l.code === body.code)) {
      return fail('LOCATION_CODE_DUPLICATED', `库位编码 ${body.code} 已存在`, 409)
    }
    const item = {
      id: newId('loc'),
      warehouseId: wid,
      warehouseCode: wh.code,
      code: body.code,
      searchCode: body.searchCode ?? null,
      type: body.type,
      status: body.status ?? 'ENABLED',
      reachability: 'UNIVERSAL',
      createdAt: nowIso(),
    }
    db.locations.push(item as LocationItem)
    return HttpResponse.json({ code: 'OK', message: 'ok', data: item }, { status: 201 })
  }),

  http.put('/api/locations/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const row = getById(db.locations, String(params.id))
    if (!row) return fail('LOCATION_NOT_FOUND', '库位不存在', 404)
    const body = (await request.json()) as { type: string; searchCode?: string | null; status: string }
    Object.assign(row, { type: body.type, searchCode: body.searchCode ?? null, status: body.status })
    return ok(row)
  }),

  http.delete('/api/locations/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const id = String(params.id)
    if (!getById(db.locations, id)) return fail('LOCATION_NOT_FOUND', '库位不存在', 404)
    // 引用保护：库位有货/被引用禁止删除（本期库存未建，无引用场景）
    db.locations = db.locations.filter((l) => l.id !== id)
    return new HttpResponse(null, { status: 204 })
  }),

  // ── 来源 ─────────────────────────────────────────────
  http.post('/api/sources/search', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    return ok(queryList(db.sources, await parseSearchBody(request, listSpec('sources'))))
  }),

  http.get('/api/sources', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    return ok(queryList(db.sources, parseQuickQuery(request, 'sources')))
  }),

  http.post('/api/sources', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const body = (await request.json()) as { type: string; code: string; name: string; searchCode?: string | null; status?: string }
    if (!body.type || !body.code || !body.name) return fail('VALIDATION_ERROR', '类型、编码和名称为必填', 400)
    if (db.sources.some((s) => s.type === body.type && s.code === body.code)) {
      return fail('SOURCE_CODE_DUPLICATED', `来源编码 ${body.code} 已存在`, 409)
    }
    const item = {
      id: newId('src'),
      type: body.type,
      code: body.code,
      name: body.name,
      searchCode: body.searchCode ?? null,
      status: body.status ?? 'ENABLED',
      createdAt: nowIso(),
    }
    db.sources.push(item as SourceItem)
    return HttpResponse.json({ code: 'OK', message: 'ok', data: item }, { status: 201 })
  }),

  http.put('/api/sources/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const row = getById(db.sources, String(params.id))
    if (!row) return fail('SOURCE_NOT_FOUND', '来源不存在', 404)
    const body = (await request.json()) as { name: string; searchCode?: string | null; status: string }
    Object.assign(row, { name: body.name, searchCode: body.searchCode ?? null, status: body.status })
    return ok(row)
  }),

  http.delete('/api/sources/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const id = String(params.id)
    if (!getById(db.sources, id)) return fail('SOURCE_NOT_FOUND', '来源不存在', 404)
    db.sources = db.sources.filter((s) => s.id !== id)
    return new HttpResponse(null, { status: 204 })
  }),

  // ── 批次（只读）──────────────────────────────────────
  http.post('/api/batches/search', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    return ok(queryList(db.batches, await parseSearchBody(request, listSpec('batches'))))
  }),

  http.get('/api/batches', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    return ok(queryList(db.batches, parseQuickQuery(request, 'batches')))
  }),

  http.get('/api/batches/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const row = getById(db.batches, String(params.id))
    if (!row) return fail('BATCH_NOT_FOUND', '批次不存在', 404)
    return ok(row)
  }),

  http.post('/api/materials/:materialId/batches/search', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const mid = String(params.materialId)
    if (!getById(db.materials, mid)) return fail('MATERIAL_NOT_FOUND', '物料不存在', 404)
    // 嵌套批次列表：契约 body 仅 status/filter/sort/page/pageSize（无 keyword）
    const opts = await parseSearchBody(request, {
      fuzzyFields: [],
      fixedParams: [{ param: 'status', op: 'eq' }],
      defaultSort: RESOURCE_DEFAULT_SORT.batches,
    })
    return ok(queryList(db.batches.filter((b) => b.materialId === mid), opts))
  }),

  // ── 导入导出 ─────────────────────────────────────────
  http.get('/api/import-export/templates/:moduleCode', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    if (String(params.moduleCode) !== 'materials') return fail('NOT_FOUND', '模板不存在', 404)
    return new HttpResponse(new Uint8Array([80, 75, 3, 4, 0, 0, 0, 0]), {
      status: 200,
      headers: {
        'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'Content-Disposition': 'attachment; filename="materials-import-template.xlsx"',
      },
    })
  }),

  http.post('/api/import-export/precheck', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const fd = await request.formData()
    const moduleCode = String(fd.get('moduleCode') ?? '')
    const file = fd.get('file') as File | null
    if (moduleCode !== 'materials' || !file) return fail('VALIDATION_ERROR', '缺少模块或文件', 400)

    const text = await file.text()
    const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0 && !l.startsWith('编码'))
    if (text.includes('BAD_FILE')) return fail('IMPORT_PARSE_ERROR', '文件解析失败：表头或格式错误', 422)

    const failures: ImportTask['failures'] = []
    const seen = new Set<string>()
    lines.forEach((line, i) => {
      const code = line.split(',')[0]?.trim() ?? ''
      const rowNo = i + 2 // 含表头
      if (!code) return
      if (seen.has(code) || db.materials.some((m) => m.code === code)) {
        failures?.push({
          rowNo,
          columnCode: 'code',
          columnName: '物料编码',
          rawValue: code,
          errorCode: 'MATERIAL_CODE_DUPLICATED',
          errorMsg: '物料编码已存在',
        })
      }
      seen.add(code)
    })

    const taskId = newId('task')
    const task: ImportTask = {
      id: taskId,
      taskNo: makeTaskNo(),
      moduleCode,
      fileName: file.name,
      direction: 'IMPORT',
      status: 'PRECHECKED',
      totalCount: lines.length,
      successCount: lines.length - (failures?.length ?? 0),
      failCount: failures?.length ?? 0,
      canExecute: (failures?.length ?? 0) === 0,
      failures,
      failReportUrl: failures && failures.length > 200 ? `/api/import-export/tasks/${taskId}/fail-report` : null,
      fileUrl: null,
      operatorId: 'u-admin',
      operatorName: '系统管理员',
      createdAt: nowIso(),
    }
    importTasks.set(task.id, task)
    return ok(task)
  }),

  http.post('/api/import-export/execute', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const body = (await request.json()) as { taskId: string }
    const task = importTasks.get(body.taskId)
    if (!task) return fail('IMPORT_TASK_NOT_FOUND', '导入任务不存在', 404)
    if (!task.canExecute) {
      return fail('IMPORT_VALIDATION_FAILED', '仍存在校验失败项，禁止执行', 422)
    }
    task.status = 'DONE'
    task.successCount = task.totalCount
    return ok(task)
  }),

  http.post('/api/import-export/export', async ({ request }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const body = (await request.json()) as { moduleCode: string }
    if (body.moduleCode !== 'materials') return fail('VALIDATION_ERROR', '模块未注册', 400)
    const taskId = newId('task')
    const task: ImportTask = {
      id: taskId,
      taskNo: makeTaskNo(),
      moduleCode: body.moduleCode,
      fileName: 'materials-export.xlsx',
      direction: 'EXPORT',
      status: 'DONE',
      totalCount: db.materials.length,
      successCount: db.materials.length,
      failCount: 0,
      canExecute: false,
      failures: undefined,
      failReportUrl: null,
      fileUrl: `/api/import-export/tasks/${taskId}/file`,
      operatorId: 'u-admin',
      operatorName: '系统管理员',
      createdAt: nowIso(),
    }
    importTasks.set(task.id, task)
    return HttpResponse.json({ code: 'OK', message: 'ok', data: task }, { status: 201 })
  }),

  http.get('/api/import-export/tasks/:id', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const task = importTasks.get(String(params.id))
    if (!task) return fail('IMPORT_TASK_NOT_FOUND', '导入导出任务不存在', 404)
    return ok(task)
  }),

  http.get('/api/import-export/tasks/:id/fail-report', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const task = importTasks.get(String(params.id))
    if (!task) return fail('IMPORT_TASK_NOT_FOUND', '导入导出任务不存在', 404)
    return new HttpResponse('rowNo,columnCode,rawValue,errorMsg\n', {
      status: 200,
      headers: { 'Content-Type': 'text/csv; charset=utf-8', 'Content-Disposition': 'attachment; filename="fail-report.csv"' },
    })
  }),

  http.get('/api/import-export/tasks/:id/file', async ({ request, params }) => {
    await mockDelay()
    const auth = requireAuth(request)
    if (auth instanceof HttpResponse) return auth
    const task = importTasks.get(String(params.id))
    if (!task) return fail('IMPORT_TASK_NOT_FOUND', '导入导出任务不存在', 404)
    return new HttpResponse(new Uint8Array([80, 75, 3, 4, 0, 0, 0, 0]), {
      status: 200,
      headers: {
        'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'Content-Disposition': 'attachment; filename="materials-export.xlsx"',
      },
    })
  }),
]

// 供测试重置任务存储
export function resetImportTasks(): void {
  importTasks.clear()
  taskSeq = 0
}

// 权限点列表（模块注册表，只读）
export const apiPermissions = () => seedPermissions
