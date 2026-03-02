import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  getAuthSessionInfo,
  getAuthUser,
  getAuthToken,
  logout,
  validateAuthSession,
  type AuthSessionInfo,
  type AuthUser,
} from '../services/authService';

function toIsAdmin(session: AuthSessionInfo | null): boolean {
  if (!session) {
    return false;
  }

  return session.roles.some((role) => role.toLowerCase() === 'admin');
}

export function useAuthCapabilities() {
  const [authUser, setAuthUser] = useState<AuthUser | null>(() => getAuthUser());
  const [sessionInfo, setSessionInfo] = useState<AuthSessionInfo | null>(() => getAuthSessionInfo());

  const syncFromStorage = useCallback(() => {
    setAuthUser(getAuthUser());
    setSessionInfo(getAuthSessionInfo());
  }, []);

  const refreshSession = useCallback(async () => {
    const token = getAuthToken();
    if (!token) {
      syncFromStorage();
      return null;
    }

    const session = await validateAuthSession();
    syncFromStorage();
    return session;
  }, [syncFromStorage]);

  const clearAuth = useCallback(() => {
    logout();
    syncFromStorage();
  }, [syncFromStorage]);

  useEffect(() => {
    const onStorage = () => {
      syncFromStorage();
    };

    window.addEventListener('storage', onStorage);
    return () => {
      window.removeEventListener('storage', onStorage);
    };
  }, [syncFromStorage]);

  const isAdmin = useMemo(() => toIsAdmin(sessionInfo), [sessionInfo]);

  return {
    authUser,
    sessionInfo,
    isAdmin,
    refreshSession,
    clearAuth,
  };
}