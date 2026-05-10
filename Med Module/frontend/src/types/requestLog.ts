export interface ProcessingRequestListItem {
  id: string;
  requestId: string;
  status: string;
  internalMode: string;
  usedVoting: boolean;
  createdAt: string;
  finishedAt: string | null;
  errorMessage: string;
}

export interface ProcessingRequestDetails extends ProcessingRequestListItem {
  sourceText: string;
  preparedText: string;
  finalSymptomsJson: string;
  evidenceJson: string;
  manualChangesJson: string;
  solverPayloadJson: string;
  solverResponseJson: string;
}
