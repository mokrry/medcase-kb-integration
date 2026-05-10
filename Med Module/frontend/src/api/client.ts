import { logToClientConsole } from '../utils/devConsoleLogger';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5274/api').replace(/\/$/, '');
const ACCESS_TOKEN_KEY = 'med-module.access-token';
const APP_STORAGE_PREFIX = 'med-module.';

export function getAccessToken() {
  return localStorage.getItem(ACCESS_TOKEN_KEY);
}

export function setAccessToken(token: string) {
  localStorage.setItem(ACCESS_TOKEN_KEY, token);
  window.dispatchEvent(new CustomEvent('med-module:auth-changed'));
}

export function clearAccessToken() {
  localStorage.removeItem(ACCESS_TOKEN_KEY);
  window.dispatchEvent(new CustomEvent('med-module:auth-changed'));
}

export function clearAppStorage() {
  const keysToRemove: string[] = [];

  for (let index = 0; index < localStorage.length; index += 1) {
    const key = localStorage.key(index);
    if (key?.startsWith(APP_STORAGE_PREFIX)) {
      keysToRemove.push(key);
    }
  }

  keysToRemove.forEach((key) => localStorage.removeItem(key));
  window.dispatchEvent(new CustomEvent('med-module:auth-changed'));
  window.dispatchEvent(new CustomEvent('med-module:reset-state'));
}

function buildHeaders(contentType?: string): HeadersInit {
  const headers: Record<string, string> = {
    Accept: 'application/json'
  };

  if (contentType) {
    headers['Content-Type'] = contentType;
  }

  const token = getAccessToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  return headers;
}

async function readResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    if (response.status === 401) {
      clearAppStorage();
      window.dispatchEvent(new CustomEvent('med-module:unauthorized'));
    }

    let details = '';
    try {
      const contentType = response.headers.get('content-type') ?? '';
      if (contentType.includes('application/json')) {
        const payload = (await response.json()) as { message?: unknown; title?: unknown; detail?: unknown };
        details =
          [payload.message, payload.detail, payload.title].find((value): value is string => typeof value === 'string') ??
          '';
      } else {
        details = await response.text();
      }
    } catch {
      details = '';
    }
    throw new Error(details || `HTTP ${response.status}`);
  }

  return response.json() as Promise<T>;
}

export async function apiGet<T>(path: string): Promise<T> {
  const url = `${API_BASE_URL}${path}`;
  void logToClientConsole({
    level: 'info',
    scope: 'API',
    message: 'Request sent',
    data: { method: 'GET', url }
  });

  const response = await fetch(url, {
    method: 'GET',
    headers: buildHeaders()
  });

  void logToClientConsole({
    level: response.ok ? 'info' : 'error',
    scope: 'API',
    message: 'Response received',
    data: { method: 'GET', url, status: response.status, ok: response.ok }
  });
  return readResponse<T>(response);
}

export async function apiPost<TRequest, TResponse>(path: string, body: TRequest): Promise<TResponse> {
  const url = `${API_BASE_URL}${path}`;
  void logToClientConsole({
    level: 'info',
    scope: 'API',
    message: 'Request sent',
    data: { method: 'POST', url, body }
  });

  const response = await fetch(url, {
    method: 'POST',
    headers: buildHeaders('application/json'),
    body: JSON.stringify(body)
  });

  void logToClientConsole({
    level: response.ok ? 'info' : 'error',
    scope: 'API',
    message: 'Response received',
    data: { method: 'POST', url, status: response.status, ok: response.ok }
  });
  return readResponse<TResponse>(response);
}

export async function apiPostForm<TResponse>(path: string, body: FormData): Promise<TResponse> {
  const url = `${API_BASE_URL}${path}`;
  void logToClientConsole({
    level: 'info',
    scope: 'API',
    message: 'Request sent',
    data: { method: 'POST', url, contentType: 'multipart/form-data' }
  });

  const response = await fetch(url, {
    method: 'POST',
    headers: buildHeaders(),
    body
  });

  void logToClientConsole({
    level: response.ok ? 'info' : 'error',
    scope: 'API',
    message: 'Response received',
    data: { method: 'POST', url, status: response.status, ok: response.ok }
  });

  return readResponse<TResponse>(response);
}
