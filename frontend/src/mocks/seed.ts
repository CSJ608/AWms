/**
 * MSW mock 种子数据 —— 严格按契约 DTO 形态（认证权限 v0.2 / 物料 v0.3 / 仓库库位 v0.2 /
 * 来源 v0.2 / 批次 v0.4 / 导入导出 v0.2 / 枚举与错误码 v1.6）。
 */
import type {
  AttachmentItem, BatchItem, InboundOrder, LocationItem, MaterialItem, PermissionItem, PrintJob, QualityExceptionItem,
  Receipt, SourceItem, UserItem, WarehouseItem,
} from '../api/types'

export const NOW = '2026-08-10T08:00:00Z'

export const MOCK_TOKEN_PREFIX = 'mock-token-'
/** 测试用：模拟过期 token（触发 401 刷新链路） */
export const MOCK_EXPIRED_TOKEN = 'mock-expired-token'

/** 所有 Mock 主键也遵守生产 UUID 契约，避免短字符串掩盖真实绑定错误。 */
export function mockUuid(namespace: number, index: number): string {
  return `${namespace.toString(16).padStart(8, '0')}-0000-4000-8000-${index.toString(16).padStart(12, '0')}`
}

export const MOCK_IDS = {
  userAdmin: mockUuid(2, 1), userOperator: mockUuid(2, 2), userSupervisor: mockUuid(2, 3), userDisabled: mockUuid(2, 4),
  roleAdmin: mockUuid(3, 1), roleOperator: mockUuid(3, 2), roleSupervisor: mockUuid(3, 3),
  material1: mockUuid(4, 1), material2: mockUuid(4, 2), material3: mockUuid(4, 3), material4: mockUuid(4, 4), material6: mockUuid(4, 6),
  warehouse1: mockUuid(5, 1), warehouse2: mockUuid(5, 2), warehouse3: mockUuid(5, 3),
  locationStaging1: mockUuid(6, 1), locationDefault1: mockUuid(6, 2), locationDefault2: mockUuid(6, 3),
  locationStaging2: mockUuid(6, 4), locationDefault3: mockUuid(6, 5), locationStaging3: mockUuid(6, 6),
  sourceSupplier1: mockUuid(7, 1), sourceSupplier2: mockUuid(7, 2), sourceSupplier3: mockUuid(7, 3), sourceWorkshop1: mockUuid(7, 4), sourceWorkshop2: mockUuid(7, 5),
  batch1: mockUuid(8, 1), batch2: mockUuid(8, 2), batch3: mockUuid(8, 3), batch4: mockUuid(8, 4),
  inboundOrder1: mockUuid(9, 1), inboundOrder2: mockUuid(9, 2),
  inboundOrderLine1: mockUuid(10, 1), inboundOrderLine2: mockUuid(10, 2), inboundOrderLine3: mockUuid(10, 3),
  receipt1: mockUuid(11, 1), receipt2: mockUuid(11, 2), receipt3: mockUuid(11, 3), receipt4: mockUuid(11, 4), receipt5: mockUuid(11, 5),
  receiptLine1: mockUuid(12, 1), receiptLine2: mockUuid(12, 2), receiptLine3: mockUuid(12, 3), receiptLine4: mockUuid(12, 4), receiptLine5: mockUuid(12, 5),
  quality1: mockUuid(13, 1), attachment1: mockUuid(14, 1), attachment2: mockUuid(14, 2),
} as const

