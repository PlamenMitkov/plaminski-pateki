import { useMemo, useState } from 'react';
import { useEffect } from 'react';
import axios from 'axios';
import { ArrowLeft, Heart, Share2 } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { MapContainer, Marker, Popup, TileLayer } from 'react-leaflet';
import type { Trail } from '../types/trail';
import { useFavorites } from '../hooks/useFavorites';
import '../App.css';

function TrailDetails() {
  const { id } = useParams<{ id: string }>();
  const [trail, setTrail] = useState<Trail | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const { isFavorite, toggleFavorite } = useFavorites();

  useEffect(() => {
    if (!id) {
      setError('Невалиден идентификатор на пътека.');
      return;
    }

    setIsLoading(true);
    setError('');

    axios
      .get(`http://127.0.0.1:5218/api/trails/${id}`)
      .then((response) => setTrail(response.data))
      .catch(() => setError('Пътеката не е намерена.'))
      .finally(() => setIsLoading(false));
  }, [id]);

  const mapCenter = useMemo(() => {
    if (trail?.latitude !== null && trail?.longitude !== null && trail?.latitude && trail?.longitude) {
      return [trail.latitude, trail.longitude] as [number, number];
    }

    return [42.7, 25.3] as [number, number];
  }, [trail]);

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
    <div className="app-container">
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
        zoom={trail.latitude && trail.longitude ? 11 : 7}
        style={{ height: '420px', width: '100%', borderRadius: '12px', marginBottom: '20px' }}
      >
        <TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
        {trail.latitude && trail.longitude && (
          <Marker position={[trail.latitude, trail.longitude]}>
            <Popup>
              <strong>{trail.name}</strong>
              <br />
              {trail.location}
            </Popup>
          </Marker>
        )}
      </MapContainer>

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