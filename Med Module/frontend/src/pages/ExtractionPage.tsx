import { useEffect, useState } from 'react';
import { EmptyState } from '../components/Common/EmptyState';
import { useAppState } from '../context/AppStateContext';
import { runPrompt, solveSymptoms } from '../api/promptApi';
import type {
  KnowledgeBaseSolver,
  LlmProvider,
  PromptRunBundle,
  SymptomEvidence
} from '../types/prompt';
import { logToClientConsole } from '../utils/devConsoleLogger';

const PROVIDERS: LlmProvider[] = ['gemini', 'chatgpt'];

export function ExtractionPage() {
  const {
    latestRun,
    setLatestRun,
    latestDiagnosisSolver,
    setLatestDiagnosisSolver,
    complaintsDraft,
    setComplaintsDraft,
    isExtractionProcessing,
    setIsExtractionProcessing,
    excludedSymptoms,
    setExcludedSymptoms,
    addedSymptoms,
    setAddedSymptoms
  } = useAppState();
  const [complaintsText, setComplaintsText] = useState(
    complaintsDraft || latestRun?.promptBuild?.sourceComplaintsText || latestRun?.promptBuild?.complaintsText || ''
  );
  const [errors, setErrors] = useState<string[]>([]);
  const [activeRun, setActiveRun] = useState<PromptRunBundle | null>(latestRun);
  const [hoveredEvidence, setHoveredEvidence] = useState<SymptomEvidence | null>(null);
  const [showSymptomSearch, setShowSymptomSearch] = useState(false);
  const [symptomSearch, setSymptomSearch] = useState('');
  const [diagnosisLoading, setDiagnosisLoading] = useState(false);
  const [editingSubmittedText, setEditingSubmittedText] = useState(false);
  const processing = isExtractionProcessing;

  useEffect(() => {
    if (complaintsText) {
      return;
    }

    const restoredText =
      complaintsDraft ||
      latestRun?.promptBuild?.sourceComplaintsText ||
      latestRun?.promptBuild?.complaintsText ||
      '';

    if (restoredText) {
      setComplaintsText(restoredText);
    }
  }, [complaintsDraft, complaintsText, latestRun]);

  function resetForm() {
    setComplaintsText('');
    setComplaintsDraft('');
    setErrors([]);
    setActiveRun(null);
    setLatestRun(null);
    setExcludedSymptoms([]);
    setAddedSymptoms([]);
    setShowSymptomSearch(false);
    setSymptomSearch('');
    setLatestDiagnosisSolver(null);
    setEditingSubmittedText(false);
  }

  function handleComplaintsChange(value: string) {
    if (activeRun || latestRun) {
      const shouldReset = window.confirm(
        'Если изменить медицинский текст, найденные симптомы и диагноз будут сброшены. Продолжить?'
      );

      if (!shouldReset) {
        return;
      }

      setActiveRun(null);
      setLatestRun(null);
      setExcludedSymptoms([]);
      setAddedSymptoms([]);
      setShowSymptomSearch(false);
      setSymptomSearch('');
      setLatestDiagnosisSolver(null);
      setHoveredEvidence(null);
    }

    setComplaintsText(value);
    setComplaintsDraft(value);
    setEditingSubmittedText(true);
  }

  async function handleSubmit() {
    const normalizedComplaints = complaintsText.trim();
    if (!normalizedComplaints) {
      setErrors(['Введите медицинский текст']);
      return;
    }

    setIsExtractionProcessing(true);
    setErrors([]);

    void logToClientConsole({
      level: 'info',
      scope: 'Extraction',
      message: 'Extraction request received',
      data: {
        complaintsLength: normalizedComplaints.length,
        providers: PROVIDERS
      }
    });

    try {
      const bundle = await runPrompt(normalizedComplaints, PROVIDERS);

      if (bundle.results.every((item) => item.error)) {
        const failedMessages = bundle.results
          .map((item) => item.error)
          .filter((item): item is string => Boolean(item));
        setErrors(failedMessages.length > 0 ? failedMessages : ['Обработка завершилась ошибкой']);
        return;
      }

      setActiveRun(bundle);
      setLatestRun(bundle);
      setExcludedSymptoms([]);
      setAddedSymptoms([]);
      setShowSymptomSearch(false);
      setSymptomSearch('');
      setLatestDiagnosisSolver(null);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Не удалось обработать текст';
      setErrors([message]);
    } finally {
      setIsExtractionProcessing(false);
    }
  }

  async function handleSolveDiagnosis() {
    const runSnapshot = activeRun ?? latestRun;
    if (!runSnapshot) {
      return;
    }

    const selectedSymptoms = getSelectedSymptoms(runSnapshot, excludedSymptoms, addedSymptoms);
    if (selectedSymptoms.length === 0) {
      setErrors(['Оставьте хотя бы один симптом для постановки диагноза']);
      return;
    }

    setDiagnosisLoading(true);
    setErrors([]);

    try {
      const solver = await solveSymptoms(
        runSnapshot.voting?.preparedComplaintsText || runSnapshot.preparation?.content || submittedText,
        selectedSymptoms
      );

      if (!solver.isSuccess) {
        setLatestDiagnosisSolver(null);
        setErrors([formatSolverError(solver.error)]);
        return;
      }

      setLatestDiagnosisSolver(solver);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Не удалось получить диагноз';
      setErrors([message]);
    } finally {
      setDiagnosisLoading(false);
    }
  }

  function excludeSymptom(symptom: string) {
    setExcludedSymptoms(Array.from(new Set([...excludedSymptoms, symptom])));
    setAddedSymptoms(addedSymptoms.filter((item) => item !== symptom));
    setHoveredEvidence(null);
    setLatestDiagnosisSolver(null);
  }

  function addSymptom(symptom: string) {
    setAddedSymptoms(addedSymptoms.includes(symptom) ? addedSymptoms : [...addedSymptoms, symptom]);
    setExcludedSymptoms(excludedSymptoms.filter((item) => item !== symptom));
    setSymptomSearch('');
    setShowSymptomSearch(false);
    setLatestDiagnosisSolver(null);
  }

  const run = activeRun ?? latestRun;
  const submittedText = run?.promptBuild?.sourceComplaintsText || run?.promptBuild?.complaintsText || '';
  const evidenceItems = run?.evidenceVerification?.symptoms ?? [];
  const fallbackSymptoms = run?.voting?.finalSymptoms ?? [];
  const whitelistSymptoms = run?.promptBuild?.symptoms?.map((item) => item.name).filter(Boolean) ?? [];
  const excludedSymptomsSet = new Set(excludedSymptoms);
  const visibleEvidenceItems = evidenceItems.filter((item) => !excludedSymptomsSet.has(item.name));
  const visibleFallbackSymptoms = fallbackSymptoms.filter((item) => !excludedSymptomsSet.has(item));
  const selectedSymptoms = run ? getSelectedSymptoms(run, excludedSymptomsSet, addedSymptoms) : [];
  const selectedSymptomsCount = selectedSymptoms.length;
  const symptomSuggestions = getSymptomSuggestions(whitelistSymptoms, selectedSymptoms, symptomSearch);
  const solverDiagnoses = latestDiagnosisSolver ? parseSolverDiagnoses(latestDiagnosisSolver.responseJson) : [];
  const primaryDiagnosis = solverDiagnoses[0];

  return (
    <section className="grid gap-24">
      <div className="page-heading">
        <h2>Извлечение</h2>
        <p>Введите медицинский текст, чтобы получить извлечённые симптомы и предполагаемый диагноз.</p>
      </div>

      <div className="workspace-grid">
        <article className="panel panel--form">
          <div className="panel__header">
            <h3>Входные данные</h3>
          </div>

          <label className="form-field">
            <span className="form-field__label">Медицинский текст</span>
            {run && (!editingSubmittedText || hoveredEvidence) ? (
              <div
                className="textarea textarea--highlighted-source"
                role="textbox"
                tabIndex={0}
                onMouseEnter={() => setEditingSubmittedText(true)}
                onFocus={() => setEditingSubmittedText(true)}
              >
                {renderHighlightedText(submittedText, hoveredEvidence)}
              </div>
            ) : (
              <textarea
                className="textarea"
                rows={12}
                value={complaintsText}
                disabled={isExtractionProcessing}
                onChange={(event) => handleComplaintsChange(event.target.value)}
              onBlur={() => {
                if (run) {
                  setEditingSubmittedText(false);
                }
              }}
              placeholder="Например: лихорадка до 38.5, кашель с мокротой, одышка при нагрузке..."
            />
            )}
            <span className="form-field__hint">Вставьте жалобы или фрагмент истории болезни.</span>
          </label>

          <div className="button-row">
            <button className="button button--primary" type="button" disabled={isExtractionProcessing} onClick={handleSubmit}>
              {processing ? 'Обрабатываю...' : 'Получить результат'}
            </button>
            <button className="button button--secondary" type="button" onClick={resetForm}>
              Сбросить
            </button>
          </div>
        </article>

        <article className="panel">
          <div className="panel__header">
            <h3>Результат</h3>
          </div>

          {errors.length > 0 ? (
            <div className="notice notice--error">
              {errors.map((message) => (
                <p key={message}>{message}</p>
              ))}
            </div>
          ) : null}

          {run ? (
            <div className="grid gap-16">
              <article className="panel panel--embedded panel--deprecated-text-copy">
                <h3>Отправленный текст</h3>
              </article>

              <article className="panel panel--embedded panel--symptoms-result">
                <h3>Симптомы</h3>
                <div className="grid gap-16">
                  <div>
                    <h4>Текст с подсветкой подтверждений</h4>
                  </div>

                  <div className="evidence-list">
                    {evidenceItems.length > 0 ? (
                      visibleEvidenceItems.map((item) => (
                        <div className="symptom-review-row" key={item.name}>
                          <button
                            className="symptom-remove-button"
                            type="button"
                            aria-label={`Исключить симптом ${item.name}`}
                            title="Исключить симптом"
                            onClick={() => excludeSymptom(item.name)}
                          >
                            ×
                          </button>
                          <button
                            className="evidence-item"
                            type="button"
                            onMouseEnter={() => setHoveredEvidence(item)}
                            onMouseLeave={() => setHoveredEvidence(null)}
                            onFocus={() => setHoveredEvidence(item)}
                            onBlur={() => setHoveredEvidence(null)}
                          >
                            <span>{item.name}</span>
                            <strong className={`evidence-badge evidence-badge--${item.verificationStatus}`}>
                              {item.verificationStatus === 'verified' ? 'Проверено' : 'Требует ручной проверки'}
                            </strong>
                            {item.evidence ? <small>{item.evidence}</small> : <small>Цитата не найдена.</small>}
                          </button>
                        </div>
                      ))
                    ) : (
                      <pre className="preformatted">{formatSymptoms(visibleFallbackSymptoms)}</pre>
                    )}
                    {addedSymptoms.map((symptom) => (
                      <div className="symptom-review-row" key={`manual-${symptom}`}>
                        <button
                          className="symptom-remove-button"
                          type="button"
                          aria-label={`Исключить симптом ${symptom}`}
                          title="Исключить симптом"
                          onClick={() => excludeSymptom(symptom)}
                        >
                          ×
                        </button>
                        <button className="evidence-item" type="button">
                          <span>{symptom}</span>
                          <strong className="evidence-badge evidence-badge--manual">
                            Добавлено вручную
                          </strong>
                          <small>Симптом добавлен пользователем из whitelist.</small>
                        </button>
                      </div>
                    ))}
                  </div>
                  <div className="manual-symptom-block">
                    <button
                      className="symptom-add-button"
                      type="button"
                      aria-label="Добавить симптом"
                      title="Добавить симптом"
                      onClick={() => setShowSymptomSearch((value) => !value)}
                    >
                      +
                    </button>
                    {showSymptomSearch ? (
                      <div className="symptom-search">
                        <input
                          className="input"
                          value={symptomSearch}
                          onChange={(event) => setSymptomSearch(event.target.value)}
                          placeholder="Начните вводить название симптома..."
                          autoFocus
                        />
                        <div className="symptom-suggestions">
                          {symptomSuggestions.length > 0 ? (
                            symptomSuggestions.map((symptom) => (
                              <button
                                className="symptom-suggestion"
                                key={symptom}
                                type="button"
                                onClick={() => addSymptom(symptom)}
                              >
                                {symptom}
                              </button>
                            ))
                          ) : (
                            <span className="muted">Ничего не найдено.</span>
                          )}
                        </div>
                      </div>
                    ) : null}
                  </div>
                  <div className="button-row">
                    <button
                      className="button button--primary"
                      type="button"
                      disabled={diagnosisLoading || selectedSymptomsCount === 0}
                      onClick={handleSolveDiagnosis}
                    >
                      {diagnosisLoading ? 'Получаю диагноз...' : 'Узнать диагноз'}
                    </button>
                  </div>
                </div>
              </article>

              {latestDiagnosisSolver ? (
                <article className="panel panel--embedded">
                  <h3>Диагноз</h3>
                  {primaryDiagnosis ? (
                    <div className="grid gap-16">
                      <div>
                        <h4>Наиболее вероятный диагноз</h4>
                        <pre className="preformatted">{formatDiagnosis(primaryDiagnosis)}</pre>
                      </div>
                      {solverDiagnoses.length > 1 ? (
                        <div>
                          <h4>Все возможные диагнозы</h4>
                          <pre className="preformatted">{solverDiagnoses.map(formatDiagnosis).join('\n\n')}</pre>
                        </div>
                      ) : null}
                    </div>
                  ) : (
                    <pre className="preformatted">Диагноз не найден.</pre>
                  )}
                </article>
              ) : null}
            </div>
          ) : (
            <EmptyState
              title="Результат ещё не сформирован"
              description="После обработки здесь появятся отправленный текст, симптомы и диагноз."
            />
          )}
        </article>
      </div>
    </section>
  );
}

