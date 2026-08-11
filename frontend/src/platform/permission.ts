/**
 * 权限工具 —— 三级权限点（route/menu/action）判断（框架设计 v0.2 / 评审 B-32）。
 */

/** 是否持有权限点（permissions 来自登录/me 响应，已含角色合并结果） */
export function hasPermission(permissions: string[] | undefined | null, code: string): boolean {
  if (!permissions) return false
  return permissions.includes(code)
}

/** 是否持有全部指定权限点 */
export function hasAllPermissions(permissions: string[] | undefined | null, codes: string[]): boolean {
  return codes.every((c) => hasPermission(permissions, c))
}

/** 是否持有任一指定权限点 */
export function hasAnyPermission(permissions: string[] | undefined | null, codes: string[]): boolean {
  return codes.some((c) => hasPermission(permissions, c))
}

/** 路由权限码：route.<moduleCode>（框架设计：路由权限=能否进入模块） */
export function routePermission(moduleCode: string): string {
  return `route.${moduleCode}`
}
