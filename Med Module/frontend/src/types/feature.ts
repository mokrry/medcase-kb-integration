export type FeatureCategory = 'Complaint' | 'Anamnesis';

export interface Feature {
  name: string;
  category: FeatureCategory | string;
}
