import { Link } from 'react-router-dom';
import { EmptyState } from '../components/Common/EmptyState';
import { useAppState } from '../context/AppStateContext';

export function ReliabilityPage() {
  const { latestRun } = useAppState();

  if (!latestRun) {
    return (
      <EmptyState
        title="Нет данных для проверки"
        description="Сначала отправьте промпт на странице извлечения."
      />
    );
  }

  const finalAnswer = formatVotingAnswer(latestRun.voting.finalSymptoms);

  return (
    <section className="grid gap-24">
      <div className="page-heading">
        <h2>Проверка</h2>
        <p>Раздел показывает, насколько полно backend подготовил данные перед отправкой промпта в модели.</p>
      </div>

      <section className="summary-grid">
        <article className="summary-card summary-card--found">
          <span className="summary-card__label">Заполнено симптомов</span>
          <strong>{latestRun.promptBuild.filledSymptoms}</strong>
        </article>
        <article className="summary-card">
          <span className="summary-card__label">Всего строк таблицы</span>
          <strong>{latestRun.promptBuild.totalSymptoms}</strong>
        </article>
        <article className="summary-card summary-card--review">
          <span className="summary-card__label">Предупреждения</span>
          <strong>{latestRun.promptBuild.warnings.length}</strong>
        </article>
      </section>

      <article className="panel">
        <h3>Итоговый ответ</h3>
        <pre className="preformatted">{finalAnswer}</pre>
      </article>

      <article className="panel">
        <h3>Контроль перед отправкой в LLM</h3>
        <div className="table-wrapper">
          <table className="result-table">
            <thead>
              <tr>
                <th>Симптом</th>
                <th>Комментарий</th>
              </tr>
            </thead>
            <tbody>
              {latestRun.promptBuild.symptoms.map((item, index) => (
                <tr key={`${item.name}-${index}`}>
                  <td>{item.name || '-'}</td>
                  <td>{item.note || '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </article>

      {latestRun.promptBuild.warnings.length > 0 ? (
        <article className="panel">
          <h3>Предупреждения</h3>
          <div className="notice notice--warning">
            {latestRun.promptBuild.warnings.map((warning) => (
              <p key={warning}>{warning}</p>
            ))}
          </div>
        </article>
      ) : null}

      <div className="button-row">
        <Link className="button button--secondary" to="/extract">
          Вернуться к извлечению
        </Link>
      </div>
    </section>
  );
}

function formatVotingAnswer(symptoms: string[]) {
  return symptoms.length > 0
    ? symptoms.join('\n')
    : 'Ничего не найдено.';
}
