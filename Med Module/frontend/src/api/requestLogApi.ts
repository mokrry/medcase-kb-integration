import { apiGet } from './client';
import type { ProcessingRequestDetails, ProcessingRequestListItem } from '../types/requestLog';

export interface RequestLogFilters {
  status?: string;
  dateFrom?: string;
  dateTo?: string;
}

export function getRequests(filters: RequestLogFilters = {}): Promise<ProcessingRequestListItem[]> {
  const params = new URLSearchParams();

  if (filters.status) {
    params.set('status', filters.status);
  }

  if (filters.dateFrom) {
    params.set('dateFrom', new Date(`${filters.dateFrom}T00:00:00`).toISOString());
  }

  if (filters.dateTo) {
    params.set('dateTo', new Date(`${filters.dateTo}T23:59:59.999`).toISOString());
  }

  const query = params.toString();
  return apiGet<ProcessingRequestListItem[]>(query ? `/requests?${query}` : '/requests');
}

export function getRequestDetails(id: string): Promise<ProcessingRequestDetails> {
  return apiGet<ProcessingRequestDetails>(`/requests/${id}`);
}
