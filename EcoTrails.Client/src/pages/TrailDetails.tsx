import { useMemo, useState } from 'react';
import { useEffect } from 'react';
import { ArrowLeft, Heart, Share2 } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { CircleMarker, MapContainer, Polyline, Popup, TileLayer } from 'react-leaflet';
import type { Trail } from '../types/trail';
import { useFavorites } from '../hooks/useFavorites';
import apiClient from '../services/apiClient';
import '../App.css';

interface TrailRouteResponse {
  startLatitude: number;
  startLongitude: number;
  endLatitude: number;
  endLongitude: number;
  isEstimatedEnd: boolean;
  isExternalRoute: boolean;
  coordinates: [number, number][];
}

function buildFallbackRoute(trail: Trail): TrailRouteResponse | null {
  if (trail.latitude === null || trail.longitude === null) {
    return null;
  }

  const seedRadians = ((trail.id % 360) * Math.PI) / 180;
  const distanceFactor = Math.min(Math.max(trail.durationInHours, 1), 6) * 0.01;
  const latOffset = Math.sin(seedRadians) * distanceFactor;
  const lngOffset = Math.cos(seedRadians) * distanceFactor;
  const endLatitude = Number((trail.latitude + latOffset).toFixed(6));
  const endLongitude = Number((trail.longitude + lngOffset).toFixed(6));

  return {
    startLatitude: trail.latitude,
    startLongitude: trail.longitude,
    endLatitude,
    endLongitude,
    isEstimatedEnd: true,
    isExternalRoute: false,
    coordinates: [
      [trail.latitude, trail.longitude],
      [endLatitude, endLongitude],
    ],
  };
}

function buildTrailSchemaOrg(trail: Trail) {
  const keywords = [trail.region, trail.location, `трудност ${trail.difficulty}/5`, 'екопътека']
    .filter(Boolean)
    .join(', ');

  return {
    '@context': 'https://schema.org',
    '@type': 'TouristAttraction',
    name: trail.name,
    description: trail.description,
    url: window.location.href,
    keywords,
    publicAccess: true,
    isAccessibleForFree: true,
    areaServed: trail.region || undefined,
    geo:
      trail.latitude !== null && trail.longitude !== null
        ? {
            '@type': 'GeoCoordinates',
            latitude: trail.latitude,
            longitude: trail.longitude,
          }
        : undefined,
    additionalProperty: [
      {
        '@type': 'PropertyValue',
        name: 'difficulty',
        value: `${trail.difficulty}/5`,
      },
      {
        '@type': 'PropertyValue',
        name: 'durationInHours',
        value: trail.durationInHours,
      },
      {
        '@type': 'PropertyValue',
        name: 'elevationGain',
        value: trail.elevationGain,
        unitCode: 'MTR',
      },
    ],
  };
}

