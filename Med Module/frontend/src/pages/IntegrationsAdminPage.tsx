import { useEffect, useState } from 'react';
import { getIntegrationStatuses, getSystemDiagnostics } from '../api/adminApi';
import type { AdminIntegrationStatus, AdminSystemDiagnostics } from '../types/admin';

export function IntegrationsAdminPage() {
  const [integrations, setIntegrations] = useState<AdminIntegrationStatus[]>([]);
  const [diagnostics, setDiagnostics] = useState<AdminSystemDiagnostics | null>(null);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isChecking, setIsChecking] = useState(false);

  useEffect(() => {
    void loadData(false);
  }, []);

  async function loadData(check: boolean) {
    setError('');
    setIsLoading(!check);
    setIsChecking(check);

    try {
      const [integrationItems, systemDiagnostics] = await Promise.all([
        getIntegrationStatuses(check),
        getSystemDiagnostics()
      ]);
      setIntegrations(integrationItems);
      setDiagnostics(systemDiagnostics);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : 'Не удалось загрузить диагностику.');
    } finally {
      setIsLoading(false);
      setIsChecking(false);
    }
  }

  return (
    <section className="grid gap-24">
      <div className="page-heading">
        <h2>Интеграции</h2>
        <p>Статусы модельных сервисов и системная диагностика.</p>
      </div>

      <article className="card">
        <div className="card__body">
          <div className="button-row">
            <button className="button button--secondary" type="button" onClick={() => loadData(false)} disabled={isLoading}>
              Обновить
            </button>
            <button className="button button--primary" type="button" onClick={() => loadData(true)} disabled={isChecking}>
              Проверить подключение
            </button>
          </div>

          {error && <div className="error-message">{error}</div>}
          {isLoading && <p className="muted">Загрузка...</p>}

          <div className="grid gap-24">
            <section>
              <h3>Модели</h3>
              <div className="table-wrapper">
                <table className="result-table">
                  <thead>
                    <tr>
                      <th>Провайдер</th>
                      <th>Модель</th>
                      <th>Ключ</th>
                      <th>Настроен</th>
                      <th>Проверка</th>
                    </tr>
                  </thead>
                  <tbody>
                    {integrations.map((item) => (
                      <tr key={item.provider}>
                        <td>{item.provider}</td>
                        <td>{item.model}</td>
                        <td>{item.keyStatus}</td>
                        <td>{item.configured ? 'Да' : 'Нет'}</td>
                        <td>{formatAvailability(item)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>

            {diagnostics && (
              <section>
                <h3>Системная диагностика</h3>
                <div className="summary-grid">
                  <StatusCard label="Backend" value={diagnostics.backendVersion} />
                  <StatusCard label="PostgreSQL" value={diagnostics.postgreSqlAvailable ? 'Доступен' : 'Ошибка'} />
                  <StatusCard label="Всего запросов" value={diagnostics.totalRequests.toString()} />
                  <StatusCard label="Успешных" value={diagnostics.completedRequests.toString()} />
                  <StatusCard label="Ошибок" value={diagnostics.failedRequests.toString()} />
                  <StatusCard label="В обработке" value={diagnostics.startedRequests.toString()} />
                </div>
                {diagnostics.lastError && (
                  <div className="notice notice--warning">
                    <p>{diagnostics.lastError}</p>
                  </div>
                )}
              </section>
            )}
          </div>
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

function formatAvailability(item: AdminIntegrationStatus) {
  if (item.available === null) {
    return item.lastCheckResult || 'Не проверялось';
  }

  return `${item.available ? 'Доступен' : 'Ошибка'}${item.lastCheckResult ? `: ${item.lastCheckResult}` : ''}`;
}