// ── 权限点（模块注册表，只读；与后端注册表对齐：route/menu.<moduleCode>）──────────
const seedPermissionRows: PermissionItem[] = [
  // 路由
  { id: 'p-route-master', code: 'route.master-data', name: '主数据', category: 'ROUTE', moduleCode: 'master-data' },
  { id: 'p-route-inbound', code: 'route.inbound', name: '入库', category: 'ROUTE', moduleCode: 'inbound' },
  { id: 'p-route-system', code: 'route.system', name: '系统管理', category: 'ROUTE', moduleCode: 'system' },
  // 菜单
  { id: 'p-menu-master', code: 'menu.master-data', name: '主数据菜单', category: 'MENU', moduleCode: 'master-data' },
  { id: 'p-menu-inbound', code: 'menu.inbound', name: '入库菜单', category: 'MENU', moduleCode: 'inbound' },
  { id: 'p-menu-system', code: 'menu.system', name: '系统管理菜单', category: 'MENU', moduleCode: 'system' },
  // 操作：入库链
  { id: 'p-act-io-c', code: 'action.inbound-order.create', name: '创建入库单', category: 'ACTION', moduleCode: 'inbound' },
  { id: 'p-act-io-v', code: 'action.inbound-order.void', name: '作废入库单', category: 'ACTION', moduleCode: 'inbound' },
  { id: 'p-act-rcp-c', code: 'action.receiving.create', name: '提交收货', category: 'ACTION', moduleCode: 'inbound' },
  { id: 'p-act-qc-c', code: 'action.quality.check', name: 'PDA 质检', category: 'ACTION', moduleCode: 'inbound' },
  { id: 'p-act-qc-r', code: 'action.quality.resolve', name: '处理质检异常', category: 'ACTION', moduleCode: 'inbound' },
  { id: 'p-act-put-c', code: 'action.putaway.create', name: 'PDA 上架', category: 'ACTION', moduleCode: 'inbound' },
  { id: 'p-act-att-u', code: 'action.attachment.upload', name: '上传业务照片', category: 'ACTION', moduleCode: 'inbound' },
  { id: 'p-act-print-c', code: 'action.print.create', name: '生成/补打固定模板', category: 'ACTION', moduleCode: 'inbound' },
  // 操作：物料
  { id: 'p-act-mat-c', code: 'action.material.create', name: '新建物料', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-mat-u', code: 'action.material.edit', name: '编辑物料', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-mat-d', code: 'action.material.delete', name: '删除物料', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-mat-i', code: 'action.import', name: '导入', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-mat-e', code: 'action.export', name: '导出', category: 'ACTION', moduleCode: 'master-data' },
  // 操作：仓库
  { id: 'p-act-wh-c', code: 'action.warehouse.create', name: '新建仓库', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-wh-u', code: 'action.warehouse.edit', name: '编辑仓库', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-wh-d', code: 'action.warehouse.delete', name: '删除仓库', category: 'ACTION', moduleCode: 'master-data' },
  // 操作：库位
  { id: 'p-act-loc-c', code: 'action.location.create', name: '新建库位', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-loc-u', code: 'action.location.edit', name: '编辑库位', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-loc-d', code: 'action.location.delete', name: '删除库位', category: 'ACTION', moduleCode: 'master-data' },
  // 操作：来源
  { id: 'p-act-src-c', code: 'action.source.create', name: '新建来源', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-src-u', code: 'action.source.edit', name: '编辑来源', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-src-d', code: 'action.source.delete', name: '删除来源', category: 'ACTION', moduleCode: 'master-data' },
]
export const seedPermissions: PermissionItem[] = seedPermissionRows.map((permission, index) => ({ ...permission, id: mockUuid(1, index + 1) }))

const ALL_PERMISSIONS = seedPermissions.map((p) => p.code)
// 与后端默认角色一致：OPERATOR 仅 inbound（无 master-data 菜单/路由/操作权限）
const OPERATOR_PERMISSIONS = [
  'route.inbound',
  'menu.inbound',
  'action.receiving.create',
  'action.quality.check',
  'action.putaway.create',
  'action.attachment.upload',
  'action.print.create',
]
// 与后端默认角色一致：SUPERVISOR = 入库 + 主数据（含物料/导入导出 + 仓库/库位/来源 9 个新增操作码）
const SUPERVISOR_PERMISSIONS = [
  ...OPERATOR_PERMISSIONS,
  'action.inbound-order.create',
  'action.inbound-order.void',
  'action.quality.resolve',
  'route.master-data',
  'menu.master-data',
  'action.material.create',
  'action.material.edit',
  'action.material.delete',
  'action.import',
  'action.export',
  'action.warehouse.create',
  'action.warehouse.edit',
  'action.warehouse.delete',
  'action.location.create',
  'action.location.edit',
  'action.location.delete',
  'action.source.create',
  'action.source.edit',
  'action.source.delete',
]

