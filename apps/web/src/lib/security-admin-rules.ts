export function applyVisiblePermissions(current: ReadonlySet<string>, visible: readonly string[], enable: boolean) {
  const next = new Set(current);
  for (const code of visible) {
    if (enable) next.add(code);
    else next.delete(code);
  }
  return next;
}

export function permissionDelta(original: readonly string[], draft: ReadonlySet<string>) {
  const source = new Set(original);
  return {
    added: [...draft].filter(code => !source.has(code)).length,
    removed: original.filter(code => !draft.has(code)).length,
  };
}

export function resolveGaiaTheme(value?: string) { return value === "classic" ? "classic" : "renewed"; }
