import type { FeatureCategory } from './feature';

export type AnalysisStatus = 'Found' | 'NotFound' | 'NeedsReview';

export interface AnalysisResult {
  featureName: string;
  category: FeatureCategory | string;
  status: AnalysisStatus | string;
  evidence: string;
}

export interface AnalysisRequest {
  patientId: number;
  includeComplaintsFeatures: boolean;
  includeAnamnesisFeatures: boolean;
}

export interface AnalysisResponse {
  patientId: number;
  fullText: string;
  includeComplaintsFeatures: boolean;
  includeAnamnesisFeatures: boolean;
  totalFeatures: number;
  foundCount: number;
  notFoundCount: number;
  needsReviewCount: number;
  results: AnalysisResult[];
}
