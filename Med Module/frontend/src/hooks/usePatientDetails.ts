import { useEffect, useState } from 'react';
import { getPatientDetails } from '../api/patientsApi';
import type { PatientDetails } from '../types/patient';

export function usePatientDetails(id?: number) {
  const [patient, setPatient] = useState<PatientDetails | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string>('');

  useEffect(() => {
    if (id === undefined || Number.isNaN(id)) {
      setLoading(false);
      setError('Не передан корректный идентификатор пациента.');
      return;
    }

    const patientId = id;
    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        setError('');
        const data = await getPatientDetails(patientId);
        if (!cancelled) {
          setPatient(data);
        }
      } catch (e) {
        if (!cancelled) {
          setError(e instanceof Error ? e.message : 'Не удалось загрузить пациента.');
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
  }, [id]);

  return { patient, loading, error };
}