// ── 用户 / 角色 ───────────────────────────────────────
export const seedUsers: UserItem[] = [
  {
    id: MOCK_IDS.userAdmin,
    username: 'admin',
    name: '系统管理员',
    status: 'ACTIVE',
    roles: [
      { id: MOCK_IDS.roleAdmin, code: 'SYSTEM_ADMIN', name: '系统管理员', permissionCodes: ALL_PERMISSIONS, createdAt: NOW },
    ],
    createdAt: NOW,
  },
  {
    id: MOCK_IDS.userOperator,
    username: 'wang01',
    name: '王仓管',
    status: 'ACTIVE',
    roles: [
      { id: MOCK_IDS.roleOperator, code: 'OPERATOR', name: '作业员', permissionCodes: OPERATOR_PERMISSIONS, createdAt: NOW },
    ],
    createdAt: NOW,
  },
  {
    id: MOCK_IDS.userSupervisor,
    username: 'zhang03',
    name: '张组长',
    status: 'ACTIVE',
    roles: [
      { id: MOCK_IDS.roleSupervisor, code: 'SUPERVISOR', name: '仓管', permissionCodes: SUPERVISOR_PERMISSIONS, createdAt: NOW },
    ],
    createdAt: NOW,
  },
  {
    id: MOCK_IDS.userDisabled,
    username: 'li02',
    name: '李停用',
    status: 'DISABLED',
    roles: [
      { id: MOCK_IDS.roleOperator, code: 'OPERATOR', name: '作业员', permissionCodes: OPERATOR_PERMISSIONS, createdAt: NOW },
    ],
    createdAt: NOW,
  },
]

/** username → password（mock 明文，仅用于开发/测试） */
export const SEED_PASSWORDS: Record<string, string> = {
  admin: 'admin123',
  wang01: '123456',
  zhang03: '123456',
  li02: '123456',
}

export function permissionsOf(username: string): string[] {
  return seedUsers.find((u) => u.username === username)?.roles[0]?.permissionCodes ?? []
}

// ── 菜单（模块注册表，与真实后端一致：dashboard/inbound/master-data/system 四项）──────
export const ALL_MENUS = {
  web: [
    // 工作台（dashboard 无权限码 = 所有登录用户可见占位页，对齐协调者 C4① 裁决）
    { path: '/', titleKey: 'nav.workspace', groupKey: 'nav.group.workspace', moduleCode: 'dashboard', iconKey: 'dashboard', sort: 5 },
    // 入库（占位页；menu.inbound 权限）
    { path: '/inbound', titleKey: 'nav.inbound', groupKey: 'nav.group.operations', moduleCode: 'inbound', iconKey: 'scan', sort: 20 },
    { path: '/web/master/materials', titleKey: 'nav.material', groupKey: 'nav.group.master', moduleCode: 'master-data', iconKey: 'package', sort: 10 },
    { path: '/web/master/warehouses', titleKey: 'nav.warehouse', groupKey: 'nav.group.master', moduleCode: 'master-data', iconKey: 'warehouse', sort: 20 },
    { path: '/web/master/sources', titleKey: 'nav.source', groupKey: 'nav.group.master', moduleCode: 'master-data', iconKey: 'truck', sort: 30 },
    { path: '/web/master/batches', titleKey: 'nav.batch', groupKey: 'nav.group.master', moduleCode: 'master-data', iconKey: 'layers', sort: 40 },
    // 系统管理（占位页；menu.system 权限）
    { path: '/system', titleKey: 'nav.system', groupKey: 'nav.group.settings', moduleCode: 'system', iconKey: 'shield', sort: 40 },
  ],
  pda: [
    { code: 'receiving', titleKey: 'pda.receiving', moduleCode: 'inbound', sort: 10 },
    { code: 'qc', titleKey: 'pda.qc', moduleCode: 'inbound', sort: 20 },
    { code: 'putaway', titleKey: 'pda.putaway', moduleCode: 'inbound', sort: 30 },
  ],
} as const

/** 菜单过滤：menu.<moduleCode> 权限码（后端按角色过滤后返回，前端只渲染）；
 * dashboard 无权限码 = 所有登录用户可见。 */
