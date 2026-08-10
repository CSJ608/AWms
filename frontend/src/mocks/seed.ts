/**
 * MSW mock 种子数据 —— 严格按契约 DTO 形态（认证权限 v0.2 / 物料 v0.3 / 仓库库位 v0.2 /
 * 来源 v0.2 / 批次 v0.4 / 导入导出 v0.2 / 枚举与错误码 v1.6）。
 */
import type {
  BatchItem, LocationItem, MaterialItem, PermissionItem, SourceItem, UserItem, WarehouseItem,
} from '../api/types'

export const NOW = '2026-08-10T08:00:00Z'

export const MOCK_TOKEN_PREFIX = 'mock-token-'
/** 测试用：模拟过期 token（触发 401 刷新链路） */
export const MOCK_EXPIRED_TOKEN = 'mock-expired-token'

// ── 权限点（模块注册表，只读；与后端注册表对齐：route/menu.<moduleCode>）──────────
export const seedPermissions: PermissionItem[] = [
  // 路由
  { id: 'p-route-master', code: 'route.master-data', name: '主数据', category: 'ROUTE', moduleCode: 'master-data' },
  // 菜单
  { id: 'p-menu-master', code: 'menu.master-data', name: '主数据菜单', category: 'MENU', moduleCode: 'master-data' },
  // 操作：物料
  { id: 'p-act-mat-c', code: 'action.material.create', name: '新建物料', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-mat-u', code: 'action.material.edit', name: '编辑物料', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-mat-d', code: 'action.material.delete', name: '删除物料', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-mat-i', code: 'action.import', name: '导入', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-mat-e', code: 'action.export', name: '导出', category: 'ACTION', moduleCode: 'master-data' },
  // 操作：仓库
  { id: 'p-act-wh-c', code: 'action.warehouse.create', name: '新建仓库', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-wh-u', code: 'action.warehouse.update', name: '编辑仓库', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-wh-d', code: 'action.warehouse.delete', name: '删除仓库', category: 'ACTION', moduleCode: 'master-data' },
  // 操作：库位
  { id: 'p-act-loc-c', code: 'action.location.create', name: '新建库位', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-loc-u', code: 'action.location.update', name: '编辑库位', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-loc-d', code: 'action.location.delete', name: '删除库位', category: 'ACTION', moduleCode: 'master-data' },
  // 操作：来源
  { id: 'p-act-src-c', code: 'action.source.create', name: '新建来源', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-src-u', code: 'action.source.update', name: '编辑来源', category: 'ACTION', moduleCode: 'master-data' },
  { id: 'p-act-src-d', code: 'action.source.delete', name: '删除来源', category: 'ACTION', moduleCode: 'master-data' },
]

const ALL_PERMISSIONS = seedPermissions.map((p) => p.code)
// 与后端默认角色一致：OPERATOR 仅 inbound（无 master-data 菜单/路由/操作权限）
const OPERATOR_PERMISSIONS = ['route.inbound', 'menu.inbound', 'action.receiving.create']

// ── 用户 / 角色 ───────────────────────────────────────
export const seedUsers: UserItem[] = [
  {
    id: 'u-admin',
    username: 'admin',
    name: '系统管理员',
    status: 'ACTIVE',
    roles: [
      { id: 'r-admin', code: 'SYSTEM_ADMIN', name: '系统管理员', permissionCodes: ALL_PERMISSIONS, createdAt: NOW },
    ],
    createdAt: NOW,
  },
  {
    id: 'u-wang01',
    username: 'wang01',
    name: '王仓管',
    status: 'ACTIVE',
    roles: [
      { id: 'r-op', code: 'OPERATOR', name: '作业员', permissionCodes: OPERATOR_PERMISSIONS, createdAt: NOW },
    ],
    createdAt: NOW,
  },
  {
    id: 'u-li02',
    username: 'li02',
    name: '李停用',
    status: 'DISABLED',
    roles: [
      { id: 'r-op', code: 'OPERATOR', name: '作业员', permissionCodes: OPERATOR_PERMISSIONS, createdAt: NOW },
    ],
    createdAt: NOW,
  },
]

/** username → password（mock 明文，仅用于开发/测试） */
export const SEED_PASSWORDS: Record<string, string> = {
  admin: 'admin123',
  wang01: '123456',
  li02: '123456',
}

export function permissionsOf(username: string): string[] {
  return seedUsers.find((u) => u.username === username)?.roles[0]?.permissionCodes ?? []
}

// ── 菜单（模块注册表）──────────────────────────────────
export const ALL_MENUS = {
  web: [
    { path: '/web/master/materials', titleKey: 'nav.material', groupKey: 'nav.group.master', moduleCode: 'master-data', iconKey: 'package', sort: 10 },
    { path: '/web/master/warehouses', titleKey: 'nav.warehouse', groupKey: 'nav.group.master', moduleCode: 'master-data', iconKey: 'warehouse', sort: 20 },
    { path: '/web/master/sources', titleKey: 'nav.source', groupKey: 'nav.group.master', moduleCode: 'master-data', iconKey: 'truck', sort: 30 },
    { path: '/web/master/batches', titleKey: 'nav.batch', groupKey: 'nav.group.master', moduleCode: 'master-data', iconKey: 'layers', sort: 40 },
  ],
  pda: [
    { code: 'receiving', titleKey: 'pda.receiving', moduleCode: 'inbound', sort: 10 },
    { code: 'qc', titleKey: 'pda.qc', moduleCode: 'inbound', sort: 20 },
    { code: 'putaway', titleKey: 'pda.putaway', moduleCode: 'inbound', sort: 30 },
  ],
} as const

/** 菜单过滤：menu.<moduleCode> 权限码（后端按角色过滤后返回，前端只渲染） */
export function menusFor(username: string) {
  const perms = permissionsOf(username)
  return {
    web: ALL_MENUS.web.filter((m) => perms.includes(`menu.${m.moduleCode}`)),
    pda: ALL_MENUS.pda.filter((m) => perms.includes(`pda.${m.code}`)),
  }
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
  id: `mat-${String(i + 1).padStart(3, '0')}`,
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
  { id: 'wh-01', code: 'WH-01', name: '一号仓', searchCode: 'YHC', status: 'ENABLED', mgmtMode: 'MANUAL', createdAt: iso(10) },
  { id: 'wh-02', code: 'WH-02', name: '二号仓', searchCode: 'EHC', status: 'ENABLED', mgmtMode: 'MANUAL', createdAt: iso(8) },
  { id: 'wh-03', code: 'WH-03', name: '三号仓', searchCode: 'SHC', status: 'DISABLED', mgmtMode: 'MANUAL', createdAt: iso(5) },
]

export const seedLocations: LocationItem[] = [
  { id: 'loc-01', warehouseId: 'wh-01', warehouseCode: 'WH-01', code: 'STG-01', searchCode: 'ZC', type: 'STAGING', status: 'ENABLED', reachability: 'UNIVERSAL', createdAt: iso(9) },
  { id: 'loc-02', warehouseId: 'wh-01', warehouseCode: 'WH-01', code: 'DEF-01', searchCode: 'MR', type: 'DEFAULT', status: 'ENABLED', reachability: 'UNIVERSAL', createdAt: iso(9) },
  { id: 'loc-03', warehouseId: 'wh-01', warehouseCode: 'WH-01', code: 'DEF-02', searchCode: 'MR2', type: 'DEFAULT', status: 'ENABLED', reachability: 'MANUAL_ONLY', createdAt: iso(9) },
  { id: 'loc-04', warehouseId: 'wh-02', warehouseCode: 'WH-02', code: 'STG-01', searchCode: 'ZC', type: 'STAGING', status: 'ENABLED', reachability: 'UNIVERSAL', createdAt: iso(7) },
  { id: 'loc-05', warehouseId: 'wh-02', warehouseCode: 'WH-02', code: 'DEF-01', searchCode: 'MR', type: 'DEFAULT', status: 'ENABLED', reachability: 'UNIVERSAL', createdAt: iso(7) },
  { id: 'loc-06', warehouseId: 'wh-03', warehouseCode: 'WH-03', code: 'STG-01', searchCode: 'ZC', type: 'STAGING', status: 'DISABLED', reachability: 'UNIVERSAL', createdAt: iso(4) },
]

// ── 来源 ──────────────────────────────────────────────
export const seedSources: SourceItem[] = [
  { id: 'src-01', type: 'SUPPLIER', code: 'SUP-001', name: '华东五金', searchCode: 'HDWJ', status: 'ENABLED', createdAt: iso(12) },
  { id: 'src-02', type: 'SUPPLIER', code: 'SUP-002', name: '华南钢材', searchCode: 'HNGC', status: 'ENABLED', createdAt: iso(11) },
  { id: 'src-03', type: 'SUPPLIER', code: 'SUP-003', name: '天成电气', searchCode: 'TCDQ', status: 'ENABLED', createdAt: iso(6) },
  { id: 'src-04', type: 'WORKSHOP', code: 'WS-01', name: '一车间', searchCode: 'YCJ', status: 'ENABLED', createdAt: iso(3) },
  { id: 'src-05', type: 'WORKSHOP', code: 'WS-02', name: '二车间', searchCode: 'ECJ', status: 'DISABLED', createdAt: iso(2) },
]

// ── 批次（系统自动建，只读）────────────────────────────
export const seedBatches: BatchItem[] = [
  {
    id: 'b-01', materialId: 'mat-001', materialCode: 'MAT-001', materialName: '螺母 M6',
    batchNo: '260810001', sourceBatchNo: 'PRD-260809-01', sourceType: 'WORKSHOP', sourceCode: 'WS-01',
    productionDate: '2026-08-09', expiryDate: null, status: 'ACTIVE', createdAt: iso(1),
  },
  {
    id: 'b-02', materialId: 'mat-001', materialCode: 'MAT-001', materialName: '螺母 M6',
    batchNo: '260810002', sourceBatchNo: 'PRD-260808-01', sourceType: 'WORKSHOP', sourceCode: 'WS-01',
    productionDate: '2026-08-08', expiryDate: null, status: 'ACTIVE', createdAt: iso(2),
  },
  {
    id: 'b-03', materialId: 'mat-002', materialCode: 'MAT-002', materialName: '垫片 8mm',
    batchNo: '260809001', sourceBatchNo: 'PO-20260805-01', sourceType: 'SUPPLIER', sourceCode: 'SUP-001',
    productionDate: '2026-08-09', expiryDate: '2027-08-09', status: 'ACTIVE', createdAt: iso(1),
  },
  {
    id: 'b-04', materialId: 'mat-004', materialCode: 'MAT-004', materialName: '钢板 2mm',
    batchNo: '260808001', sourceBatchNo: 'PO-20260803-02', sourceType: 'SUPPLIER', sourceCode: 'SUP-002',
    productionDate: '2026-08-08', expiryDate: null, status: 'ACTIVE', createdAt: iso(2),
  },
]
