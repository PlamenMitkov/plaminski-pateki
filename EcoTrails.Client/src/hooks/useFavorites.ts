import { useEffect, useMemo, useRef, useReducer } from 'react';
import axios from 'axios';
import { getAuthToken, getAuthUser, isAuthenticated } from '../services/authService';
import apiClient from '../services/apiClient';

const STORAGE_KEY = 'ecotrails:favorites';
const SYNCED_USER_STORAGE_KEY = 'ecotrails:favoritesSyncedUser';

function parseStoredIds(value: string | null): number[] {
  if (!value) return [];
  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed) ? parsed.map(Number).filter(n => Number.isInteger(n) && n > 0) : [];
  } catch {
    return [];
  }
}

interface FavoritesState {
  favoriteIds: number[];
  isSyncing: boolean;
  lastSyncError: string;
}

type FavoritesAction =
  | { type: 'SET_FAVORITES'; ids: number[] }
  | { type: 'TOGGLE_FAVORITE'; trailId: number }
  | { type: 'START_SYNC' }
  | { type: 'SYNC_SUCCESS' }
  | { type: 'SYNC_ERROR'; error: string }
  | { type: 'CLEAR_FAVORITES' };

function favoritesReducer(state: FavoritesState, action: FavoritesAction): FavoritesState {
  switch (action.type) {
    case 'SET_FAVORITES':
      return { ...state, favoriteIds: action.ids };
    case 'TOGGLE_FAVORITE': {
      const { trailId } = action;
      const favoriteIds = state.favoriteIds.includes(trailId)
        ? state.favoriteIds.filter((id) => id !== trailId)
        : [...state.favoriteIds, trailId];
      return { ...state, favoriteIds };
    }
    case 'START_SYNC':
      return { ...state, isSyncing: true, lastSyncError: '' };
    case 'SYNC_SUCCESS':
      return { ...state, isSyncing: false };
    case 'SYNC_ERROR':
      return { ...state, isSyncing: false, lastSyncError: action.error };
    case 'CLEAR_FAVORITES':
      return { ...state, favoriteIds: [], lastSyncError: '' };
    default:
      return state;
  }
}

export function useFavorites() {
  const [state, dispatch] = useReducer(favoritesReducer, {
    favoriteIds: parseStoredIds(localStorage.getItem(STORAGE_KEY)),
    isSyncing: false,
    lastSyncError: '',
  });

  const isFirstRenderRef = useRef(true);
  const abortControllerRef = useRef<AbortController | null>(null);

  const authUser = getAuthUser();
  const hasToken = isAuthenticated();
  const hasPendingCloudSync =
    hasToken &&
    state.favoriteIds.length > 0 &&
    localStorage.getItem(SYNCED_USER_STORAGE_KEY) !== authUser?.userId;

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state.favoriteIds));
    if (state.favoriteIds.length > 0) {
      localStorage.removeItem(SYNCED_USER_STORAGE_KEY);
    }
  }, [state.favoriteIds]);

  const syncFavoritesToCloud = async (signal?: AbortSignal) => {
    const token = getAuthToken();
    if (!token) return;

    try {
      dispatch({ type: 'START_SYNC' });
      await apiClient.post('/favorites/sync', { trailIds: state.favoriteIds }, { signal });

      if (authUser?.userId) {
        localStorage.setItem(SYNCED_USER_STORAGE_KEY, authUser.userId);
      }
      dispatch({ type: 'SYNC_SUCCESS' });
    } catch (error) {
      if (axios.isCancel(error)) return;
      console.error('Favorites sync failed:', error);
      dispatch({ type: 'SYNC_ERROR', error: 'Неуспешна синхронизация на любими.' });
    }
  };

  useEffect(() => {
    if (!hasToken) return;

    if (isFirstRenderRef.current) {
      isFirstRenderRef.current = false;
      return;
    }

    // Cancel previous sync
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }

    const controller = new AbortController();
    abortControllerRef.current = controller;

    void syncFavoritesToCloud(controller.signal);

    return () => {
      controller.abort();
    };
  }, [state.favoriteIds, hasToken]);

  const favoriteSet = useMemo(() => new Set(state.favoriteIds), [state.favoriteIds]);
  const isFavorite = (trailId: number) => favoriteSet.has(trailId);

  const toggleFavorite = (trailId: number) => {
    dispatch({ type: 'TOGGLE_FAVORITE', trailId });
  };

  const clearFavorites = () => {
    dispatch({ type: 'CLEAR_FAVORITES' });
    localStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem(SYNCED_USER_STORAGE_KEY);
  };

  return {
    ...state,
    isFavorite,
    toggleFavorite,
    hasToken,
    hasPendingCloudSync,
    syncFavoritesToCloud,
    clearFavorites,
  };
}