export function menusFor(username: string) {
  const perms = permissionsOf(username)
  return {
    web: ALL_MENUS.web.filter((m) => m.moduleCode === 'dashboard' || perms.includes(`menu.${m.moduleCode}`)),
    pda: ALL_MENUS.pda.filter((m) => perms.includes('route.inbound') && perms.includes(pdaActionPermission(m.code))),
  }
}

function pdaActionPermission(code: string): string {
  if (code === 'receiving') return 'action.receiving.create'
  if (code === 'qc') return 'action.quality.check'
  return 'action.putaway.create'
}

// ── 物料 ──────────────────────────────────────────────
function iso(daysAgo: number): string {
  const d = new Date('2026-08-10T00:00:00Z')
  d.setUTCDate(d.getUTCDate() - daysAgo)
  return d.toISOString()
}

const MATERIAL_NAMES: Array<[string, string, boolean, string, string, string | null]> = [
  ['MAT-001', '螺母 M6', true, 'SKU', 'CT', '10.0000'],
  ['MAT-002', '垫片 8mm', false, 'NONE', 'PC', null],
  ['MAT-003', '螺栓 M8x30', true, 'SKU', 'PC', '50.0000'],
  ['MAT-004', '钢板 2mm', true, 'UNIQUE', 'KG', '1.0000'],
  ['MAT-005', '铝型材 4040', false, 'NONE', 'M', null],
  ['MAT-006', '轴承 6204', true, 'SKU', 'PC', '1.0000'],
  ['MAT-007', '密封圈 25mm', false, 'NONE', 'PC', null],
  ['MAT-008', '气缸 SC32', true, 'SKU', 'PC', '1.0000'],
  ['MAT-009', '电磁阀 4V210', true, 'UNIQUE', 'PC', '1.0000'],
  ['MAT-010', '继电器 24V', false, 'NONE', 'PC', null],
  ['MAT-011', 'PLC 模块', true, 'SKU', 'PC', '1.0000'],
  ['MAT-012', '伺服电机 400W', true, 'SKU', 'PC', '1.0000'],
  ['MAT-013', '减速机 1:20', false, 'NONE', 'PC', null],
  ['MAT-014', '同步带 5M', true, 'SKU', 'M', '2.0000'],
  ['MAT-015', '齿轮 20T', false, 'NONE', 'PC', null],
  ['MAT-016', '链条 08B', true, 'SKU', 'M', '1.0000'],
  ['MAT-017', '焊丝 1.2mm', true, 'UNIQUE', 'KG', '5.0000'],
  ['MAT-018', '油漆 灰色', false, 'NONE', 'L', null],
  ['MAT-019', '稀释剂', false, 'NONE', 'L', null],
  ['MAT-020', '砂纸 120目', true, 'SKU', 'PC', '100.0000'],
  ['MAT-021', '标签纸 60x40', true, 'SKU', 'BOX', '1.0000'],
  ['MAT-022', '碳带 110mm', false, 'NONE', 'BOX', null],
  ['MAT-023', '扎带 200mm', true, 'SKU', 'BOX', '10.0000'],
  ['MAT-024', '波纹管 20mm', false, 'NONE', 'M', null],
  ['MAT-025', '插头 16A', true, 'SKU', 'PC', '1.0000'],
]

const SEARCH_CODES = ['LM', 'DP', 'LS', 'GB', 'LX', 'ZC', 'MFQ', 'QG', 'DCF', 'JDQ', 'PL', 'SD', 'JS', 'TBD', 'CL', 'LT', 'HS', 'YQ', 'XS', 'SZ', 'BQZ', 'TD', 'ZD', 'BWG', 'CT']

export const seedMaterials: MaterialItem[] = MATERIAL_NAMES.map(([code, name, batchControlled, labelType, defaultUom, qty], i) => ({
  id: mockUuid(4, i + 1),
  code,
  name,
  searchCode: SEARCH_CODES[i],
  batchControlled,
  labelType: labelType as MaterialItem['labelType'],
  defaultUom: defaultUom as MaterialItem['defaultUom'],
  defaultQtyPerLabel: qty,
  status: i % 5 === 4 ? 'DISABLED' : 'ENABLED', // 20% 停用
  createdAt: iso(i % 7),
  updatedAt: iso(i % 7),
}))

