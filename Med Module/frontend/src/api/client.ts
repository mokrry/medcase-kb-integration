import { logToClientConsole } from '../utils/devConsoleLogger';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5274/api').replace(/\/$/, '');

async function readResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let details = '';
    try {
      details = await response.text();
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
    headers: {
      Accept: 'application/json'
    }
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
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json'
    },
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
