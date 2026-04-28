import { useEffect, useState } from 'react';
import { analyzePatient } from '../api/patientsApi';
import type { AnalysisResponse } from '../types/analysis';

export function useAnalysis(
  patientId?: number,
  includeComplaintsFeatures = true,
  includeAnamnesisFeatures = true
) {
  const [result, setResult] = useState<AnalysisResponse | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string>('');

  useEffect(() => {
    if (patientId === undefined || Number.isNaN(patientId)) {
      setLoading(false);
      setError('Не передан корректный идентификатор пациента.');
      return;
    }

    const resolvedPatientId = patientId;
    let cancelled = false;

    async function runAnalysis() {
      try {
        setLoading(true);
        setError('');
        const data = await analyzePatient({
          patientId: resolvedPatientId,
          includeComplaintsFeatures,
          includeAnamnesisFeatures
        });
        if (!cancelled) {
          setResult(data);
        }
      } catch (e) {
        if (!cancelled) {
          setError(e instanceof Error ? e.message : 'Не удалось выполнить анализ.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    runAnalysis();

    return () => {
      cancelled = true;
    };
  }, [patientId, includeComplaintsFeatures, includeAnamnesisFeatures]);

  return { result, loading, error };
}