// ── 仓库 / 库位 ───────────────────────────────────────
export const seedWarehouses: WarehouseItem[] = [
  { id: MOCK_IDS.warehouse1, code: 'WH-01', name: '一号仓', searchCode: 'YHC', status: 'ENABLED', mgmtMode: 'MANUAL', createdAt: iso(10) },
  { id: MOCK_IDS.warehouse2, code: 'WH-02', name: '二号仓', searchCode: 'EHC', status: 'ENABLED', mgmtMode: 'MANUAL', createdAt: iso(8) },
  { id: MOCK_IDS.warehouse3, code: 'WH-03', name: '三号仓', searchCode: 'SHC', status: 'DISABLED', mgmtMode: 'MANUAL', createdAt: iso(5) },
]

export const seedLocations: LocationItem[] = [
  { id: MOCK_IDS.locationStaging1, warehouseId: MOCK_IDS.warehouse1, warehouseCode: 'WH-01', code: 'STG-01', searchCode: 'ZC', type: 'STAGING', status: 'ENABLED', reachability: 'UNIVERSAL', createdAt: iso(9) },
  { id: MOCK_IDS.locationDefault1, warehouseId: MOCK_IDS.warehouse1, warehouseCode: 'WH-01', code: 'DEF-01', searchCode: 'MR', type: 'DEFAULT', status: 'ENABLED', reachability: 'UNIVERSAL', createdAt: iso(9) },
  { id: MOCK_IDS.locationDefault2, warehouseId: MOCK_IDS.warehouse1, warehouseCode: 'WH-01', code: 'DEF-02', searchCode: 'MR2', type: 'DEFAULT', status: 'ENABLED', reachability: 'MANUAL_ONLY', createdAt: iso(9) },
  { id: MOCK_IDS.locationStaging2, warehouseId: MOCK_IDS.warehouse2, warehouseCode: 'WH-02', code: 'STG-01', searchCode: 'ZC', type: 'STAGING', status: 'ENABLED', reachability: 'UNIVERSAL', createdAt: iso(7) },
  { id: MOCK_IDS.locationDefault3, warehouseId: MOCK_IDS.warehouse2, warehouseCode: 'WH-02', code: 'DEF-01', searchCode: 'MR', type: 'DEFAULT', status: 'ENABLED', reachability: 'UNIVERSAL', createdAt: iso(7) },
  { id: MOCK_IDS.locationStaging3, warehouseId: MOCK_IDS.warehouse3, warehouseCode: 'WH-03', code: 'STG-01', searchCode: 'ZC', type: 'STAGING', status: 'DISABLED', reachability: 'UNIVERSAL', createdAt: iso(4) },
]

// ── 来源 ──────────────────────────────────────────────
export const seedSources: SourceItem[] = [
  { id: MOCK_IDS.sourceSupplier1, type: 'SUPPLIER', code: 'SUP-001', name: '华东五金', searchCode: 'HDWJ', status: 'ENABLED', createdAt: iso(12) },
  { id: MOCK_IDS.sourceSupplier2, type: 'SUPPLIER', code: 'SUP-002', name: '华南钢材', searchCode: 'HNGC', status: 'ENABLED', createdAt: iso(11) },
  { id: MOCK_IDS.sourceSupplier3, type: 'SUPPLIER', code: 'SUP-003', name: '天成电气', searchCode: 'TCDQ', status: 'ENABLED', createdAt: iso(6) },
  { id: MOCK_IDS.sourceWorkshop1, type: 'WORKSHOP', code: 'WS-01', name: '一车间', searchCode: 'YCJ', status: 'ENABLED', createdAt: iso(3) },
  { id: MOCK_IDS.sourceWorkshop2, type: 'WORKSHOP', code: 'WS-02', name: '二车间', searchCode: 'ECJ', status: 'DISABLED', createdAt: iso(2) },
]

