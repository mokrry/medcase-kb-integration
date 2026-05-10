import { useEffect, useState } from 'react';
import { getRequestDetails, getRequests, type RequestLogFilters } from '../api/requestLogApi';
import { useAppState } from '../context/AppStateContext';
import type { ProcessingRequestDetails, ProcessingRequestListItem } from '../types/requestLog';

interface SolverDiagnosis {
  id: number;
  name: string;
  description?: string;
  explanatorySet?: Array<{
    id: number;
    name: string;
    description?: string;
  }>;
}

export function RequestLogPage() {
  const {
    requestLogItems,
    setRequestLogItems,
    selectedRequestDetails,
    setSelectedRequestDetails
  } = useAppState();
  const [filters, setFilters] = useState<RequestLogFilters>({});
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(requestLogItems.length === 0);

  useEffect(() => {
    void loadRequests(filters);
  }, []);

  async function loadRequests(nextFilters = filters) {
    setIsLoading(true);
    setError('');

    try {
      const items = await getRequests(nextFilters);
      setRequestLogItems(items);

      if (selectedRequestDetails && !items.some((item) => item.id === selectedRequestDetails.id)) {
        setSelectedRequestDetails(null);
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : 'Не удалось загрузить журнал.');
    } finally {
      setIsLoading(false);
    }
  }

  async function openRequest(id: string) {
    setError('');

    try {
      setSelectedRequestDetails(await getRequestDetails(id));
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : 'Не удалось загрузить детали запроса.');
    }
  }

  function updateFilter(key: keyof RequestLogFilters, value: string) {
    setFilters((current) => ({
      ...current,
      [key]: value || undefined
    }));
  }

  function applyFilters() {
    void loadRequests(filters);
  }

  function resetFilters() {
    const emptyFilters: RequestLogFilters = {};
    setFilters(emptyFilters);
    void loadRequests(emptyFilters);
  }

  return (
    <section className="grid gap-24">
      <article className="card">
        <div className="card__body">
          <h2>Журнал запросов</h2>
          <p className="muted">
            Выберите запись в истории. Детали откроются рядом с выбранной карточкой на той же
            высоте.
          </p>

          <div className="form-grid">
            <div className="filter-row">
              <label className="form-field">
                <span>Статус</span>
                <select
                  className="input"
                  value={filters.status ?? ''}
                  onChange={(event) => updateFilter('status', event.target.value)}
                >
                  <option value="">Все статусы</option>
                  <option value="Started">В обработке</option>
                  <option value="Completed">Завершён</option>
                  <option value="Failed">Ошибка</option>
                </select>
              </label>

              <label className="form-field">
                <span>Дата с</span>
                <input
                  className="input"
                  type="date"
                  value={filters.dateFrom ?? ''}
                  onChange={(event) => updateFilter('dateFrom', event.target.value)}
                />
              </label>

              <label className="form-field">
                <span>Дата по</span>
                <input
                  className="input"
                  type="date"
                  value={filters.dateTo ?? ''}
                  onChange={(event) => updateFilter('dateTo', event.target.value)}
                />
              </label>
            </div>

            <div className="button-row">
              <button className="button button--primary" type="button" onClick={applyFilters} disabled={isLoading}>
                Применить фильтры
              </button>
              <button className="button button--secondary" type="button" onClick={resetFilters} disabled={isLoading}>
                Сбросить
              </button>
              <button className="button button--secondary" type="button" onClick={() => loadRequests()} disabled={isLoading}>
                Обновить
              </button>
            </div>
          </div>

          {error && <div className="error-message">{error}</div>}
        </div>
      </article>

      <article className="card request-log-list-card">
        <div className="card__body">
          <h3>История</h3>
          {requestLogItems.length === 0 && (
            <p className="muted">{isLoading ? 'Загрузка...' : 'Запросов по выбранным фильтрам нет.'}</p>
          )}
          <div className="log-list">
            {requestLogItems.map((request) => (
              <div
                className={`request-log-row${selectedRequestDetails?.id === request.id ? ' request-log-row--selected' : ''}`}
                key={request.id}
              >
                <button
                  className={`log-list__item${selectedRequestDetails?.id === request.id ? ' log-list__item--active' : ''}`}
                  type="button"
                  onClick={() => openRequest(request.id)}
                >
                  <strong>{getRequestKindTitle(request)}</strong>
                  <span>{formatStatus(request.status)}</span>
                  <small>{new Date(request.createdAt).toLocaleString()}</small>
                  <small className="muted">{request.requestId}</small>
                </button>

                {selectedRequestDetails?.id === request.id && (
                  <article className="request-log-inline-details">
                    <h3>Детали</h3>
                    <RequestDetails request={selectedRequestDetails} />
                  </article>
                )}
              </div>
            ))}
          </div>
        </div>
      </article>
    </section>
  );
}

