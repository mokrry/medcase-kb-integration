import { useState } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState } from '../components/Common/EmptyState';
import { useAppState } from '../context/AppStateContext';
import { runPrompt } from '../api/promptApi';
import type { LlmProvider, PromptRunBundle } from '../types/prompt';
import { logToClientConsole } from '../utils/devConsoleLogger';

const PROVIDERS: LlmProvider[] = ['gemini', 'chatgpt', 'gigachat'];

export function ExtractionPage() {
  const { latestRun, setLatestRun } = useAppState();
  const [complaintsText, setComplaintsText] = useState('');
  const [processing, setProcessing] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);
  const [activeRun, setActiveRun] = useState<PromptRunBundle | null>(latestRun);

  function resetForm() {
    setComplaintsText('');
    setErrors([]);
    setActiveRun(null);
    setLatestRun(null);
  }

  async function handleSubmit() {
    const normalizedComplaints = complaintsText.trim();
    if (!normalizedComplaints) {
      setErrors(['Введите жалобы пациента']);
      return;
    }

    setProcessing(true);
    setErrors([]);

    void logToClientConsole({
      level: 'info',
      scope: 'PromptBuilder',
      message: 'Voting request received',
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
        setErrors(failedMessages.length > 0 ? failedMessages : ['Все запросы к LLM завершились ошибкой']);
        return;
      }

      setActiveRun(bundle);
      setLatestRun(bundle);

      void logToClientConsole({
        level: 'info',
        scope: 'PromptBuilder',
        message: 'Voting response received',
        data: {
          requestId: bundle.promptBuild.requestId,
          voting: bundle.voting,
          results: bundle.results.map((item) => ({
            provider: item.provider,
            model: item.model,
            error: item.error,
            score: item.review.score
          }))
        }
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Не удалось отправить промпт в LLM';
      setErrors([message]);
    } finally {
      setProcessing(false);
    }
  }

  const run = activeRun ?? latestRun;
  const finalAnswer = run ? formatVotingAnswer(run.voting.finalSymptoms) : '';

  return (
    <section className="grid gap-24">
      <div className="page-heading">
        <h2>Извлечение</h2>
        <p>Backend собирает промпт по базе знаний, сам вызывает модели, проверяет ответы по правилам и строит итог голосования.</p>
      </div>

      <div className="workspace-grid">
        <article className="panel panel--form">
          <div className="panel__header">
            <h3>Входные данные</h3>
            <p>Введите жалобы пациента. Один и тот же сценарий будет проверен сразу на трёх LLM.</p>
          </div>

          <label className="form-field">
            <span className="form-field__label">Жалобы пациента</span>
            <textarea
              className="textarea"
              rows={10}
              value={complaintsText}
              onChange={(event) => setComplaintsText(event.target.value)}
              placeholder="Например: слабость, температура 38.5, кашель с мокротой, одышка при нагрузке..."
            />
            <span className="form-field__hint">Вставьте жалобы пациента в свободном текстовом виде.</span>
          </label>

          <div className="meta-grid">
            <div className="meta-card">
              <span className="summary-card__label">LLM провайдеры</span>
              <strong>Gemini, ChatGPT, GigaChat</strong>
            </div>
            <div className="meta-card">
              <span className="summary-card__label">Источник симптомов</span>
              <strong>Локальная база знаний</strong>
            </div>
          </div>

          <div className="button-row">
            <button className="button button--primary" type="button" disabled={processing} onClick={handleSubmit}>
              {processing ? 'Собираю промпт и запускаю голосование...' : 'Отправить во все LLM'}
            </button>
            <button className="button button--secondary" type="button" onClick={resetForm}>
              Сбросить форму
            </button>
          </div>
        </article>

        <article className="panel">
          <div className="panel__header">
            <h3>Результат обработки</h3>
            <p>Здесь показываются собранный промпт, ответы моделей, проверка правил и итог голосования.</p>
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
              <div className="meta-grid">
                <div className="meta-card">
                  <span className="summary-card__label">Request ID</span>
                  <strong>{run.promptBuild.requestId}</strong>
                </div>
                <div className="meta-card">
                  <span className="summary-card__label">Прочитано симптомов</span>
                  <strong>{run.promptBuild.filledSymptoms} / {run.promptBuild.totalSymptoms}</strong>
                </div>
                <div className="meta-card">
                  <span className="summary-card__label">Успешных ответов</span>
                  <strong>{run.results.filter((item) => !item.error).length} / {run.results.length}</strong>
                </div>
                <div className="meta-card">
                  <span className="summary-card__label">Итог голосования</span>
                  <strong>{run.voting.finalSymptoms.length}</strong>
                </div>
              </div>

              {run.promptBuild.warnings.length > 0 ? (
                <div className="notice notice--warning">
                  {run.promptBuild.warnings.map((warning) => (
                    <p key={warning}>{warning}</p>
                  ))}
                </div>
              ) : null}

              <article className="panel panel--embedded">
                <h3>Сформированный промпт</h3>
                <pre className="preformatted">{run.promptBuild.prompt}</pre>
              </article>

              <article className="panel panel--embedded">
                <h3>Итоговый ответ</h3>
                <pre className="preformatted">{finalAnswer}</pre>
              </article>

              {run.results.map((result) => (
                <article className="panel panel--embedded" key={result.provider}>
                  <h3>{formatProviderName(result.provider)} {result.model ? `/ ${result.model}` : ''}</h3>
                  {result.error ? (
                    <div className="notice notice--error">
                      <p>{result.error}</p>
                    </div>
                  ) : (
                    <div className="grid gap-16">
                      <pre className="preformatted">{result.content || 'LLM вернула пустой ответ.'}</pre>
                      <div className="meta-grid">
                        <div className="meta-card">
                          <span className="summary-card__label">Score</span>
                          <strong>{result.review.score}</strong>
                        </div>
                        <div className="meta-card">
                          <span className="summary-card__label">Соответствие</span>
                          <strong>{result.review.isCompliant ? 'Да' : 'Нет'}</strong>
                        </div>
                      </div>
                      {result.review.issues.length > 0 ? (
                        <div className="notice notice--error">
                          {result.review.issues.map((issue) => (
                            <p key={issue}>{issue}</p>
                          ))}
                        </div>
                      ) : null}
                      {result.review.warnings.length > 0 ? (
                        <div className="notice notice--warning">
                          {result.review.warnings.map((warning) => (
                            <p key={warning}>{warning}</p>
                          ))}
                        </div>
                      ) : null}
                    </div>
                  )}
                </article>
              ))}

              <div className="button-row">
                <Link className="button button--secondary" to="/reliability">
                  Перейти к проверке
                </Link>
              </div>
            </div>
          ) : (
            <EmptyState
              title="Промпт еще не отправлен"
              description="После отправки жалоб здесь появятся собранный промпт, ответы моделей и итог голосования."
            />
          )}
        </article>
      </div>
    </section>
  );
}

function formatProviderName(provider: LlmProvider) {
  switch (provider) {
    case 'gemini':
      return 'Gemini';
    case 'chatgpt':
      return 'ChatGPT';
    case 'gigachat':
      return 'GigaChat';
  }
}

function formatVotingAnswer(symptoms: string[]) {
  return symptoms.length > 0
    ? symptoms.join('\n')
    : 'Ничего не найдено.';
}
