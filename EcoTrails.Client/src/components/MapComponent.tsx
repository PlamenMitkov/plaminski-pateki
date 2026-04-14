import { useEffect, useRef, type MutableRefObject } from 'react';
import { MapContainer, Marker, Popup, TileLayer, useMap, useMapEvents } from 'react-leaflet';
import MarkerClusterGroup from 'react-leaflet-cluster';
import L from 'leaflet';
import { Link } from 'react-router-dom';
import type { Trail } from '../types/trail';

interface Props {
  trails: Trail[];
  selectedTrailId?: number | null;
  onSelectTrail?: (trailId: number) => void;
  initialCenter?: [number, number];
  initialZoom?: number;
  onMapViewChange?: (center: [number, number], zoom: number) => void;
}

function MapViewSync({ onMapViewChange }: { onMapViewChange?: (center: [number, number], zoom: number) => void }) {
  useMapEvents({
    moveend(event) {
      if (!onMapViewChange) {
        return;
      }

      const center = event.target.getCenter();
      const zoom = event.target.getZoom();
      onMapViewChange([center.lat, center.lng], zoom);
    },
  });

  return null;
}

function SelectedTrailFocus({
  selectedTrailId,
  trails,
  markerRefs,
}: {
  selectedTrailId: number | null;
  trails: Trail[];
  markerRefs: MutableRefObject<Map<number, L.Marker>>;
}) {
  const map = useMap();

  useEffect(() => {
    if (!selectedTrailId) {
      return;
    }

    const selected = trails.find((trail) => trail.id === selectedTrailId);
    if (!selected || selected.latitude === null || selected.longitude === null) {
      return;
    }

    const targetZoom = Math.max(map.getZoom(), 12);
    map.flyTo([selected.latitude, selected.longitude], targetZoom, { duration: 0.7 });

    const marker = markerRefs.current.get(selectedTrailId);
    if (marker) {
      window.setTimeout(() => {
        marker.openPopup();
      }, 250);
    }
  }, [map, markerRefs, selectedTrailId, trails]);

  return null;
}

const createClusterCustomIcon = (cluster: any) => {
  const count = cluster.getChildCount();
  let size = 'small';
  if (count > 10) size = 'medium';
  if (count > 50) size = 'large';

  return L.divIcon({
    html: `<div class="cluster-icon ${size}"><span>${count}</span></div>`,
    className: 'custom-marker-cluster',
    iconSize: L.point(40, 40, true),
  });
};

function MapComponent({
  trails,
  selectedTrailId = null,
  onSelectTrail,
  initialCenter = [42.7, 25.3],
  initialZoom = 7,
  onMapViewChange,
}: Props) {
  const markerRefs = useRef<Map<number, L.Marker>>(new Map());

  const trailsWithCoordinates = trails.filter(
    (trail) => trail.latitude !== null && trail.longitude !== null,
  );

  return (
    <MapContainer
      center={initialCenter}
      zoom={initialZoom}
      style={{ height: '450px', width: '100%', borderRadius: '12px', marginBottom: '20px' }}
    >
      <MapViewSync onMapViewChange={onMapViewChange} />
      <SelectedTrailFocus
        selectedTrailId={selectedTrailId}
        trails={trailsWithCoordinates}
        markerRefs={markerRefs}
      />
      <TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
      <MarkerClusterGroup
        chunkedLoading
        spiderfyOnMaxZoom
        showCoverageOnHover={false}
        iconCreateFunction={createClusterCustomIcon}
      >
        {trailsWithCoordinates.map((trail) => (
          <Marker
            key={trail.id}
            position={[trail.latitude!, trail.longitude!]}
            ref={(marker) => {
              if (marker) {
                markerRefs.current.set(trail.id, marker);
                return;
              }

              markerRefs.current.delete(trail.id);
            }}
            eventHandlers={{
              click: () => onSelectTrail?.(trail.id),
            }}
          >
            <Popup>
              <strong>{trail.name}</strong>
              {selectedTrailId === trail.id && (
                <>
                  <br />
                  <em>Избрана пътека</em>
                </>
              )}
              <br />
              {trail.location}
              <br />
              <Link to={`/trail/${trail.id}`}>Виж детайли</Link>
            </Popup>
          </Marker>
        ))}
      </MarkerClusterGroup>
    </MapContainer>
  );
}

export default MapComponent;