import type { LatLngBounds } from 'leaflet';
import { Download, XCircle } from 'lucide-react';
import { useOfflineMap } from '../../hooks/useOfflineMap';

export interface OfflineMapDownloadProps {
  trailId: number;
  mapBounds: LatLngBounds | null;
}

export function OfflineMapDownload({ trailId, mapBounds }: OfflineMapDownloadProps) {
  const { isDownloading, progress, downloadMap, cancelDownload, error } = useOfflineMap(trailId, mapBounds);

  return (
    <div
      className="offline-map-download"
      style={{
        padding: '16px', // Spacing L
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        backgroundColor: '#08142c', // Card Slate
        borderRadius: '8px',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '8px' }}>
        <button
          type="button"
          className="primary-btn offline-map-download__button"
          onClick={isDownloading ? cancelDownload : downloadMap}
          disabled={!mapBounds && !isDownloading}
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '8px',
            border: '1px solid #3b82f6', // primary-btn blue border (Tailwind blue-500 equivalent)
            backgroundColor: 'transparent',
            color: '#f8fbff', // Text White
            padding: '8px 16px',
            borderRadius: '6px',
            cursor: (!mapBounds && !isDownloading) ? 'not-allowed' : 'pointer',
            opacity: (!mapBounds && !isDownloading) ? 0.5 : 1,
          }}
        >
          {isDownloading ? (
            <>
              <XCircle size={16} color="#ef4444" />
              <span className="offline-map-download__action-text">Cancel Download</span>
            </>
          ) : (
            <>
              <Download size={16} color="#4ecca3" />
              <span className="offline-map-download__action-text">Download Offline Map</span>
            </>
          )}
        </button>
        {progress > 0 && <span className="offline-map-download__progress-text" style={{ color: '#f8fbff', fontWeight: 'bold' }}>{progress}%</span>}
      </div>

      {isDownloading && (
        <div
          className="offline-map-download__progress-bar"
          style={{
            width: '100%',
            height: '8px',
            backgroundColor: 'rgba(248, 251, 255, 0.1)', // semi-transparent wrapper over Card Slate
            borderRadius: '4px',
            overflow: 'hidden',
          }}
        >
          <div
            className="offline-map-download__progress-fill"
            style={{
              height: '100%',
              width: `${progress}%`,
              backgroundColor: '#4ecca3', // Eco-Green
              transition: 'width 0.2s linear',
            }}
          />
        </div>
      )}

      {error && (
        <p className="offline-map-download__error" style={{ color: '#ef4444', fontSize: '14px', margin: 0 }}>
          {error}
        </p>
      )}
    </div>
  );
}
