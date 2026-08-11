/**
 * 枚举 → i18n key 映射（数据枚举不进语言包，走 labels.ts；未知值透传原码，契约扩展不炸）。
 */
export const ENUM_LABEL_KEYS: Record<string, Record<string, string>> = {
  labelType: {
    NONE: 'enums.labelType.none',
    SKU: 'enums.labelType.sku',
    UNIQUE: 'enums.labelType.unique',
  },
  status: {
    ENABLED: 'enums.status.enabled',
    DISABLED: 'enums.status.disabled',
  },
  mgmtMode: {
    MANUAL: 'enums.mgmtMode.manual',
    AGV: 'enums.mgmtMode.agv',
  },
  locationType: {
    STAGING: 'enums.locationType.staging',
    DEFAULT: 'enums.locationType.default',
  },
  reachability: {
    MANUAL_ONLY: 'enums.reachability.manualOnly',
    AGV: 'enums.reachability.agv',
    UNIVERSAL: 'enums.reachability.universal',
  },
  sourceType: {
    SUPPLIER: 'enums.sourceType.supplier',
    WORKSHOP: 'enums.sourceType.workshop',
  },
  batchStatus: {
    ACTIVE: 'enums.batchStatus.active',
    CLOSED: 'enums.batchStatus.closed',
  },
  importTaskStatus: {
    PRECHECKING: 'enums.importTaskStatus.prechecking',
    PRECHECKED: 'enums.importTaskStatus.prechecked',
    EXECUTING: 'enums.importTaskStatus.executing',
    DONE: 'enums.importTaskStatus.done',
    FAILED: 'enums.importTaskStatus.failed',
  },
  importDirection: {
    IMPORT: 'enums.importDirection.import',
    EXPORT: 'enums.importDirection.export',
  },
  uom: {
    CT: 'enums.uom.CT',
    PC: 'enums.uom.PC',
    BOX: 'enums.uom.BOX',
    KG: 'enums.uom.KG',
    G: 'enums.uom.G',
    L: 'enums.uom.L',
    M: 'enums.uom.M',
  },
}

/** 取枚举展示文案的 i18n key（未知值返回 null，调用方透传原码） */
export function enumLabelKey(enumName: string, value: string | null | undefined): string | null {
  if (value === null || value === undefined) return null
  return ENUM_LABEL_KEYS[enumName]?.[value] ?? null
}

/** 状态徽章语义色（视觉规范：成功 emerald / 待检 amber / 危险 red / 中性 slate） */
export function statusVariant(value: string | null | undefined, activeValues: string[] = ['ENABLED', 'ACTIVE']): string {
  if (value && activeValues.includes(value)) return 'success'
  if (value === 'DISABLED' || value === 'CLOSED') return 'neutral'
  return 'neutral'
}
