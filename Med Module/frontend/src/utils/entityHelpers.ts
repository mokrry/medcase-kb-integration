export function normalizeEntityList(value: string): string[] {
  const unique = new Map<string, string>();

  value
    .split(/[\n,]+/)
    .map((part) => part.trim())
    .filter(Boolean)
    .forEach((entity) => {
      const normalizedKey = entity.toLocaleLowerCase('ru-RU');
      if (!unique.has(normalizedKey)) {
        unique.set(normalizedKey, entity);
      }
    });

  return Array.from(unique.values());
}

export function formatDateTime(value: string): string {
  const date = new Date(value);
  return new Intl.DateTimeFormat('ru-RU', {
    dateStyle: 'short',
    timeStyle: 'short'
  }).format(date);
}

export function downloadTextFile(fileName: string, content: string) {
  const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}
