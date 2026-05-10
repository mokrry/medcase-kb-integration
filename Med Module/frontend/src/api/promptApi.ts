import { apiPost, apiPostForm } from './client';
import type { KnowledgeBaseSolver, PromptBuildResponse, PromptRunBundle } from '../types/prompt';

export function buildPrompt(complaintsText: string): Promise<PromptBuildResponse> {
  const formData = new FormData();
  formData.append('complaintsText', complaintsText);
  return apiPostForm<PromptBuildResponse>('/prompt/build', formData);
}

export function runPrompt(
  complaintsText: string,
  providers: string[] = ['gemini', 'chatgpt']
): Promise<PromptRunBundle> {
  const formData = new FormData();
  formData.append('complaintsText', complaintsText);
  providers.forEach((provider) => formData.append('providers', provider));
  return apiPostForm<PromptRunBundle>('/prompt/run-bundle', formData);
}

export function solveSymptoms(complaintsText: string, symptoms: string[]): Promise<KnowledgeBaseSolver> {
  return apiPost('/prompt/solve-symptoms', {
    complaintsText,
    symptoms
  });
}
