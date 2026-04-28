export interface PatientListItem {
  id: number;
  complaintsPreview: string;
}

export interface PatientDetails {
  id: number;
  complaints: string;
  anamnesis: string;
  physicalExam: string;
  fullText: string;
}
