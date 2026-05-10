import { createContext, useContext, useEffect, useMemo, useState, type PropsWithChildren } from 'react';
import type { KnowledgeBaseSolver, PromptRunBundle } from '../types/prompt';
import type { ProcessingRequestDetails, ProcessingRequestListItem } from '../types/requestLog';

type AuthPageMode = 'login' | 'register';

interface AppStateContextValue {
  latestRun: PromptRunBundle | null;
  setLatestRun: (run: PromptRunBundle | null) => void;
  latestDiagnosisSolver: KnowledgeBaseSolver | null;
  setLatestDiagnosisSolver: (solver: KnowledgeBaseSolver | null) => void;
  authPageMode: AuthPageMode;
  setAuthPageMode: (mode: AuthPageMode) => void;
  authPageEmail: string;
  setAuthPageEmail: (email: string) => void;
  complaintsDraft: string;
  setComplaintsDraft: (value: string) => void;
  isExtractionProcessing: boolean;
  setIsExtractionProcessing: (value: boolean) => void;
  excludedSymptoms: string[];
  setExcludedSymptoms: (value: string[]) => void;
  addedSymptoms: string[];
  setAddedSymptoms: (value: string[]) => void;
  requestLogItems: ProcessingRequestListItem[];
  setRequestLogItems: (items: ProcessingRequestListItem[]) => void;
  selectedRequestDetails: ProcessingRequestDetails | null;
  setSelectedRequestDetails: (request: ProcessingRequestDetails | null) => void;
}

const AppStateContext = createContext<AppStateContextValue | undefined>(undefined);
const LATEST_RUN_KEY = 'med-module.latest-prompt-run';
const COMPLAINTS_DRAFT_KEY = 'med-module.complaints-draft';
const EXCLUDED_SYMPTOMS_KEY = 'med-module.excluded-symptoms';
const ADDED_SYMPTOMS_KEY = 'med-module.added-symptoms';
const LATEST_DIAGNOSIS_SOLVER_KEY = 'med-module.latest-diagnosis-solver';
const AUTH_PAGE_MODE_KEY = 'med-module.auth-page-mode';
const AUTH_PAGE_EMAIL_KEY = 'med-module.auth-page-email';
const REQUEST_LOG_ITEMS_KEY = 'med-module.request-log-items';
const SELECTED_REQUEST_DETAILS_KEY = 'med-module.selected-request-details';

function resetAppStateStorage() {
  localStorage.removeItem(LATEST_RUN_KEY);
  localStorage.removeItem(LATEST_DIAGNOSIS_SOLVER_KEY);
  localStorage.removeItem(COMPLAINTS_DRAFT_KEY);
  localStorage.removeItem(EXCLUDED_SYMPTOMS_KEY);
  localStorage.removeItem(ADDED_SYMPTOMS_KEY);
  localStorage.removeItem(AUTH_PAGE_MODE_KEY);
  localStorage.removeItem(AUTH_PAGE_EMAIL_KEY);
  localStorage.removeItem(REQUEST_LOG_ITEMS_KEY);
  localStorage.removeItem(SELECTED_REQUEST_DETAILS_KEY);
}

function isPromptRunBundle(value: unknown): value is PromptRunBundle {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as Partial<PromptRunBundle> & {
    promptBuild?: { requestId?: unknown };
    results?: unknown;
  };

  return (
    !!candidate.promptBuild &&
    typeof candidate.promptBuild.requestId === 'string' &&
    Array.isArray(candidate.results)
  );
}

function isProcessingRequestListItem(value: unknown): value is ProcessingRequestListItem {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as Partial<ProcessingRequestListItem>;
  return (
    typeof candidate.id === 'string' &&
    typeof candidate.requestId === 'string' &&
    typeof candidate.status === 'string' &&
    typeof candidate.createdAt === 'string'
  );
}

function isProcessingRequestDetails(value: unknown): value is ProcessingRequestDetails {
  if (!isProcessingRequestListItem(value)) {
    return false;
  }

  const candidate = value as Partial<ProcessingRequestDetails>;
  return (
    typeof candidate.sourceText === 'string' &&
    typeof candidate.preparedText === 'string' &&
    typeof candidate.finalSymptomsJson === 'string' &&
    typeof candidate.solverResponseJson === 'string'
  );
}

