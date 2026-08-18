export function hasPermission(permissions: Iterable<string>, expression: string) {
  const permissionSet = new Set([...permissions].map(value => value.trim().toUpperCase()));
  return expression.split("|").some(value => permissionSet.has(value.trim().toUpperCase()));
}
