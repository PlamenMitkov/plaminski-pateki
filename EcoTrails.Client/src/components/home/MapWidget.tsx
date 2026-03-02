import MapComponent from '../MapComponent';
import type { Trail } from '../../types/trail';

interface MapWidgetProps {
  mapFilteredTrails: Trail[];
  mapTrailsToShow: Trail[];
  selectedMapTrailId: number | null;
  showOnlySelectedOnMap: boolean;
  copyStatus: string;
  handlers: {
    onSelectedTrailChange: (trailId: number | null) => void;
    onToggleOnlySelected: () => void;
    onClearSelected: () => void;
    onCopyCurrentViewLink: () => void;
    onSelectTrailFromMap: (trailId: number) => void;
  };
  mapCenter: [number, number];
  mapZoom: number;
  onMapViewChange: (center: [number, number], zoom: number) => void;
  formatTrailCount: (count: number) => string;
  formatRouteCount: (count: number) => string;
}

function MapWidget({
  mapFilteredTrails,
  mapTrailsToShow,
  selectedMapTrailId,
  showOnlySelectedOnMap,
  copyStatus,
  handlers,
  mapCenter,
  mapZoom,
  onMapViewChange,
  formatTrailCount,
  formatRouteCount,
}: MapWidgetProps) {
  return (
    <>
      <div className="map-tools">
        <select
          className="select-input map-select"
          value={selectedMapTrailId ?? ''}
          onChange={(event) => {
            const value = event.target.value;
            if (value === '') {
              handlers.onSelectedTrailChange(null);
              return;
            }

            handlers.onSelectedTrailChange(Number(value));
          }}
        >
          <option value="">Избери пътека на картата...</option>
          {mapFilteredTrails.map((trail) => (
            <option key={trail.id} value={trail.id}>
              {trail.name}
            </option>
          ))}
        </select>
        <button
          type="button"
          className={`secondary-btn ${showOnlySelectedOnMap ? 'active-btn' : ''}`}
          disabled={!selectedMapTrailId}
          onClick={handlers.onToggleOnlySelected}
        >
          Покажи само избраната
        </button>
        <button
          type="button"
          className="secondary-btn"
          disabled={!selectedMapTrailId}
          onClick={handlers.onClearSelected}
        >
          Изчисти избора
        </button>
        <button type="button" className="secondary-btn" onClick={handlers.onCopyCurrentViewLink}>
          Копирай линк към текущия изглед
        </button>
        <span className="map-counter">
          На картата: {formatTrailCount(mapTrailsToShow.length)} от общо {formatRouteCount(mapFilteredTrails.length)}
        </span>
        {copyStatus && <span className="map-copy-status">{copyStatus}</span>}
      </div>

      <MapComponent
        trails={mapTrailsToShow}
        selectedTrailId={selectedMapTrailId}
        onSelectTrail={handlers.onSelectTrailFromMap}
        initialCenter={mapCenter}
        initialZoom={mapZoom}
        onMapViewChange={onMapViewChange}
      />
    </>
  );
}

export default MapWidget;
