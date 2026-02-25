import { MapContainer, Marker, Popup, TileLayer } from 'react-leaflet';
import MarkerClusterGroup from 'react-leaflet-cluster';
import L from 'leaflet';
import { Link } from 'react-router-dom';
import type { Trail } from '../types/trail';

interface Props {
  trails: Trail[];
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

function MapComponent({ trails }: Props) {
  const trailsWithCoordinates = trails.filter(
    (trail) => trail.latitude !== null && trail.longitude !== null,
  );

  return (
    <MapContainer
      center={[42.7, 25.3]}
      zoom={7}
      style={{ height: '450px', width: '100%', borderRadius: '12px', marginBottom: '20px' }}
    >
      <TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
      <MarkerClusterGroup
        chunkedLoading
        spiderfyOnMaxZoom
        showCoverageOnHover={false}
        iconCreateFunction={createClusterCustomIcon}
      >
        {trailsWithCoordinates.map((trail) => (
          <Marker key={trail.id} position={[trail.latitude!, trail.longitude!]}>
            <Popup>
              <strong>{trail.name}</strong>
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