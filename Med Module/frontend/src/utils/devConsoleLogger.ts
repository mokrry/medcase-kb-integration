type ClientLogLevel = 'info' | 'error' | 'warn';

interface ClientLogPayload {
  level: ClientLogLevel;
  scope: string;
  message: string;
  data?: unknown;
}

export async function logToClientConsole(payload: ClientLogPayload) {
  if (!import.meta.env.DEV) {
    return;
  }

  try {
    await fetch('/__client-log', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        ...payload,
        timestamp: new Date().toISOString()
      }),
      keepalive: true
    });
  } catch {
    // Ignore transport issues in development logging.
  }
}