// ── 批次（系统自动建，只读）────────────────────────────
export const seedBatches: BatchItem[] = [
  {
    id: MOCK_IDS.batch1, materialId: MOCK_IDS.material1, materialCode: 'MAT-001', materialName: '螺母 M6',
    batchNo: '260810001', sourceBatchNo: 'PRD-260809-01', sourceType: 'WORKSHOP', sourceCode: 'WS-01',
    productionDate: '2026-08-09', expiryDate: null, status: 'ACTIVE', createdAt: iso(1),
  },
  {
    id: MOCK_IDS.batch2, materialId: MOCK_IDS.material1, materialCode: 'MAT-001', materialName: '螺母 M6',
    batchNo: '260810002', sourceBatchNo: 'PRD-260808-01', sourceType: 'WORKSHOP', sourceCode: 'WS-01',
    productionDate: '2026-08-08', expiryDate: null, status: 'ACTIVE', createdAt: iso(2),
  },
  {
    id: MOCK_IDS.batch3, materialId: MOCK_IDS.material2, materialCode: 'MAT-002', materialName: '垫片 8mm',
    batchNo: '260809001', sourceBatchNo: 'PO-20260805-01', sourceType: 'SUPPLIER', sourceCode: 'SUP-001',
    productionDate: '2026-08-09', expiryDate: '2027-08-09', status: 'ACTIVE', createdAt: iso(1),
  },
  {
    id: MOCK_IDS.batch4, materialId: MOCK_IDS.material4, materialCode: 'MAT-004', materialName: '钢板 2mm',
    batchNo: '260808001', sourceBatchNo: 'PO-20260803-02', sourceType: 'SUPPLIER', sourceCode: 'SUP-002',
    productionDate: '2026-08-08', expiryDate: null, status: 'ACTIVE', createdAt: iso(2),
  },
]

// ── 入库链（第 4 批演示与 MSW 验收种子）────────────────

export const seedInboundOrders: InboundOrder[] = [
  {
    id: MOCK_IDS.inboundOrder1,
    orderNo: 'PO-20260819-0001',
    type: 'PO',
    warehouseId: MOCK_IDS.warehouse1,
    warehouseCode: 'WH-01',
    sourceType: 'SUPPLIER',
    sourceCode: 'SUP-001',
    status: 'CONFIRMED',
    lines: [
      {
        id: MOCK_IDS.inboundOrderLine1,
        lineNo: 1,
        materialId: MOCK_IDS.material1,
        materialCode: 'MAT-001',
        materialName: '螺母 M6',
        expectedQty: '200.0000',
        receivedQty: '0.0000',
        remainingQty: '200.0000',
        uniqueCodes: [],
      },
      {
        id: MOCK_IDS.inboundOrderLine2,
        lineNo: 2,
        materialId: MOCK_IDS.material4,
        materialCode: 'MAT-004',
        materialName: '钢板 2mm',
        expectedQty: '10.0000',
        receivedQty: '0.0000',
        remainingQty: '10.0000',
        uniqueCodes: [
          { code: 'BOX-20260820-0001', quantity: '5.0000', status: 'UNRECEIVED', receivedAt: null },
          { code: 'BOX-20260820-0002', quantity: '5.0000', status: 'UNRECEIVED', receivedAt: null },
        ],
      },
    ],
    createdAt: '2026-08-19T02:20:00Z',
    createdBy: '张组长',
    voidedAt: null,
    voidedBy: null,
    voidReason: null,
  },
  {
    id: MOCK_IDS.inboundOrder2,
    orderNo: 'PR-20260819-0002',
    type: 'PR',
    warehouseId: MOCK_IDS.warehouse1,
    warehouseCode: 'WH-01',
    sourceType: 'WORKSHOP',
    sourceCode: 'WS-01',
    status: 'RECEIVING',
    lines: [
      {
        id: MOCK_IDS.inboundOrderLine3,
        lineNo: 1,
        materialId: MOCK_IDS.material6,
        materialCode: 'MAT-006',
        materialName: '轴承 6204',
        expectedQty: '50.0000',
        receivedQty: '20.0000',
        remainingQty: '30.0000',
        uniqueCodes: [],
      },
    ],
    createdAt: '2026-08-19T04:10:00Z',
    createdBy: '张组长',
    voidedAt: null,
    voidedBy: null,
    voidReason: null,
  },
]