function renderHighlightedText(text: string, evidence: SymptomEvidence | null) {
  if (!text) {
    return 'Текст отсутствует.';
  }

  if (
    !evidence ||
    evidence.evidenceStart === null ||
    evidence.evidenceEnd === null ||
    evidence.evidenceStart < 0 ||
    evidence.evidenceEnd <= evidence.evidenceStart ||
    evidence.evidenceEnd > text.length
  ) {
    const fallbackRange = findEvidenceRange(text, evidence?.evidence ?? '');
    if (!fallbackRange) {
      return text;
    }

    return (
      <>
        {text.slice(0, fallbackRange.start)}
        <mark className={`evidence-highlight evidence-highlight--${evidence?.verificationStatus ?? 'needsReview'}`}>
          {text.slice(fallbackRange.start, fallbackRange.end)}
        </mark>
        {text.slice(fallbackRange.end)}
      </>
    );
  }

  return (
    <>
      {text.slice(0, evidence.evidenceStart)}
      <mark className={`evidence-highlight evidence-highlight--${evidence.verificationStatus}`}>
        {text.slice(evidence.evidenceStart, evidence.evidenceEnd)}
      </mark>
      {text.slice(evidence.evidenceEnd)}
    </>
  );
}

function getSelectedSymptoms(run: PromptRunBundle, excludedSymptoms: string[] | Set<string>, addedSymptoms: string[]) {
  const excludedSymptomsSet = excludedSymptoms instanceof Set ? excludedSymptoms : new Set(excludedSymptoms);
  const extractedSymptoms = (run.voting?.finalSymptoms ?? []).filter((symptom) => !excludedSymptomsSet.has(symptom));
  return [...extractedSymptoms, ...addedSymptoms]
    .filter((symptom, index, symptoms) => symptoms.indexOf(symptom) === index);
}

