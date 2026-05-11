import { apiGet } from './client';
import type {
  AdminIntegrationStatus,
  AdminKnowledgeBaseStatus,
  AdminSystemDiagnostics
} from '../types/admin';

export function getKnowledgeBaseStatus(): Promise<AdminKnowledgeBaseStatus> {
  return apiGet<AdminKnowledgeBaseStatus>('/admin/knowledge-base');
}

export function getIntegrationStatuses(check = false): Promise<AdminIntegrationStatus[]> {
  return apiGet<AdminIntegrationStatus[]>(`/admin/integrations${check ? '?check=true' : ''}`);
}

export function getSystemDiagnostics(): Promise<AdminSystemDiagnostics> {
  return apiGet<AdminSystemDiagnostics>('/admin/diagnostics');
}
