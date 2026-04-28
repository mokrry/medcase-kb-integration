import type { AnalysisResponse } from '../../types/analysis';

export function AnalysisSummary({ result }: { result: AnalysisResponse }) {
  return (
    <section className="summary-grid">
      <article className="summary-card">
        <span className="summary-card__label">Всего признаков</span>
        <strong>{result.totalFeatures}</strong>
      </article>

      <article className="summary-card summary-card--found">
        <span className="summary-card__label">Найдено</span>
        <strong>{result.foundCount}</strong>
      </article>

      <article className="summary-card summary-card--not-found">
        <span className="summary-card__label">Не найдено</span>
        <strong>{result.notFoundCount}</strong>
      </article>

      <article className="summary-card summary-card--review">
        <span className="summary-card__label">Нужна проверка</span>
        <strong>{result.needsReviewCount}</strong>
      </article>
    </section>
  );
}
