import { useState, useRef, useCallback } from 'react';
import type { LatLngBounds } from 'leaflet';

export interface UseOfflineMapResult {
  isDownloading: boolean;
  progress: number;
  downloadMap: () => Promise<void>;
  cancelDownload: () => void;
  error: string | null;
}

export function useOfflineMap(trailId: number, mapBounds: LatLngBounds | null): UseOfflineMapResult {
  const [isDownloading, setIsDownloading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  const downloadMap = useCallback(async () => {
    if (!mapBounds) {
      setError('Map bounds not available. Cannot download map data.');
      return;
    }

    setIsDownloading(true);
    setProgress(0);
    setError(null);

    const controller = new AbortController();
    abortControllerRef.current = controller;

    try {
      // Simulating fetching map tiles inside the bounding box
      const totalTiles = 20; 
      
      for (let i = 0; i < totalTiles; i++) {
        if (controller.signal.aborted) {
          throw new DOMException('Download cancelled', 'AbortError');
        }

        // Simulate network latency per tile
        await new Promise((resolve) => setTimeout(resolve, 150));
        
        setProgress(Math.round(((i + 1) / totalTiles) * 100));
      }
      
      // Simulate Cache API persisting
      if ('caches' in window) {
        const cache = await caches.open(`eco-trails-map-${trailId}`);
        await cache.put(
          new Request(`/api/maps/info?trailId=${trailId}`), 
          new Response(JSON.stringify({ cached: true, timestamp: Date.now() }))
        );
      }
    } catch (err: unknown) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        setError('Download was cancelled.');
      } else {
        setError('An error occurred during map download.');
      }
    } finally {
      setIsDownloading(false);
      abortControllerRef.current = null;
      if (!controller.signal.aborted) {
        setProgress(100);
      }
    }
  }, [trailId, mapBounds]);

  const cancelDownload = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
  }, []);

  return {
    isDownloading,
    progress,
    downloadMap,
    cancelDownload,
    error,
  };
}
