import { useEffect, useState } from 'react';
import { getKnowledgeBaseStatus } from '../api/adminApi';
import type { AdminKnowledgeBaseStatus } from '../types/admin';

export function KnowledgeBaseAdminPage() {
  const [status, setStatus] = useState<AdminKnowledgeBaseStatus | null>(null);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    void loadStatus();
  }, []);

  async function loadStatus() {
    setIsLoading(true);
    setError('');

    try {
      setStatus(await getKnowledgeBaseStatus());
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : 'Не удалось загрузить статус базы знаний.');
    } finally {
      setIsLoading(false);
    }
  }

  function downloadPayload() {
    if (!status?.lastPayloadJson) {
      return;
    }

    const blob = new Blob([status.lastPayloadJson], { type: 'application/json;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'solver-payload.json';
    link.click();
    URL.revokeObjectURL(url);
  }

  return (
    <section className="grid gap-24">
      <div className="page-heading">
        <h2>База знаний</h2>
        <p>Техническая диагностика подключения к базе знаний и solver.</p>
      </div>

      <article className="card">
        <div className="card__body">
          <div className="button-row">
            <button className="button button--secondary" type="button" onClick={loadStatus} disabled={isLoading}>
              Обновить
            </button>
            <button
              className="button button--secondary"
              type="button"
              onClick={downloadPayload}
              disabled={!status?.lastPayloadJson}
            >
              Скачать JSON payload
            </button>
          </div>

          {error && <div className="error-message">{error}</div>}
          {isLoading && <p className="muted">Загрузка...</p>}

          {status && (
            <div className="grid gap-24">
              <div className="summary-grid">
                <StatusCard label="Файл БЗ" value={status.fileFound ? 'Найден' : 'Не найден'} />
                <StatusCard label="Имя файла" value={status.fileName || 'Не задано'} />
                <StatusCard label="Листов" value={status.worksheetCount.toString()} />
                <StatusCard label="Solver" value={status.solverAvailable ? 'Доступен' : 'Ошибка'} />
              </div>

              <section>
                <h3>Ключевые таблицы</h3>
                {status.keyTables.length > 0 ? (
                  <ul className="clean-list">
                    {status.keyTables.map((table) => (
                      <li key={table}>{table}</li>
                    ))}
                  </ul>
                ) : (
                  <p className="muted">Ключевые таблицы не найдены.</p>
                )}
              </section>

              <section>
                <h3>Статус solver endpoint</h3>
                <pre className="preformatted">{status.solverStatus || 'Нет данных.'}</pre>
              </section>

              <section>
                <h3>Последний payload</h3>
                <pre className="preformatted">{status.lastPayloadJson || 'Payload ещё не формировался.'}</pre>
              </section>

              <section>
                <h3>Последний ответ solver</h3>
                <pre className="preformatted">{status.lastSolverResponseJson || 'Ответ solver ещё не получался.'}</pre>
              </section>
            </div>
          )}
        </div>
      </article>
    </section>
  );
}

function StatusCard({ label, value }: { label: string; value: string }) {
  return (
    <article className="summary-card">
      <span className="summary-card__label">{label}</span>
      <strong>{value}</strong>
    </article>
  );
}