function TrailDetails() {
  const { id } = useParams<{ id: string }>();
  const [trail, setTrail] = useState<Trail | null>(null);
  const [route, setRoute] = useState<TrailRouteResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [routeWarning, setRouteWarning] = useState('');
  const { isFavorite, toggleFavorite } = useFavorites();

  useEffect(() => {
    if (!id) {
      setError('Невалиден идентификатор на пътека.');
      return;
    }

    setIsLoading(true);
    setError('');

    apiClient
      .get(`/trails/${id}`)
      .then((response) => setTrail(response.data))
      .catch(() => setError('Пътеката не е намерена.'))
      .finally(() => setIsLoading(false));
  }, [id]);

  useEffect(() => {
    if (!trail) {
      return;
    }

    const fallback = buildFallbackRoute(trail);

    if (!fallback) {
      setRoute(null);
      return;
    }

    setRouteWarning('');

    apiClient
      .get<TrailRouteResponse>(`/trails/${trail.id}/route`)
      .then((response) => setRoute(response.data))
      .catch((requestError) => {
        console.error('Грешка при зареждане на маршрут:', requestError);
        setRoute(fallback);
        setRouteWarning('Маршрутът е приблизителен, защото външният routing API не отговори.');
      });
  }, [trail]);

  const mapCenter = useMemo(() => {
    if (route) {
      return [route.startLatitude, route.startLongitude] as [number, number];
    }

    if (
      trail?.latitude !== null &&
      trail?.latitude !== undefined &&
      trail?.longitude !== null &&
      trail?.longitude !== undefined
    ) {
      return [trail.latitude, trail.longitude] as [number, number];
    }

    return [42.7, 25.3] as [number, number];
  }, [trail, route]);

  const schemaOrgJson = useMemo(() => {
    if (!trail) {
      return '';
    }

    return JSON.stringify(buildTrailSchemaOrg(trail));
  }, [trail]);

  useEffect(() => {
    if (!schemaOrgJson) {
      return;
    }

    const scriptId = 'trail-schema-org-jsonld';
    let script = document.getElementById(scriptId) as HTMLScriptElement | null;
    if (!script) {
      script = document.createElement('script');
      script.id = scriptId;
      script.type = 'application/ld+json';
      document.head.appendChild(script);
    }

    script.text = schemaOrgJson;

    return () => {
      const current = document.getElementById(scriptId);
      if (current) {
        current.remove();
      }
    };
  }, [schemaOrgJson]);

  const handleShare = async () => {
    const shareUrl = window.location.href;

    if (navigator.share) {
      try {
        await navigator.share({
          title: trail?.name ?? 'Екопътека',
          text: trail?.description ?? 'Виж тази екопътека',
          url: shareUrl,
        });
      } catch {
      }
      return;
    }

    await navigator.clipboard.writeText(shareUrl);
    window.alert('Линкът е копиран.');
  };

  if (isLoading) {
    return <div className="app-container">Зареждане...</div>;
  }

  if (error || !trail) {
    return (
      <div className="app-container">
        <p className="status-text error">{error || 'Пътеката не е намерена.'}</p>
        <Link className="trail-link" to="/">
          Обратно към списъка
        </Link>
      </div>
    );
  }

  return (
    <div className="app-container trail-details-page">
      <div className="details-actions">
        <Link className="trail-link" to="/">
          <ArrowLeft size={16} />
          Назад
        </Link>
        <div className="details-actions-right">
          <button
            type="button"
            className={`secondary-btn favorite-btn ${isFavorite(trail.id) ? 'active-btn' : ''}`}
            onClick={() => toggleFavorite(trail.id)}
          >
            <Heart size={16} fill={isFavorite(trail.id) ? 'currentColor' : 'none'} />
            {isFavorite(trail.id) ? 'В любими' : 'Добави в любими'}
          </button>
          <button type="button" className="secondary-btn" onClick={handleShare}>
            <Share2 size={16} />
            Сподели
          </button>
        </div>
      </div>

      <h1 className="app-title">{trail.name}</h1>

      <MapContainer
        center={mapCenter}
        zoom={trail.latitude !== null && trail.longitude !== null ? 11 : 7}
        style={{ height: '420px', width: '100%', borderRadius: '12px', marginBottom: '20px' }}
      >
        <TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />

        {route && route.coordinates.length >= 2 && (
          <>
            <Polyline positions={route.coordinates} color="#2563eb" weight={4} />

            <CircleMarker
              center={[route.startLatitude, route.startLongitude]}
              radius={8}
              pathOptions={{ color: '#ffffff', fillColor: '#16a34a', fillOpacity: 1, weight: 2 }}
            >
              <Popup>
                <strong>Начало</strong>
                <br />
                {trail.name}
              </Popup>
            </CircleMarker>

            <CircleMarker
              center={[route.endLatitude, route.endLongitude]}
              radius={8}
              pathOptions={{ color: '#ffffff', fillColor: '#dc2626', fillOpacity: 1, weight: 2 }}
            >
              <Popup>
                <strong>Край</strong>
                <br />
                {route.isEstimatedEnd
                  ? 'Ориентировъчен (няма отделна крайна точка в източника)'
                  : 'Изчислен по маршрут'}
              </Popup>
            </CircleMarker>
          </>
        )}
      </MapContainer>

      {route && (
        <>
          <div className="route-legend">
            <span className="legend-item">
              <span className="legend-dot start" /> Начало
            </span>
            <span className="legend-item">
              <span className="legend-dot end" /> Край
            </span>
          </div>
          <p className="status-text">
            {route.isExternalRoute
              ? 'Линията е изчислена през OpenRouteService (foot-hiking).'
              : 'Линията е приблизителна, защото няма външен маршрут.'}
          </p>
        </>
      )}

      {routeWarning && <p className="status-text error">{routeWarning}</p>}

      <div className="trail-card">
        <p>
          <strong>Локация:</strong> {trail.location}
        </p>
        <p>
          <strong>Трудност:</strong> {trail.difficulty}/5
        </p>
        <p>
          <strong>Продължителност:</strong> {trail.durationInHours} ч.
        </p>
        <p>
          <strong>Денивелация:</strong> {trail.elevationGain} м
        </p>
        <p>{trail.description}</p>
      </div>
    </div>
  );
}

export default TrailDetails;
