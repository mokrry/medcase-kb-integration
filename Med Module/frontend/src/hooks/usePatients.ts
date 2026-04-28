import { useEffect, useState } from 'react';
import { getPatients } from '../api/patientsApi';
import type { PatientListItem } from '../types/patient';

export function usePatients(page = 1, pageSize = 20) {
  const [patients, setPatients] = useState<PatientListItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string>('');

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        setError('');

        const data = await getPatients(page, pageSize);
        if (!Array.isArray(data)) {
          throw new Error('Backend вернул неожиданный формат списка пациентов.');
        }

        if (!cancelled) {
          setPatients(data);
        }
      } catch (e) {
        if (!cancelled) {
          setError(e instanceof Error ? e.message : 'Не удалось загрузить список пациентов.');
          setPatients([]);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    load();

    return () => {
      cancelled = true;
    };
  }, [page, pageSize]);

  return { patients, loading, error };
}