function RequestDetails({ request }: { request: ProcessingRequestDetails }) {
  const symptoms = parseStringArray(request.finalSymptomsJson);
  const diagnoses = parseDiagnoses(request.solverResponseJson);
  const isSolve = isSolveRequest(request);

  return (
    <div className="grid gap-16">
      <div className="request-summary">
        <span className="table-tag">{getRequestKindTitle(request)}</span>
        <span className="table-tag">{formatStatus(request.status)}</span>
      </div>

      <p>
        <strong>Время:</strong> {new Date(request.createdAt).toLocaleString()}
      </p>
      <p>
        <strong>Технический ID:</strong> {request.requestId}
      </p>
      {request.errorMessage && (
        <p className="error-message">
          <strong>Ошибка:</strong> {request.errorMessage}
        </p>
      )}

      <section>
        <h4>Медицинский текст</h4>
        <p className="text-panel">{request.sourceText || request.preparedText || 'Текст не сохранён.'}</p>
      </section>

      <section>
        <h4>{isSolve ? 'Симптомы для определения диагноза' : 'Извлечённые симптомы'}</h4>
        {symptoms.length > 0 ? (
          <ul className="clean-list">
            {symptoms.map((symptom) => (
              <li key={symptom}>{symptom}</li>
            ))}
          </ul>
        ) : (
          <p className="muted">Симптомы не сохранены.</p>
        )}
      </section>

      {diagnoses.length > 0 && (
        <section>
          <h4>{isSolve ? 'Результат определения диагноза' : 'Предварительные диагностические гипотезы'}</h4>
          <div className="diagnosis-list">
            {diagnoses.map((diagnosis, index) => (
              <article className="diagnosis-card" key={`${diagnosis.id}-${diagnosis.name}`}>
                <strong>
                  {index === 0 ? 'Наиболее вероятно: ' : ''}
                  {formatDiagnosisTitle(diagnosis)}
                </strong>
                {diagnosis.explanatorySet && diagnosis.explanatorySet.length > 0 && (
                  <p className="muted">
                    Подтверждающие признаки: {diagnosis.explanatorySet.map((item) => item.name).join(', ')}
                  </p>
                )}
              </article>
            ))}
          </div>
        </section>
      )}

      {diagnoses.length === 0 && isSolve && <p className="muted">Ответ solver не содержит диагнозов.</p>}
    </div>
  );
}

function isSolveRequest(request: ProcessingRequestListItem) {
  return request.requestId.startsWith('SOLVE-') || request.internalMode === 'manual-symptoms-solver';
}

function getRequestKindTitle(request: ProcessingRequestListItem) {
  if (isSolveRequest(request)) {
    return 'Определение диагноза';
  }

  if (request.requestId.startsWith('PROMPT-') || request.usedVoting) {
    return 'Извлечение симптомов';
  }

  return 'Запрос';
}

function formatStatus(status: string) {
  switch (status) {
    case 'Completed':
      return 'Завершён';
    case 'Started':
      return 'В обработке';
    case 'Failed':
      return 'Ошибка';
    default:
      return status || 'Неизвестно';
  }
}

function parseStringArray(value: string): string[] {
  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === 'string') : [];
  } catch {
    return [];
  }
}

function parseDiagnoses(value: string): SolverDiagnosis[] {
  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed) ? parsed.filter(isSolverDiagnosis) : [];
  } catch {
    return [];
  }
}

function isSolverDiagnosis(value: unknown): value is SolverDiagnosis {
  return Boolean(value && typeof value === 'object' && 'name' in value);
}

function formatDiagnosisTitle(diagnosis: SolverDiagnosis) {
  return diagnosis.description ? `${diagnosis.name} (${diagnosis.description})` : diagnosis.name;
}
