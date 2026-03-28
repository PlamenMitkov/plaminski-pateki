import apiClient from './apiClient';

const TOKEN_STORAGE_KEY = 'ecotrails:authToken';
const USER_STORAGE_KEY = 'ecotrails:authUser';
const SESSION_STORAGE_KEY = 'ecotrails:authSession';

export interface AuthUser {
  userId: string;
  email: string;
}

export interface AuthSessionInfo extends AuthUser {
  roles: string[];
  userName?: string;
  phoneNumber?: string;
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

function normalizeRoles(roles: string[] | undefined): string[] {
  if (!Array.isArray(roles)) {
    return [];
  }

  return roles
    .map((role) => String(role).trim())
    .filter((role) => role.length > 0);
}

function saveSession(session: AuthSessionInfo) {
  const roles = normalizeRoles(session.roles);
  localStorage.setItem(
    SESSION_STORAGE_KEY,
    JSON.stringify({ userId: session.userId, email: session.email, roles }),
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
  const response = await apiClient.post<AuthResponse>('/auth/login', {
    email,
    password,
  });

  saveAuth(response.data);
  await validateAuthSession();
  return { userId: response.data.userId, email: response.data.email };
}

export async function register(email: string, password: string): Promise<AuthUser> {
  const response = await apiClient.post<AuthResponse>('/auth/register', {
    email,
    password,
  });

  saveAuth(response.data);
  await validateAuthSession();
  return { userId: response.data.userId, email: response.data.email };
}

export function logout() {
  localStorage.removeItem(TOKEN_STORAGE_KEY);
  localStorage.removeItem(USER_STORAGE_KEY);
  localStorage.removeItem(SESSION_STORAGE_KEY);
}

export async function validateAuthSession(): Promise<AuthSessionInfo | null> {
  const token = getAuthToken();
  if (!token) {
    return null;
  }

  try {
    const response = await apiClient.get<AuthSessionInfo>('/auth/me');
    const session = response.data;
    localStorage.setItem(
      USER_STORAGE_KEY,
      JSON.stringify({ userId: session.userId, email: session.email }),
    );
    saveSession(session);
    return session;
  } catch {
    logout();
    return null;
  }
}

export function getAuthSessionInfo(): AuthSessionInfo | null {
  const value = localStorage.getItem(SESSION_STORAGE_KEY);
  if (!value) {
    return null;
  }

  try {
    const parsed = JSON.parse(value) as AuthSessionInfo;
    if (!parsed.userId || !parsed.email) {
      return null;
    }

    return {
      userId: parsed.userId,
      email: parsed.email,
      roles: normalizeRoles(parsed.roles),
    };
  } catch {
    return null;
  }
}

export function isCurrentUserAdmin(): boolean {
  const session = getAuthSessionInfo();
  if (!session) {
    return false;
  }

  return session.roles.some((role) => role.toLowerCase() === 'admin');
}

export interface UpdateProfileResponse extends AuthSessionInfo {
  userName?: string;
  phoneNumber?: string;
}

export interface UpdateProfileRequest {
  email?: string;
  userName?: string;
  phoneNumber?: string;
}

export async function updateProfile(request: UpdateProfileRequest): Promise<UpdateProfileResponse> {
  const response = await apiClient.put<UpdateProfileResponse>('/auth/profile', request);
  
  // Update stored user and session info
  if (response.data.email && response.data.userId) {
    localStorage.setItem(
      USER_STORAGE_KEY,
      JSON.stringify({ userId: response.data.userId, email: response.data.email }),
    );
  }
  
  if (response.data.userId && response.data.email) {
    saveSession({
      userId: response.data.userId,
      email: response.data.email,
      roles: response.data.roles || [],
    });
  }
  
  return response.data;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ChangePasswordResponse {
  token: string;
}

export async function changePassword(request: ChangePasswordRequest): Promise<void> {
  const response = await apiClient.post<ChangePasswordResponse>('/auth/change-password', request);
  
  // Save new token
  if (response.data.token) {
    localStorage.setItem(TOKEN_STORAGE_KEY, response.data.token);
  }
  
  // Revalidate session with new token
  await validateAuthSession();
}