export const seedReceipts: Receipt[] = [
  {
    id: MOCK_IDS.receipt1,
    receiptNo: 'RCP-20260819-0001',
    warehouseId: MOCK_IDS.warehouse1,
    warehouseCode: 'WH-01',
    inboundOrderId: null,
    sourceDocType: 'PR',
    sourceDocNo: null,
    sourceType: 'WORKSHOP',
    sourceCode: 'WS-01',
    status: 'RECEIVING',
    lines: [
      {
        id: MOCK_IDS.receiptLine1,
        lineNo: 1,
        orderLineId: null,
        orderLineNo: null,
        materialId: MOCK_IDS.material1,
        materialCode: 'MAT-001',
        materialName: '螺母 M6',
        batchId: MOCK_IDS.batch1,
        batchNo: '260810001',
        expectedQty: null,
        actualQty: '200.0000',
        qtyDiff: null,
        status: 'RECEIVED',
        sourceBatchNo: 'PRD-260809-01',
        productionDate: '2026-08-09',
        expiryDate: null,
      },
    ],
    stagingLocationId: MOCK_IDS.locationStaging1,
    stagingLocationCode: 'STG-01',
    photos: [MOCK_IDS.attachment1],
    operatorId: MOCK_IDS.userOperator,
    operatorName: '王仓管',
    occurredAt: '2026-08-19T05:20:00Z',
  },
  {
    id: MOCK_IDS.receipt2,
    receiptNo: 'RCP-20260819-0002',
    warehouseId: MOCK_IDS.warehouse1,
    warehouseCode: 'WH-01',
    inboundOrderId: null,
    sourceDocType: 'PR',
    sourceDocNo: null,
    sourceType: 'WORKSHOP',
    sourceCode: 'WS-01',
    status: 'PUTAWAY',
    lines: [
      {
        id: MOCK_IDS.receiptLine2,
        lineNo: 1,
        orderLineId: null,
        orderLineNo: null,
        materialId: MOCK_IDS.material1,
        materialCode: 'MAT-001',
        materialName: '螺母 M6',
        batchId: MOCK_IDS.batch1,
        batchNo: '260810001',
        expectedQty: null,
        actualQty: '200.0000',
        qtyDiff: null,
        status: 'CHECKED',
        sourceBatchNo: 'PRD-260809-01',
        productionDate: '2026-08-09',
        expiryDate: null,
      },
    ],
    stagingLocationId: MOCK_IDS.locationStaging1,
    stagingLocationCode: 'STG-01',
    photos: [],
    operatorId: MOCK_IDS.userOperator,
    operatorName: '王仓管',
    occurredAt: '2026-08-19T06:00:00Z',
  },
  {
    id: MOCK_IDS.receipt3,
    receiptNo: 'RCP-20260819-0003',
    warehouseId: MOCK_IDS.warehouse1,
    warehouseCode: 'WH-01',
    inboundOrderId: null,
    sourceDocType: 'PR',
    sourceDocNo: null,
    sourceType: 'WORKSHOP',
    sourceCode: 'WS-01',
    status: 'CHECKING',
    lines: [
      {
        id: MOCK_IDS.receiptLine3,
        lineNo: 1,
        orderLineId: null,
        orderLineNo: null,
        materialId: MOCK_IDS.material6,
        materialCode: 'MAT-006',
        materialName: '轴承 6204',
        batchId: MOCK_IDS.batch1,
        batchNo: '260810001',
        expectedQty: null,
        actualQty: '30.0000',
        qtyDiff: null,
        status: 'EXCEPTION',
        sourceBatchNo: 'PRD-260809-01',
        productionDate: '2026-08-09',
        expiryDate: null,
      },
    ],
    stagingLocationId: MOCK_IDS.locationStaging1,
    stagingLocationCode: 'STG-01',
    photos: [],
    operatorId: MOCK_IDS.userOperator,
    operatorName: '王仓管',
    occurredAt: '2026-08-19T06:30:00Z',
  },
  {
    id: MOCK_IDS.receipt4,
    receiptNo: 'RCP-20260819-0004',
    warehouseId: MOCK_IDS.warehouse1,
    warehouseCode: 'WH-01',
    inboundOrderId: null,
    sourceDocType: 'PR',
    sourceDocNo: null,
    sourceType: 'WORKSHOP',
    sourceCode: 'WS-01',
    status: 'RECEIVING',
    lines: [
      {
        id: MOCK_IDS.receiptLine4,
        lineNo: 1,
        orderLineId: null,
        orderLineNo: null,
        materialId: MOCK_IDS.material1,
        materialCode: 'MAT-001',
        materialName: '螺母 M6',
        batchId: MOCK_IDS.batch1,
        batchNo: '260810001',
        expectedQty: null,
        actualQty: '40.0000',
        qtyDiff: null,
        status: 'RECEIVED',
        sourceBatchNo: 'PRD-260809-01',
        productionDate: '2026-08-09',
        expiryDate: null,
      },
    ],
    stagingLocationId: MOCK_IDS.locationStaging1,
    stagingLocationCode: 'STG-01',
    photos: [],
    operatorId: MOCK_IDS.userOperator,
    operatorName: '王仓管',
    occurredAt: '2026-08-19T07:20:00Z',
  },
  {
    id: MOCK_IDS.receipt5,
    receiptNo: 'RCP-20260819-0005',
    warehouseId: MOCK_IDS.warehouse1,
    warehouseCode: 'WH-01',
    inboundOrderId: null,
    sourceDocType: 'PO',
    sourceDocNo: 'PO-20260818-0009',
    sourceType: 'SUPPLIER',
    sourceCode: 'SUP-001',
    status: 'RECEIVING',
    lines: [
      {
        id: MOCK_IDS.receiptLine5,
        lineNo: 1,
        orderLineId: null,
        orderLineNo: null,
        materialId: MOCK_IDS.material2,
        materialCode: 'MAT-002',
        materialName: '垫片 8mm',
        batchId: MOCK_IDS.batch3,
        batchNo: '260809001',
        expectedQty: null,
        actualQty: '12.0000',
        qtyDiff: null,
        status: 'RECEIVED',
        sourceBatchNo: 'PO-20260805-01',
        productionDate: '2026-08-09',
        expiryDate: '2027-08-09',
      },
    ],
    stagingLocationId: MOCK_IDS.locationStaging1,
    stagingLocationCode: 'STG-01',
    photos: [],
    operatorId: MOCK_IDS.userOperator,
    operatorName: '王仓管',
    occurredAt: '2026-08-19T07:35:00Z',
  },
]

