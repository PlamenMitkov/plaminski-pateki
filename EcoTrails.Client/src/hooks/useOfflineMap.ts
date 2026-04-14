import { useState, useRef, useCallback } from 'react';
import type { LatLngBounds } from 'leaflet';

const TILE_CACHE_NAME = 'ecotrails-tiles-v1';
const OFFLINE_ZOOM_LEVELS = [11, 12, 13, 14];
const MAX_TILE_DOWNLOAD = 1200;

function long2tileX(longitude: number, zoom: number): number {
  return Math.floor(((longitude + 180) / 360) * Math.pow(2, zoom));
}

function lat2tileY(latitude: number, zoom: number): number {
  const latRad = (latitude * Math.PI) / 180;
  const n = Math.pow(2, zoom);
  const value = (1 - Math.log(Math.tan(latRad) + 1 / Math.cos(latRad)) / Math.PI) / 2;
  return Math.floor(value * n);
}

function buildTileUrls(bounds: LatLngBounds, zoomLevels: number[]): string[] {
  const north = Math.min(bounds.getNorth(), 85.0511);
  const south = Math.max(bounds.getSouth(), -85.0511);
  const west = bounds.getWest();
  const east = bounds.getEast();

  const urls: string[] = [];

  zoomLevels.forEach((zoom) => {
    const maxIndex = Math.pow(2, zoom) - 1;

    const xMin = Math.max(0, Math.min(maxIndex, long2tileX(west, zoom)));
    const xMax = Math.max(0, Math.min(maxIndex, long2tileX(east, zoom)));
    const yMin = Math.max(0, Math.min(maxIndex, lat2tileY(north, zoom)));
    const yMax = Math.max(0, Math.min(maxIndex, lat2tileY(south, zoom)));

    for (let x = Math.min(xMin, xMax); x <= Math.max(xMin, xMax); x++) {
      for (let y = Math.min(yMin, yMax); y <= Math.max(yMin, yMax); y++) {
        urls.push(`https://tile.openstreetmap.org/${zoom}/${x}/${y}.png`);
      }
    }
  });

  return Array.from(new Set(urls));
}

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
      setError('Липсват граници на картата и не може да стартира офлайн изтегляне.');
      return;
    }

    if (!('caches' in window)) {
      setError('Браузърът не поддържа Cache API за офлайн карта.');
      return;
    }

    setIsDownloading(true);
    setProgress(0);
    setError(null);

    const controller = new AbortController();
    abortControllerRef.current = controller;

    try {
      const tileUrls = buildTileUrls(mapBounds, OFFLINE_ZOOM_LEVELS);
      if (tileUrls.length === 0) {
        setError('Не успях да изчисля tile клетки за този изглед.');
        return;
      }

      if (tileUrls.length > MAX_TILE_DOWNLOAD) {
        setError(`Изгледът е твърде голям (${tileUrls.length} tiles). Увеличи zoom и пробвай отново.`);
        return;
      }

      const cache = await caches.open(TILE_CACHE_NAME);
      let failed = 0;

      for (let i = 0; i < tileUrls.length; i++) {
        if (controller.signal.aborted) {
          throw new DOMException('Download cancelled', 'AbortError');
        }

        const tileUrl = tileUrls[i];
        const request = new Request(tileUrl, { mode: 'cors' });
        const cached = await cache.match(request);

        if (!cached) {
          try {
            const response = await fetch(request, { signal: controller.signal, cache: 'no-store' });
            if (response.ok) {
              await cache.put(request, response.clone());
            } else {
              failed++;
            }
          } catch {
            failed++;
          }
        }

        setProgress(Math.round(((i + 1) / tileUrls.length) * 100));
      }

      if (failed > 0) {
        setError(`Картата е записана частично: ${failed} tiles не се изтеглиха.`);
      }
    } catch (err: unknown) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        setError('Изтеглянето е отменено.');
      } else {
        setError('Възникна грешка при изтегляне на офлайн картата.');
      }
    } finally {
      setIsDownloading(false);
      abortControllerRef.current = null;
      if (!controller.signal.aborted) {
        setProgress(100);
      }
    }
  }, [mapBounds, trailId]);

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
