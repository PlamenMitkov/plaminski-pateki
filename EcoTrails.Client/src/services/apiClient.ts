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