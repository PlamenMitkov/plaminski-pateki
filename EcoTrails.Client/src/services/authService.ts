import axios from 'axios';

const API_BASE_URL = 'http://127.0.0.1:5218/api';
const TOKEN_STORAGE_KEY = 'ecotrails:authToken';
const USER_STORAGE_KEY = 'ecotrails:authUser';

export interface AuthUser {
  userId: string;
  email: string;
}

interface AuthResponse {
  token: string;
  userId: string;
  email: string;
}

function saveAuth(authResponse: AuthResponse) {
  localStorage.setItem(TOKEN_STORAGE_KEY, authResponse.token);
  localStorage.setItem(
    USER_STORAGE_KEY,
    JSON.stringify({ userId: authResponse.userId, email: authResponse.email }),
  );
}

export function getAuthToken(): string | null {
  return localStorage.getItem(TOKEN_STORAGE_KEY);
}

export function getAuthUser(): AuthUser | null {
  const value = localStorage.getItem(USER_STORAGE_KEY);
  if (!value) {
    return null;
  }

  try {
    const parsed = JSON.parse(value) as AuthUser;
    if (!parsed.userId || !parsed.email) {
      return null;
    }

    return parsed;
  } catch {
    return null;
  }
}

export function isAuthenticated(): boolean {
  return !!getAuthToken();
}

export async function login(email: string, password: string): Promise<AuthUser> {
  const response = await axios.post<AuthResponse>(`${API_BASE_URL}/auth/login`, {
    email,
    password,
  });

  saveAuth(response.data);
  return { userId: response.data.userId, email: response.data.email };
}

export async function register(email: string, password: string): Promise<AuthUser> {
  const response = await axios.post<AuthResponse>(`${API_BASE_URL}/auth/register`, {
    email,
    password,
  });

  saveAuth(response.data);
  return { userId: response.data.userId, email: response.data.email };
}

export function logout() {
  localStorage.removeItem(TOKEN_STORAGE_KEY);
  localStorage.removeItem(USER_STORAGE_KEY);
}