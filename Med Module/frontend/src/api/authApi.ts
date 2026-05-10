import { apiGet, apiPost, clearAppStorage, setAccessToken } from './client';
import type { AuthResponse, LoginRequest, RegisterRequest, UserProfile } from '../types/auth';

export async function login(request: LoginRequest): Promise<AuthResponse> {
  const response = await apiPost<LoginRequest, AuthResponse>('/auth/login', request);
  clearAppStorage();
  setAccessToken(response.accessToken);
  return response;
}

export async function register(request: RegisterRequest): Promise<AuthResponse> {
  const response = await apiPost<RegisterRequest, AuthResponse>('/auth/register', request);
  clearAppStorage();
  setAccessToken(response.accessToken);
  return response;
}

export function getCurrentUser(): Promise<UserProfile> {
  return apiGet<UserProfile>('/auth/me');
}

export function logout() {
  clearAppStorage();
}
