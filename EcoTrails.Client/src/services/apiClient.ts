import axios from 'axios';

const DEFAULT_API_BASE_URL = 'http://127.0.0.1:5218/api';
const TOKEN_STORAGE_KEY = 'ecotrails:authToken';
const USER_STORAGE_KEY = 'ecotrails:authUser';
const SESSION_STORAGE_KEY = 'ecotrails:authSession';
const FORBIDDEN_NOTICE_KEY = 'ecotrails:forbiddenNotice';

export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? DEFAULT_API_BASE_URL;

const apiClient = axios.create({
  baseURL: apiBaseUrl,
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_STORAGE_KEY);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const status = error?.response?.status as number | undefined;
    const requestUrl = String(error?.config?.url ?? '');
    const isAuthLoginOrRegister =
      requestUrl.includes('/auth/login') || requestUrl.includes('/auth/register');

    if (status === 401 && !isAuthLoginOrRegister) {
      localStorage.removeItem(TOKEN_STORAGE_KEY);
      localStorage.removeItem(USER_STORAGE_KEY);
      localStorage.removeItem(SESSION_STORAGE_KEY);

      if (!window.location.pathname.startsWith('/auth')) {
        window.location.assign('/auth?reason=session-expired');
      }
    }

    if (status === 403) {
      sessionStorage.setItem(
        FORBIDDEN_NOTICE_KEY,
        'Нямате достатъчни права за това действие.',
      );

      if (window.location.pathname !== '/') {
        window.location.assign('/');
      }
    }

    return Promise.reject(error);
  },
);

export default apiClient;
export { TOKEN_STORAGE_KEY };

export async function* postStream<TRequest = unknown>(
  path: string,
  request: TRequest,
  signal?: AbortSignal,
): AsyncGenerator<string, void, unknown> {
  const token = localStorage.getItem(TOKEN_STORAGE_KEY);
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    throw new Error(`Streaming request failed: ${response.statusText}`);
  }

  const reader = response.body?.getReader();
  if (!reader) {
    throw new Error('Streaming not supported by response body.');
  }

  const decoder = new TextDecoder();
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      const chunk = decoder.decode(value, { stream: true });
      yield chunk;
    }
  } finally {
    reader.releaseLock();
  }
}