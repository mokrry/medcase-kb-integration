export interface AdminKnowledgeBaseStatus {
  fileFound: boolean;
  fileName: string;
  worksheetCount: number;
  keyTables: string[];
  solverAvailable: boolean;
  solverStatus: string;
  lastPayloadJson: string;
  lastSolverResponseJson: string;
}

export interface AdminIntegrationStatus {
  provider: string;
  model: string;
  baseUrl: string;
  configured: boolean;
  keyStatus: string;
  available: boolean | null;
  lastCheckResult: string;
}

export interface AdminSystemDiagnostics {
  backendVersion: string;
  postgreSqlAvailable: boolean;
  apiStatus: string;
  totalRequests: number;
  completedRequests: number;
  failedRequests: number;
  startedRequests: number;
  lastError: string;
}