function getSymptomSuggestions(whitelistSymptoms: string[], selectedSymptoms: string[], query: string) {
  const normalizedQuery = normalizeSearchValue(query);
  if (!normalizedQuery) {
    return whitelistSymptoms
      .filter((symptom) => !selectedSymptoms.includes(symptom))
      .slice(0, 8);
  }

  return whitelistSymptoms
    .filter((symptom) => !selectedSymptoms.includes(symptom))
    .filter((symptom) => normalizeSearchValue(symptom).includes(normalizedQuery))
    .slice(0, 8);
}

function normalizeSearchValue(value: string) {
  return value.toLowerCase().replace('ё', 'е').trim();
}

function findEvidenceRange(text: string, evidence: string) {
  const normalizedEvidence = evidence.trim();
  if (!normalizedEvidence) {
    return null;
  }

  const index = text.toLowerCase().indexOf(normalizedEvidence.toLowerCase());
  return index < 0
    ? null
    : {
        start: index,
        end: index + normalizedEvidence.length
      };
}

function formatSymptoms(symptoms: string[]) {
  return symptoms.length > 0 ? symptoms.join('\n') : 'Симптомы не найдены.';
}

function formatSolverError(error: string) {
  if (!error.trim()) {
    return 'Не удалось связаться с сервисом определения диагноза. Проверьте подключение к интернету и повторите запрос.';
  }

  const normalizedError = error.toLowerCase();
  if (
    normalizedError.includes('host') ||
    normalizedError.includes('known') ||
    normalizedError.includes('dns') ||
    normalizedError.includes('connect') ||
    normalizedError.includes('network') ||
    normalizedError.includes('timed out') ||
    normalizedError.includes('timeout') ||
    normalizedError.includes('хост') ||
    normalizedError.includes('соедин')
  ) {
    return 'Не удалось связаться с сервисом определения диагноза. Проверьте подключение к интернету и повторите запрос.';
  }

  return `Не удалось получить диагноз: ${error}`;
}

interface SolverDiagnosis {
  id: number;
  name: string;
  description?: string;
  explanatorySet?: Array<{ id: number; name: string; description?: string }>;
}

function parseSolverDiagnoses(content: string): SolverDiagnosis[] {
  if (!content.trim()) {
    return [];
  }

  try {
    const parsed = JSON.parse(content) as unknown;
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed
      .filter((item): item is SolverDiagnosis => {
        if (!item || typeof item !== 'object') {
          return false;
        }

        const candidate = item as Partial<SolverDiagnosis>;
        return typeof candidate.id === 'number' && typeof candidate.name === 'string';
      })
      .sort((left, right) => (right.explanatorySet?.length ?? 0) - (left.explanatorySet?.length ?? 0));
  } catch {
    return [];
  }
}

function formatDiagnosis(diagnosis: SolverDiagnosis) {
  const parts = [`${diagnosis.name}${diagnosis.description ? ` (${diagnosis.description})` : ''}`];
  const explanatorySet = diagnosis.explanatorySet ?? [];

  if (explanatorySet.length > 0) {
    parts.push(`Подтверждающие признаки: ${explanatorySet.map((item) => item.name).join(', ')}`);
  }

  return parts.join('\n');
}
