export function getStatusLabel(status: string): string {
  switch (status) {
    case 'Found':
      return 'Найдено';
    case 'NotFound':
      return 'Не найдено';
    case 'PartiallyFound':
      return 'Найдено частично';
    case 'NeedsReview':
      return 'Требуется ручная проверка';
    case 'Confirmed':
      return 'Подтверждено';
    case 'Unconfirmed':
      return 'Не подтверждено';
    default:
      return status;
  }
}

export function getStatusClassName(status: string): string {
  switch (status) {
    case 'Found':
      return 'status-badge status-badge--found';
    case 'NotFound':
      return 'status-badge status-badge--not-found';
    case 'PartiallyFound':
      return 'status-badge status-badge--partial';
    case 'NeedsReview':
      return 'status-badge status-badge--review';
    case 'Confirmed':
      return 'status-badge status-badge--found';
    case 'Unconfirmed':
      return 'status-badge status-badge--not-found';
    default:
      return 'status-badge';
  }
}
