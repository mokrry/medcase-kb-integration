import type { PatientDetails } from '../../types/patient';

interface PatientTextBlockProps {
  patient: PatientDetails;
}

export function PatientTextBlock({ patient }: PatientTextBlockProps) {
  return (
    <section className="grid gap-16">
      <article className="panel">
        <h3>Жалобы</h3>
        <p>{patient.complaints || 'Нет данных'}</p>
      </article>

      <article className="panel">
        <h3>Анамнез заболевания</h3>
        <p>{patient.anamnesis || 'Нет данных'}</p>
      </article>

      <article className="panel">
        <h3>Физикальное обследование</h3>
        <p>{patient.physicalExam || 'Нет данных'}</p>
      </article>
    </section>
  );
}
