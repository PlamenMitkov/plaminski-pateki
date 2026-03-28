import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useFavorites } from '../../src/hooks/useFavorites';
import { apiClientPostMock } from './mocked/apiClient.mock';
import { isAuthenticatedMock, getAuthUserMock } from './mocked/authService.mock';

describe('useFavorites', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  describe('toggleFavorite', () => {
    /**
     * target: useFavorites.toggleFavorite
     * dependencies: favoritesReducer, localStorage
     * scenario: Add a new trail to favorites
     * expected output: favoriteIds includes the new trail, persisted to localStorage
     */
    it('should add a trail to favorites and persist it locally', async () => {
      const { result } = renderHook(() => useFavorites());

      act(() => {
        result.current.toggleFavorite(101);
      });

      expect(result.current.favoriteIds).toContain(101);
      expect(result.current.isFavorite(101)).toBe(true);
      expect(localStorage.getItem('ecotrails:favorites')).toContain('101');
    });

    /**
     * target: useFavorites.toggleFavorite
     * dependencies: favoritesReducer
     * scenario: Remove a trail from favorites
     * expected output: favoriteIds no longer includes the trail
     */
    it('should remove a trail if it already is in favorites', async () => {
      localStorage.setItem('ecotrails:favorites', JSON.stringify([101, 102]));
      const { result } = renderHook(() => useFavorites());

      act(() => {
        result.current.toggleFavorite(101);
      });

      expect(result.current.favoriteIds).not.toContain(101);
      expect(result.current.favoriteIds).toContain(102);
    });
  });

  describe('Cloud Sync', () => {
    /**
     * target: useFavorites (useEffect)
     * dependencies: apiClient.post, authService
     * scenario: Toggling a favorite triggers background sync if authenticated
     * expected output: isSyncing becomes true then false, apiClient called
     */
    it('should trigger cloud sync when a favorite is toggled (authenticated)', async () => {
      isAuthenticatedMock.mockReturnValue(true);
      getAuthUserMock.mockReturnValue({ userId: 'user123' });
      
      const { result } = renderHook(() => useFavorites());

      await act(async () => {
        result.current.toggleFavorite(50);
      });

      // Syc is triggered by useEffect on favoriteIds change
      expect(apiClientPostMock).toHaveBeenCalledWith(
        '/favorites/sync',
        { trailIds: [50] },
        expect.any(Object)
      );
    });

    /**
     * target: useFavorites.syncFavoritesToCloud
     * dependencies: apiClient.post
     * scenario: Handle sync failure
     * expected output: lastSyncError is set
     */
    it('should set an error state if sync fails', async () => {
      apiClientPostMock.mockRejectedValueOnce(new Error('Sync failure'));
      isAuthenticatedMock.mockReturnValue(true);

      const { result } = renderHook(() => useFavorites());

      await act(async () => {
        await result.current.syncFavoritesToCloud();
      });

      expect(result.current.lastSyncError).toBe('Неуспешна синхронизация на любими.');
      expect(result.current.isSyncing).toBe(false);
    });
  });

  describe('clearFavorites', () => {
    /**
     * target: useFavorites.clearFavorites
     * dependencies: favoritesReducer, localStorage
     * scenario: Reset and clear all favorites
     * expected output: favoriteIds is empty, localStorage keys removed
     */
    it('should clear all state and storage', async () => {
      localStorage.setItem('ecotrails:favorites', JSON.stringify([1, 2, 3]));
      const { result } = renderHook(() => useFavorites());

      act(() => {
        result.current.clearFavorites();
      });

      expect(result.current.favoriteIds).toHaveLength(0);
      expect(localStorage.getItem('ecotrails:favorites')).toBeNull();
      expect(result.current.lastSyncError).toBe('');
    });
  });
});