function readJson<T>(key: string, fallback: T): T {
  const raw = localStorage.getItem(key);
  if (!raw) {
    return fallback;
  }

  try {
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
}

function persistJson(key: string, value: unknown) {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch (error) {
    console.warn(`Failed to persist ${key} in localStorage`, error);
  }
}

function normalizeKnowledgeBaseSolver(solver: KnowledgeBaseSolver): KnowledgeBaseSolver {
  return {
    payload: solver.payload ?? {},
    mappings: Array.isArray(solver.mappings) ? solver.mappings : [],
    warnings: Array.isArray(solver.warnings) ? solver.warnings : [],
    requestJson: solver.requestJson ?? '{}',
    statusCode: solver.statusCode ?? null,
    isSuccess: solver.isSuccess ?? false,
    responseJson: solver.responseJson ?? '',
    error: solver.error ?? ''
  };
}

function isKnowledgeBaseSolver(value: unknown): value is KnowledgeBaseSolver {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as Partial<KnowledgeBaseSolver>;
  return (
    typeof candidate.requestJson === 'string' &&
    typeof candidate.responseJson === 'string' &&
    typeof candidate.isSuccess === 'boolean'
  );
}

function normalizePromptRunBundle(run: PromptRunBundle): PromptRunBundle {
  return {
    ...run,
    promptBuild: {
      ...run.promptBuild,
      sourceComplaintsText: run.promptBuild.sourceComplaintsText ?? run.promptBuild.complaintsText ?? '',
      preparedComplaintsText: run.promptBuild.preparedComplaintsText ?? run.promptBuild.complaintsText ?? '',
      warnings: Array.isArray(run.promptBuild.warnings) ? run.promptBuild.warnings : [],
      symptoms: Array.isArray(run.promptBuild.symptoms) ? run.promptBuild.symptoms : []
    },
    preparation: run.preparation ?? {
      provider: '',
      model: '',
      prompt: '',
      content: run.promptBuild.preparedComplaintsText ?? run.promptBuild.complaintsText ?? '',
      rawResponse: '',
      error: null
    },
    results: Array.isArray(run.results)
      ? run.results.map((result) => ({
          ...result,
          prompt: result.prompt ?? '',
          content: result.content ?? '',
          rawResponse: result.rawResponse ?? '',
          review: {
            ...result.review,
            extractedSymptoms: Array.isArray(result.review?.extractedSymptoms) ? result.review.extractedSymptoms : [],
            issues: Array.isArray(result.review?.issues) ? result.review.issues : [],
            warnings: Array.isArray(result.review?.warnings) ? result.review.warnings : []
          }
        }))
      : [],
    disputeResolution: run.disputeResolution ?? {
      candidateSymptoms: [],
      confirmedSymptoms: [],
      stage: {
        provider: '',
        model: '',
        prompt: '',
        content: '',
        rawResponse: '',
        error: null
      }
    },
    voting: {
      ...run.voting,
      preparedComplaintsText: run.voting.preparedComplaintsText ?? run.promptBuild.preparedComplaintsText ?? run.promptBuild.complaintsText ?? '',
      consensusSymptoms: Array.isArray(run.voting.consensusSymptoms) ? run.voting.consensusSymptoms : [],
      disputedSymptoms: Array.isArray(run.voting.disputedSymptoms) ? run.voting.disputedSymptoms : [],
      geminiConfirmedSymptoms: Array.isArray(run.voting.geminiConfirmedSymptoms) ? run.voting.geminiConfirmedSymptoms : [],
      finalSymptoms: Array.isArray(run.voting.finalSymptoms) ? run.voting.finalSymptoms : [],
      voteDetails: Array.isArray(run.voting.voteDetails)
        ? run.voting.voteDetails.map((item) => ({
            ...item,
            providers: Array.isArray(item.providers) ? item.providers : [],
            resolvedByGemini: item.resolvedByGemini ?? false,
            includedInFinalAnswer: item.includedInFinalAnswer ?? false
          }))
        : []
    },
    evidenceVerification: {
      symptoms: Array.isArray(run.evidenceVerification?.symptoms)
        ? run.evidenceVerification.symptoms.map((item) => ({
            name: item.name ?? '',
            evidence: item.evidence ?? '',
            evidenceStart: item.evidenceStart ?? null,
            evidenceEnd: item.evidenceEnd ?? null,
            verificationStatus: item.verificationStatus === 'verified' ? 'verified' : 'needsReview'
          }))
        : [],
      stage: run.evidenceVerification?.stage ?? {
        provider: '',
        model: '',
        prompt: '',
        content: '',
        rawResponse: '',
        error: null
      }
    },
    solver: {
      payload: run.solver?.payload ?? {},
      mappings: Array.isArray(run.solver?.mappings) ? run.solver.mappings : [],
      warnings: Array.isArray(run.solver?.warnings) ? run.solver.warnings : [],
      requestJson: run.solver?.requestJson ?? '{}',
      statusCode: run.solver?.statusCode ?? null,
      isSuccess: run.solver?.isSuccess ?? false,
      responseJson: run.solver?.responseJson ?? '',
      error: run.solver?.error ?? ''
    }
  };
}

export function AppStateProvider({ children }: PropsWithChildren) {
  const [latestRun, setLatestRunState] = useState<PromptRunBundle | null>(null);
  const [latestDiagnosisSolver, setLatestDiagnosisSolverState] = useState<KnowledgeBaseSolver | null>(null);
  const [authPageMode, setAuthPageModeState] = useState<AuthPageMode>('login');
  const [authPageEmail, setAuthPageEmailState] = useState('');
  const [complaintsDraft, setComplaintsDraftState] = useState('');
  const [isExtractionProcessing, setIsExtractionProcessingState] = useState(false);
  const [excludedSymptoms, setExcludedSymptomsState] = useState<string[]>([]);
  const [addedSymptoms, setAddedSymptomsState] = useState<string[]>([]);
  const [requestLogItems, setRequestLogItemsState] = useState<ProcessingRequestListItem[]>([]);
  const [selectedRequestDetails, setSelectedRequestDetailsState] = useState<ProcessingRequestDetails | null>(null);

  useEffect(() => {
    function handleResetState() {
      setLatestRunState(null);
      setLatestDiagnosisSolverState(null);
      setAuthPageModeState('login');
      setAuthPageEmailState('');
      setComplaintsDraftState('');
      setIsExtractionProcessingState(false);
      setExcludedSymptomsState([]);
      setAddedSymptomsState([]);
      setRequestLogItemsState([]);
      setSelectedRequestDetailsState(null);
      resetAppStateStorage();
    }

    window.addEventListener('med-module:reset-state', handleResetState);
    return () => window.removeEventListener('med-module:reset-state', handleResetState);
  }, []);

  useEffect(() => {
    const persistedValue = readJson<unknown>(LATEST_RUN_KEY, null);
    const persistedDiagnosisSolver = readJson<unknown>(LATEST_DIAGNOSIS_SOLVER_KEY, null);
    const persistedAuthPageMode = localStorage.getItem(AUTH_PAGE_MODE_KEY);
    const persistedAuthPageEmail = localStorage.getItem(AUTH_PAGE_EMAIL_KEY) ?? '';
    const persistedDraft = localStorage.getItem(COMPLAINTS_DRAFT_KEY) ?? '';
    const persistedExcludedSymptoms = readJson<string[]>(EXCLUDED_SYMPTOMS_KEY, []);
    const persistedAddedSymptoms = readJson<string[]>(ADDED_SYMPTOMS_KEY, []);
    const persistedRequestLogItems = readJson<unknown>(REQUEST_LOG_ITEMS_KEY, []);
    const persistedSelectedRequestDetails = readJson<unknown>(SELECTED_REQUEST_DETAILS_KEY, null);

    setAuthPageModeState(persistedAuthPageMode === 'register' ? 'register' : 'login');
    setAuthPageEmailState(persistedAuthPageEmail);
    setComplaintsDraftState(persistedDraft);
    setExcludedSymptomsState(persistedExcludedSymptoms);
    setAddedSymptomsState(persistedAddedSymptoms);
    setLatestDiagnosisSolverState(
      isKnowledgeBaseSolver(persistedDiagnosisSolver) ? normalizeKnowledgeBaseSolver(persistedDiagnosisSolver) : null
    );
    setRequestLogItemsState(
      Array.isArray(persistedRequestLogItems)
        ? persistedRequestLogItems.filter(isProcessingRequestListItem)
        : []
    );
    setSelectedRequestDetailsState(
      isProcessingRequestDetails(persistedSelectedRequestDetails) ? persistedSelectedRequestDetails : null
    );

    if (isPromptRunBundle(persistedValue)) {
      setLatestRunState(normalizePromptRunBundle(persistedValue));
      if (!persistedDraft) {
        setComplaintsDraftState(
          persistedValue.promptBuild.sourceComplaintsText ?? persistedValue.promptBuild.complaintsText ?? ''
        );
      }
      return;
    }

    localStorage.removeItem(LATEST_RUN_KEY);
    setLatestRunState(null);
  }, []);

  const value = useMemo<AppStateContextValue>(
    () => ({
      latestRun,
      latestDiagnosisSolver,
      authPageMode,
      authPageEmail,
      complaintsDraft,
      isExtractionProcessing,
      excludedSymptoms,
      addedSymptoms,
      requestLogItems,
      selectedRequestDetails,
      setLatestRun(run) {
        setLatestRunState(run ? normalizePromptRunBundle(run) : null);
        if (run) {
          const normalizedRun = normalizePromptRunBundle(run);
          persistJson(LATEST_RUN_KEY, normalizedRun);
          const sourceText = normalizedRun.promptBuild.sourceComplaintsText || normalizedRun.promptBuild.complaintsText;
          if (sourceText) {
            setComplaintsDraftState(sourceText);
            localStorage.setItem(COMPLAINTS_DRAFT_KEY, sourceText);
          }
        } else {
          localStorage.removeItem(LATEST_RUN_KEY);
        }
      },
      setLatestDiagnosisSolver(solver) {
        setLatestDiagnosisSolverState(solver ? normalizeKnowledgeBaseSolver(solver) : null);
        if (solver) {
          persistJson(LATEST_DIAGNOSIS_SOLVER_KEY, normalizeKnowledgeBaseSolver(solver));
        } else {
          localStorage.removeItem(LATEST_DIAGNOSIS_SOLVER_KEY);
        }
      },
      setAuthPageMode(mode) {
        setAuthPageModeState(mode);
        localStorage.setItem(AUTH_PAGE_MODE_KEY, mode);
      },
      setAuthPageEmail(email) {
        setAuthPageEmailState(email);
        localStorage.setItem(AUTH_PAGE_EMAIL_KEY, email);
      },
      setComplaintsDraft(value) {
        setComplaintsDraftState(value);
        localStorage.setItem(COMPLAINTS_DRAFT_KEY, value);
      },
      setIsExtractionProcessing(value) {
        setIsExtractionProcessingState(value);
      },
      setExcludedSymptoms(value) {
        setExcludedSymptomsState(value);
        persistJson(EXCLUDED_SYMPTOMS_KEY, value);
      },
      setAddedSymptoms(value) {
        setAddedSymptomsState(value);
        persistJson(ADDED_SYMPTOMS_KEY, value);
      },
      setRequestLogItems(items) {
        setRequestLogItemsState(items);
        persistJson(REQUEST_LOG_ITEMS_KEY, items);
      },
      setSelectedRequestDetails(request) {
        setSelectedRequestDetailsState(request);
        if (request) {
          persistJson(SELECTED_REQUEST_DETAILS_KEY, request);
        } else {
          localStorage.removeItem(SELECTED_REQUEST_DETAILS_KEY);
        }
      }
    }),
    [
      latestRun,
      latestDiagnosisSolver,
      authPageMode,
      authPageEmail,
      complaintsDraft,
      isExtractionProcessing,
      excludedSymptoms,
      addedSymptoms,
      requestLogItems,
      selectedRequestDetails
    ]
  );

  return <AppStateContext.Provider value={value}>{children}</AppStateContext.Provider>;
}

export function useAppState() {
  const context = useContext(AppStateContext);
  if (!context) {
    throw new Error('useAppState must be used inside AppStateProvider');
  }
  return context;
}
