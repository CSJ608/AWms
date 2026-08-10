/**
 * 契约 DTO —— 与 docs/api/ 契约一一对应（唯一事实来源，勿自行增删字段）。
 * 依据：docs/api/README.md（通用规范 v2.1）、认证权限 v0.2、物料 v0.4、仓库库位 v0.3、
 * 来源 v0.3、批次 v0.6、导入导出 v0.2、枚举与错误码 v1.6。
 */

// ── 通用 ──────────────────────────────────────────────

/** 统一响应 envelope（通用规范 2.1） */
export interface Envelope<T> {
  code: string
  message: string
  data: T | null
}

/** 前端统一错误形态 */
export interface ApiError {
  code: string
  message: string
  status?: number
}

/** 分页结果（通用规范 2.2） */
export interface PageResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

// ── 枚举（as const 数组 + 类型联合，禁 TS enum）─────────

export const USER_STATUSES = ['ACTIVE', 'DISABLED'] as const
export type UserStatus = (typeof USER_STATUSES)[number]

export const MATERIAL_STATUSES = ['ENABLED', 'DISABLED'] as const
export type MaterialStatus = (typeof MATERIAL_STATUSES)[number]

export const LABEL_TYPES = ['NONE', 'SKU', 'UNIQUE'] as const
export type LabelType = (typeof LABEL_TYPES)[number]

export const UOMS = ['CT', 'PC', 'BOX', 'KG', 'G', 'L', 'M'] as const
export type Uom = (typeof UOMS)[number]

export const WAREHOUSE_MGMT_MODES = ['MANUAL', 'AGV'] as const
export type WarehouseMgmtMode = (typeof WAREHOUSE_MGMT_MODES)[number]

export const LOCATION_TYPES = ['STAGING', 'DEFAULT'] as const
export type LocationType = (typeof LOCATION_TYPES)[number]

export const LOCATION_REACHABILITIES = ['MANUAL_ONLY', 'AGV', 'UNIVERSAL'] as const
export type LocationReachability = (typeof LOCATION_REACHABILITIES)[number]

export const SOURCE_TYPES = ['SUPPLIER', 'WORKSHOP'] as const
export type SourceType = (typeof SOURCE_TYPES)[number]

export const BATCH_STATUSES = ['ACTIVE', 'CLOSED'] as const
export type BatchStatus = (typeof BATCH_STATUSES)[number]

export const IMPORT_TASK_STATUSES = ['PRECHECKING', 'PRECHECKED', 'EXECUTING', 'DONE', 'FAILED'] as const
export type ImportTaskStatus = (typeof IMPORT_TASK_STATUSES)[number]

export const IMPORT_DIRECTIONS = ['IMPORT', 'EXPORT'] as const
export type ImportDirection = (typeof IMPORT_DIRECTIONS)[number]

export const PERMISSION_CATEGORIES = ['ROUTE', 'MENU', 'ACTION'] as const
export type PermissionCategory = (typeof PERMISSION_CATEGORIES)[number]

// ── 查询与筛选（通用规范 2.10）─────────────────────────

export const FILTER_OPS = [
  'eq', 'neq', 'contains', 'startsWith', 'in', 'notIn',
  'gt', 'gte', 'lt', 'lte', 'between', 'isNull', 'isNotNull',
] as const
export type FilterOp = (typeof FILTER_OPS)[number]

export const FIELD_TYPES = ['string', 'number', 'decimal', 'date', 'datetime', 'bool', 'enum', 'uuid', 'ref'] as const
export type FieldType = (typeof FIELD_TYPES)[number]

export const SORT_DIRS = ['asc', 'desc'] as const
export type SortDir = (typeof SORT_DIRS)[number]

export interface FieldOption {
  value: string
  labelKey: string
}

/** 字段元数据（运行时元数据端点返回） */
export interface FieldMeta {
  field: string
  labelKey: string
  type: FieldType
  operators: FilterOp[]
  options?: FieldOption[]
  refResource?: string
}

export interface FilterCondition {
  field: string
  op: FilterOp
  value?: string | number | boolean | string[] | number[] | null
}

export interface FilterGroup {
  op: 'and' | 'or'
  conditions: (FilterCondition | FilterGroup)[]
}

export type FilterDsl = FilterGroup

export interface SortSpec {
  field: string
  dir: SortDir
}

// ── 认证权限（认证权限 v0.2）───────────────────────────

export interface RoleItem {
  id: string
  code: string
  name: string
  permissionCodes: string[]
  createdAt: string
}

export interface UserItem {
  id: string
  username: string
  name: string
  status: UserStatus
  roles: RoleItem[]
  createdAt: string
}

export interface PermissionItem {
  id: string
  code: string
  name: string
  category: PermissionCategory
  moduleCode: string
}

