/**
 * 菜单图标映射 —— iconKey（模块注册表）→ lucide 图标；未知 iconKey 回退 Circle（契约扩展不炸）。
 */
import {
  Box, Circle, Home, Layers, LayoutDashboard, Package, Printer, ScanLine, Shield, Truck, Users, Warehouse,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'

const ICONS: Record<string, LucideIcon> = {
  home: Home,
  dashboard: LayoutDashboard,
  package: Package,
  warehouse: Warehouse,
  truck: Truck,
  layers: Layers,
  users: Users,
  shield: Shield,
  printer: Printer,
  box: Box,
  scan: ScanLine,
}

export function menuIcon(iconKey: string): LucideIcon {
  return ICONS[iconKey] ?? Circle
}
