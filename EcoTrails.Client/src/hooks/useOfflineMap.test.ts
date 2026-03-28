import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useOfflineMap } from './useOfflineMap';
import type { LatLngBounds } from 'leaflet';

describe('useOfflineMap hook', () => {
  const mockBounds = {
    getSouthWest: () => ({ lat: 0, lng: 0 }),
    getNorthEast: () => ({ lat: 1, lng: 1 }),
  } as unknown as LatLngBounds;

  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('initializes with default state', () => {
    const { result } = renderHook(() => useOfflineMap(1, mockBounds));
    expect(result.current.isDownloading).toBe(false);
    expect(result.current.progress).toBe(0);
    expect(result.current.error).toBeNull();
  });

  it('sets error if mapBounds are null', async () => {
    const { result } = renderHook(() => useOfflineMap(1, null));
    await act(async () => {
      await result.current.downloadMap();
    });
    expect(result.current.error).toBe('Map bounds not available. Cannot download map data.');
  });
});
