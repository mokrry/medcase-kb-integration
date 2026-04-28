export function compactText(value: string): string {
  return value.replace(/\s+/g, ' ').trim();
}

export function buildPreview(value: string, maxLength = 140): string {
  const compact = compactText(value);
  return compact.length <= maxLength ? compact : `${compact.slice(0, maxLength)}...`;
}
