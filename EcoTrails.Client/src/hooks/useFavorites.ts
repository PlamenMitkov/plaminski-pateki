import axios from 'axios';
import { useEffect, useMemo, useRef, useState } from 'react';
import { getAuthToken, getAuthUser, isAuthenticated } from '../services/authService';

const STORAGE_KEY = 'ecotrails:favorites';
const SYNCED_USER_STORAGE_KEY = 'ecotrails:favoritesSyncedUser';

function parseStoredIds(value: string | null): number[] {
  if (!value) {
    return [];
  }

  try {
    const parsed = JSON.parse(value);
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed
      .map((item) => Number(item))
      .filter((item) => Number.isInteger(item) && item > 0);
  } catch {
    return [];
  }
}

export function useFavorites() {
  const [favoriteIds, setFavoriteIds] = useState<number[]>(() =>
    parseStoredIds(localStorage.getItem(STORAGE_KEY)),
  );
  const [isSyncing, setIsSyncing] = useState(false);
  const [lastSyncError, setLastSyncError] = useState('');
  const isFirstRenderRef = useRef(true);

  const authUser = getAuthUser();
  const hasToken = isAuthenticated();
  const hasPendingCloudSync =
    hasToken &&
    favoriteIds.length > 0 &&
    localStorage.getItem(SYNCED_USER_STORAGE_KEY) !== authUser?.userId;

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(favoriteIds));
    if (favoriteIds.length > 0) {
      localStorage.removeItem(SYNCED_USER_STORAGE_KEY);
    }
  }, [favoriteIds]);

  const syncFavoritesToCloud = async () => {
    const token = getAuthToken();
    if (!token) {
      return;
    }

    try {
      setIsSyncing(true);
      setLastSyncError('');

      await axios.post(
        'http://127.0.0.1:5218/api/favorites/sync',
        { trailIds: favoriteIds },
        { headers: { Authorization: `Bearer ${token}` } },
      );

      if (authUser?.userId) {
        localStorage.setItem(SYNCED_USER_STORAGE_KEY, authUser.userId);
      }
    } catch (error) {
      console.error('Грешка при синхронизация на любими:', error);
      setLastSyncError('Неуспешна синхронизация на любими.');
    } finally {
      setIsSyncing(false);
    }
  };

  useEffect(() => {
    if (!hasToken) {
      return;
    }

    if (isFirstRenderRef.current) {
      isFirstRenderRef.current = false;
      return;
    }

    void syncFavoritesToCloud();
  }, [favoriteIds, hasToken]);

  const favoriteSet = useMemo(() => new Set(favoriteIds), [favoriteIds]);

  const isFavorite = (trailId: number) => favoriteSet.has(trailId);

  const addFavorite = (trailId: number) => {
    setFavoriteIds((current) => (current.includes(trailId) ? current : [...current, trailId]));
  };

  const removeFavorite = (trailId: number) => {
    setFavoriteIds((current) => current.filter((item) => item !== trailId));
  };

  const toggleFavorite = (trailId: number) => {
    setFavoriteIds((current) =>
      current.includes(trailId) ? current.filter((item) => item !== trailId) : [...current, trailId],
    );
  };

  const clearFavorites = () => {
    setFavoriteIds([]);
    localStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem(SYNCED_USER_STORAGE_KEY);
    setLastSyncError('');
  };

  return {
    favoriteIds,
    isFavorite,
    addFavorite,
    removeFavorite,
    toggleFavorite,
    hasToken,
    hasPendingCloudSync,
    syncFavoritesToCloud,
    isSyncing,
    lastSyncError,
    clearFavorites,
  };
}