export const seedQualityChecks: QualityExceptionItem[] = [
  {
    id: MOCK_IDS.quality1,
    receiptLineId: MOCK_IDS.receiptLine3,
    receiptNo: 'RCP-20260819-0003',
    warehouseId: MOCK_IDS.warehouse1,
    warehouseCode: 'WH-01',
    materialCode: 'MAT-006',
    materialName: '轴承 6204',
    batchNo: '260810001',
    checkedQty: '30.0000',
    exceptionReason: 'DAMAGED',
    note: '外箱破损',
    photoIds: [MOCK_IDS.attachment2],
    checkedBy: MOCK_IDS.userOperator,
    checkedByName: '王仓管',
    checkedAt: '2026-08-19T07:00:00Z',
    resolutionAction: null,
    resolutionNote: null,
    resolvedBy: null,
    resolvedByName: null,
    resolvedAt: null,
  },
]

export const seedAttachments: AttachmentItem[] = [
  {
    id: MOCK_IDS.attachment1,
    fileName: 'receipt-001.png',
    mimeType: 'image/png',
    size: 128000,
    bizType: 'RECEIPT',
    bizId: MOCK_IDS.receipt1,
    uploadedBy: MOCK_IDS.userOperator,
    uploadedByName: '王仓管',
    uploadedAt: '2026-08-19T05:18:00Z',
    url: `/api/attachments/${MOCK_IDS.attachment1}`,
    thumbnailUrl: `/api/attachments/${MOCK_IDS.attachment1}/thumbnail`,
  },
  {
    id: MOCK_IDS.attachment2,
    fileName: 'exception-001.png',
    mimeType: 'image/png',
    size: 156000,
    bizType: 'EXCEPTION',
    bizId: MOCK_IDS.quality1,
    uploadedBy: MOCK_IDS.userOperator,
    uploadedByName: '王仓管',
    uploadedAt: '2026-08-19T06:58:00Z',
    url: `/api/attachments/${MOCK_IDS.attachment2}`,
    thumbnailUrl: `/api/attachments/${MOCK_IDS.attachment2}/thumbnail`,
  },
]

export const seedPrintJobs: PrintJob[] = []
