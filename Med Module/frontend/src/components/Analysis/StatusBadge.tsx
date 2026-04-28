import { getStatusClassName, getStatusLabel } from '../../utils/statusHelpers';

export function StatusBadge({ status }: { status: string }) {
  return <span className={getStatusClassName(status)}>{getStatusLabel(status)}</span>;
}
