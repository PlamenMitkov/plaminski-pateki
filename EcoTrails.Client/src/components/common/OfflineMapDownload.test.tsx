import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { OfflineMapDownload } from './OfflineMapDownload';
import * as useOfflineMapModule from '../../hooks/useOfflineMap';
import type { LatLngBounds } from 'leaflet';

describe('OfflineMapDownload', () => {
  const mockBounds = {} as unknown as LatLngBounds;

  it('renders download button and triggers download', () => {
    const downloadMapMock = vi.fn();
    vi.spyOn(useOfflineMapModule, 'useOfflineMap').mockReturnValue({
      isDownloading: false,
      progress: 0,
      error: null,
      downloadMap: downloadMapMock,
      cancelDownload: vi.fn(),
    });

    render(<OfflineMapDownload trailId={1} mapBounds={mockBounds} />);
    const btn = screen.getByText('Download Offline Map');
    fireEvent.click(btn);
    expect(downloadMapMock).toHaveBeenCalled();
  });

  it('renders cancel button and progress bar when downloading', () => {
    const cancelDownloadMock = vi.fn();
    vi.spyOn(useOfflineMapModule, 'useOfflineMap').mockReturnValue({
      isDownloading: true,
      progress: 45,
      error: null,
      downloadMap: vi.fn(),
      cancelDownload: cancelDownloadMock,
    });

    const { container } = render(<OfflineMapDownload trailId={1} mapBounds={mockBounds} />);
    const btn = screen.getByText('Cancel Download');
    fireEvent.click(btn);
    expect(cancelDownloadMock).toHaveBeenCalled();
    expect(screen.getByText('45%')).toBeDefined();
    expect(container.querySelector('.offline-map-download__progress-bar')).toBeDefined();
  });
});