/** Web 端菜单项（注册表驱动） */
export interface MenuEntry {
  path: string
  titleKey: string
  groupKey: string
  moduleCode: string
  iconKey: string
  sort: number
}

/** PDA 端作业入口 */
export interface PdaEntry {
  code: string
  titleKey: string
  moduleCode: string
  sort: number
}

export interface Menus {
  web: MenuEntry[]
  pda: PdaEntry[]
}

export interface LoginRequest {
  username: string
  password: string
}

export interface LoginResponse {
  token: string
  expiresAt: string
  user: UserItem
  permissions: string[]
  menus: Menus
}

export interface RefreshResponse {
  token: string
  expiresAt: string
}

// ── 主数据：物料（物料 v0.4）───────────────────────────

export interface MaterialItem {
  id: string
  code: string
  name: string
  searchCode: string | null
  batchControlled: boolean
  labelType: LabelType
  defaultUom: Uom
  defaultQtyPerLabel: string | null
  status: MaterialStatus
  createdAt: string
  updatedAt: string
}

export interface MaterialCreateRequest {
  code: string
  name: string
  searchCode?: string | null
  batchControlled: boolean
  labelType: LabelType
  defaultUom: Uom
  defaultQtyPerLabel?: string | null
  status?: MaterialStatus
}

export interface MaterialUpdateRequest {
  name: string
  searchCode?: string | null
  batchControlled: boolean
  labelType: LabelType
  defaultUom: Uom
  defaultQtyPerLabel?: string | null
  status: MaterialStatus
}

// ── 主数据：仓库 / 库位（仓库库位 v0.3）────────────────

export interface WarehouseItem {
  id: string
  code: string
  name: string
  searchCode: string | null
  status: MaterialStatus
  mgmtMode: WarehouseMgmtMode
  createdAt: string
}

export interface WarehouseCreateRequest {
  code: string
  name: string
  searchCode?: string | null
  status?: MaterialStatus
  mgmtMode?: WarehouseMgmtMode
}

export interface WarehouseUpdateRequest {
  name: string
  searchCode?: string | null
  status: MaterialStatus
}

export interface LocationItem {
  id: string
  warehouseId: string
  warehouseCode: string
  code: string
  searchCode: string | null
  type: LocationType
  status: MaterialStatus
  reachability: LocationReachability
  createdAt: string
}

export interface LocationCreateRequest {
  code: string
  searchCode?: string | null
  type: LocationType
  status?: MaterialStatus
}

export interface LocationUpdateRequest {
  type: LocationType
  searchCode?: string | null
  status: MaterialStatus
}

// ── 主数据：来源（来源 v0.3）───────────────────────────

export interface SourceItem {
  id: string
  type: SourceType
  code: string
  name: string
  searchCode: string | null
  status: MaterialStatus
  createdAt: string
}

export interface SourceCreateRequest {
  type: SourceType
  code: string
  name: string
  searchCode?: string | null
  status?: MaterialStatus
}

export interface SourceUpdateRequest {
  name: string
  searchCode?: string | null
  status: MaterialStatus
}

// ── 主数据：批次（批次 v0.6，只读）─────────────────────

export interface BatchItem {
  id: string
  materialId: string
  materialCode: string
  materialName?: string
  batchNo: string
  sourceBatchNo: string | null
  sourceType: SourceType | null
  sourceCode: string | null
  productionDate: string | null
  expiryDate: string | null
  status: BatchStatus
  createdAt: string
}

// ── 导入导出（导入导出 v0.2）───────────────────────────

export interface FailureDetail {
  rowNo: number
  columnCode: string
  columnName: string
  rawValue: string
  errorCode: string
  errorMsg: string
}

export interface ImportTask {
  id: string
  taskNo: string
  moduleCode: string
  fileName: string
  direction: ImportDirection
  status: ImportTaskStatus
  totalCount: number
  successCount: number
  failCount: number
  canExecute: boolean
  failures?: FailureDetail[]
  failReportUrl: string | null
  fileUrl: string | null
  operatorId: string
  operatorName: string
  createdAt: string
}

export interface ExportRequest {
  moduleCode: string
  filter?: FilterDsl
  sort?: SortSpec[]
  pageSize?: number
}

/** POST /api/{resource}/search 请求体（通用规范 2.10 v1.9：标准列表查询统一走 POST search） */
export interface SearchRequest {
  page?: number
  pageSize?: number
  keyword?: string
  filter?: FilterDsl
  sort?: SortSpec[]
  /** 固定参数（各接口契约声明的 eq/contains 快捷参数） */
  [fixedParam: string]: unknown
}

/** 前端列表查询状态（与 SearchRequest 同构；页面筛选/排序后由 toSearchBody 构造请求体） */
export type ListQuery = SearchRequest
