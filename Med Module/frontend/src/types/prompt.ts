export interface PromptBuildResponse {
  requestId: string;
  prompt: string;
  complaintsText: string;
  sourceComplaintsText: string;
  preparedComplaintsText: string;
  totalSymptoms: number;
  filledSymptoms: number;
  symptoms: Array<{
    name: string;
    note: string;
  }>;
  warnings: string[];
}

export type LlmProvider = 'gemini' | 'chatgpt' | 'gigachat';

export interface PromptRunResponse {
  promptBuild: PromptBuildResponse;
  provider: string;
  model: string;
  content: string;
  rawResponse: string;
}

export interface PromptResponseReview {
  isCompliant: boolean;
  hasCriticalIssues: boolean;
  score: number;
  extractedSymptoms: string[];
  issues: string[];
  warnings: string[];
}

export interface PromptProviderResult {
  provider: LlmProvider;
  model: string;
  prompt: string;
  content: string;
  rawResponse: string;
  error: string | null;
  review: PromptResponseReview;
}

export interface PromptWorkflowStage {
  provider: string;
  model: string;
  prompt: string;
  content: string;
  rawResponse: string;
  error?: string | null;
}

export interface PromptDisputeResolution {
  candidateSymptoms: string[];
  confirmedSymptoms: string[];
  stage: PromptWorkflowStage;
}

export interface PromptSymptomVote {
  symptom: string;
  votes: number;
  providers: string[];
  reachedMajority: boolean;
  resolvedByGemini: boolean;
  includedInFinalAnswer: boolean;
}

export interface PromptVotingSummary {
  requestedProviders: number;
  successfulProviders: number;
  majorityThreshold: number;
  preparedComplaintsText: string;
  consensusSymptoms: string[];
  disputedSymptoms: string[];
  geminiConfirmedSymptoms: string[];
  finalSymptoms: string[];
  voteDetails: PromptSymptomVote[];
}

export interface KnowledgeBaseMapping {
  source: string;
  symptom: string;
  nodeId: string;
  activationConditionId: string;
  label: string;
  value: string;
}

export interface KnowledgeBaseSolver {
  payload: Record<string, string>;
  mappings: KnowledgeBaseMapping[];
  warnings: string[];
  requestJson: string;
  statusCode: number | null;
  isSuccess: boolean;
  responseJson: string;
  error: string;
}

export type SymptomEvidenceStatus = 'verified' | 'needsReview';

export interface SymptomEvidence {
  name: string;
  evidence: string;
  evidenceStart: number | null;
  evidenceEnd: number | null;
  verificationStatus: SymptomEvidenceStatus;
}

export interface SymptomEvidenceVerification {
  symptoms: SymptomEvidence[];
  stage: PromptWorkflowStage;
}

export interface PromptRunBundle {
  promptBuild: PromptBuildResponse;
  preparation: PromptWorkflowStage;
  results: PromptProviderResult[];
  disputeResolution: PromptDisputeResolution;
  voting: PromptVotingSummary;
  evidenceVerification: SymptomEvidenceVerification;
  solver: KnowledgeBaseSolver;
}
