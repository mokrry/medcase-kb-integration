import { Link } from 'react-router-dom';
import type { PatientListItem } from '../../types/patient';

interface PatientCardProps {
  patient: PatientListItem;
}

export function PatientCard({ patient }: PatientCardProps) {
  return (
    <article className="card">
      <div className="card__body">
        <h3>Пациент #{patient.id}</h3>
        <p>{patient.complaintsPreview}</p>
      </div>

      <div className="card__actions">
        <Link className="button button--primary" to={`/patients/${patient.id}`}>
          Открыть
        </Link>
      </div>
    </article>
  );
}
