import { apiGet, apiPost } from './client';
import type { PatientListItem, PatientDetails } from '../types/patient';
import type { Feature } from '../types/feature';
import type { AnalysisRequest, AnalysisResponse } from '../types/analysis';

export function getPatients(page = 1, pageSize = 20): Promise<PatientListItem[]> {
  return apiGet<PatientListItem[]>(`/patients?page=${page}&pageSize=${pageSize}`);
}

export function getPatientDetails(id: number): Promise<PatientDetails> {
  return apiGet<PatientDetails>(`/patients/${id}`);
}

export function getFeatures(
  includeComplaintsFeatures = true,
  includeAnamnesisFeatures = true
): Promise<Feature[]> {
  return apiGet<Feature[]>(
    `/patients/features?includeComplaintsFeatures=${includeComplaintsFeatures}&includeAnamnesisFeatures=${includeAnamnesisFeatures}`
  );
}

export function analyzePatient(request: AnalysisRequest): Promise<AnalysisResponse> {
  return apiPost<AnalysisRequest, AnalysisResponse>('/patients/analyze', request);
}
