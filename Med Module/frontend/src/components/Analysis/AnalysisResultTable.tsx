import type { AnalysisResult } from '../../types/analysis';
import { StatusBadge } from './StatusBadge';

interface AnalysisResultTableProps {
  results: AnalysisResult[];
}

export function AnalysisResultTable({ results }: AnalysisResultTableProps) {
  return (
    <div className="table-wrapper">
      <table className="result-table">
        <thead>
          <tr>
            <th>Признак</th>
            <th>Категория</th>
            <th>Статус</th>
            <th>Основание</th>
          </tr>
        </thead>
        <tbody>
          {results.map((result) => (
            <tr key={`${result.category}-${result.featureName}`}>
              <td>{result.featureName}</td>
              <td>{result.category}</td>
              <td>
                <StatusBadge status={result.status} />
              </td>
              <td>{result.evidence || '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
