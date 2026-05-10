export type ExtractionEntityStatus = 'Found' | 'NotFound' | 'PartiallyFound' | 'NeedsReview';

export type ProcessingState = 'idle' | 'processing' | 'success' | 'partial' | 'error' | 'timeout';

export type ModelConnectionStatus = 'available' | 'degraded' | 'unavailable';

export interface ModelOption {
  id: string;
  name: string;
  provider: string;
  description: string;
  connectionStatus: ModelConnectionStatus;
  source: 'mock';
}

export interface ExtractionEntityResult {
  entity: string;
  status: ExtractionEntityStatus;
  valueCount: number;
  values: string[];
  comment: string;
  evidence: string[];
}

export interface ReliabilityEntityResult {
  entity: string;
  status: 'Confirmed' | 'Unconfirmed' | 'NeedsReview';
  rationale: string;
}

export interface KnowledgeBaseMatch {
  entity: string;
  matched: boolean;
  matchedValues: string[];
  missingValues: string[];
  comment: string;
}

export interface ExtractionSummary {
  totalRequested: number;
  foundCount: number;
  notFoundCount: number;
  partialCount: number;
  needsReviewCount: number;
}

export interface ExtractionRun {
  requestId: string;
  createdAt: string;
  sourceFileName: string;
  xmlText: string;
  plainText: string;
  modelId: string;
  modelName: string;
  entities: string[];
  results: ExtractionEntityResult[];
  summary: ExtractionSummary;
  reliability: ReliabilityEntityResult[];
  knowledgeBase: KnowledgeBaseMatch[];
  warnings: string[];
  errors: string[];
  processingState: ProcessingState;
  progress: number;
}

export interface ExtractionLogEntry {
  requestId: string;
  createdAt: string;
  modelName: string;
  status: ProcessingState;
  sourceFileName: string;
  summary: ExtractionSummary;
  warnings: string[];
  errors: string[];
  reliabilityReviewCount: number;
}

export type UserActionType =
  | 'file_uploaded'
  | 'file_removed'
  | 'entities_updated'
  | 'model_selected'
  | 'form_reset'
  | 'processing_started'
  | 'processing_completed'
  | 'processing_failed';

export interface UserActionLogEntry {
  id: string;
  createdAt: string;
  actionType: UserActionType;
  label: string;
  fileName?: string;
  fileLoaded: boolean;
  sourceFormat?: 'xml' | 'xlsx';
  modelName?: string;
  requestId?: string;
  details?: string;
}

export interface ExtractionInput {
  fileName: string;
  xmlText: string;
  entities: string[];
  modelId: string;
}
