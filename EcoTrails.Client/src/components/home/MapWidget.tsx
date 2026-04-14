import { useEffect, useMemo, useState } from 'react';
import { latLngBounds, type LatLngBounds } from 'leaflet';
import { Link } from 'react-router-dom';
import MapComponent from '../MapComponent';
import type { Trail } from '../../types/trail';
import { OfflineMapDownload } from '../common/OfflineMapDownload';
import { TrailWeatherWidget } from '../common/TrailWeatherWidget';

function buildRouteHints(trail: Trail): string[] {
  const hints: string[] = [];

  if (trail.difficulty >= 4) {
    hints.push('Труден маршрут: тръгни по-рано и планирай резерв от време.');
  } else if (trail.difficulty <= 2) {
    hints.push('Лек маршрут: подходящ за спокойно темпо и по-кратки почивки.');
  } else {
    hints.push('Средна трудност: поддържай равномерно темпо по изкачванията.');
  }

  if (trail.elevationGain >= 900) {
    hints.push('Голяма денивелация: носи допълнителна вода и следи за умора.');
  } else if (trail.elevationGain <= 250) {
    hints.push('Ниска денивелация: удобен маршрут за начинаещи.');
  }

  if (trail.durationInHours >= 6) {
    hints.push('Дълъг преход: вземи храна за деня и челник.');
  }

  if (trail.latitude !== null && trail.longitude !== null) {
    hints.push('Има GPS координати: може да използваш офлайн карта и GPX навигация.');
  } else {
    hints.push('Липсват GPS координати: ползвай детайлно описание на достъпа.');
  }

  return hints;
}

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
  const selectedTrail = useMemo(
    () => mapFilteredTrails.find((trail) => trail.id === selectedMapTrailId) ?? null,
    [mapFilteredTrails, selectedMapTrailId],
  );

  const selectedTrailBounds = useMemo<LatLngBounds | null>(() => {
    if (selectedTrail?.latitude === null || selectedTrail?.longitude === null) {
      return null;
    }

    if (!selectedTrail) {
      return null;
    }

    const padding = 0.03;
    const latitude = selectedTrail.latitude;
    const longitude = selectedTrail.longitude;
    return latLngBounds(
      [latitude - padding, longitude - padding],
      [latitude + padding, longitude + padding],
    );
  }, [selectedTrail]);

  const selectedTrailHints = useMemo(() => {
    if (!selectedTrail) {
      return [];
    }

    return buildRouteHints(selectedTrail);
  }, [selectedTrail]);

  const [weatherState, setWeatherState] = useState<{
    temperature: number;
    weatherIcon: 'sunny' | 'cloudy' | 'rainy' | 'snowy';
  } | null>(null);

  const selectedTrailWeatherLabel = useMemo(() => {
    if (!selectedTrail) {
      return 'Време за избраната пътека';
    }

    return `Време: ${selectedTrail.name}`;
  }, [selectedTrail]);

  useEffect(() => {
    if (selectedTrail?.latitude === null || selectedTrail?.longitude === null) {
      setWeatherState(null);
      return;
    }

    if (!selectedTrail) {
      setWeatherState(null);
      return;
    }

    const latitude = selectedTrail.latitude;
    const longitude = selectedTrail.longitude;

    const controller = new AbortController();

    const fetchWeather = async () => {
      try {
        const query = new URLSearchParams({
          latitude: String(latitude),
          longitude: String(longitude),
          current: 'temperature_2m,weather_code',
          timezone: 'auto',
        });

        const response = await fetch(`https://api.open-meteo.com/v1/forecast?${query.toString()}`, {
          signal: controller.signal,
        });

        if (!response.ok) {
          throw new Error(`Open-Meteo request failed (${response.status})`);
        }

        const payload = (await response.json()) as {
          current?: {
            temperature_2m?: number;
            weather_code?: number;
          };
        };

        const temperature = payload.current?.temperature_2m;
        const code = payload.current?.weather_code;

        if (typeof temperature !== 'number') {
          setWeatherState(null);
          return;
        }

        let weatherIcon: 'sunny' | 'cloudy' | 'rainy' | 'snowy' = 'cloudy';
        if (typeof code === 'number') {
          if (code === 0) {
            weatherIcon = 'sunny';
          } else if ([71, 73, 75, 77, 85, 86].includes(code)) {
            weatherIcon = 'snowy';
          } else if ([51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82, 95, 96, 99].includes(code)) {
            weatherIcon = 'rainy';
          }
        }

        setWeatherState({
          temperature: Math.round(temperature),
          weatherIcon,
        });
      } catch (error) {
        if (!controller.signal.aborted) {
          setWeatherState(null);
          console.warn('Неуспешно зареждане на текущо време за картата:', error);
        }
      }
    };

    void fetchWeather();

    return () => {
      controller.abort();
    };
  }, [selectedTrail]);

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

      <div className="map-widget-stage">
        {weatherState && (
          <TrailWeatherWidget
            temperature={weatherState.temperature}
            weatherIcon={weatherState.weatherIcon}
            label={selectedTrailWeatherLabel}
          />
        )}
        <MapComponent
          trails={mapTrailsToShow}
          selectedTrailId={selectedMapTrailId}
          onSelectTrail={handlers.onSelectTrailFromMap}
          initialCenter={mapCenter}
          initialZoom={mapZoom}
          onMapViewChange={onMapViewChange}
        />
      </div>

      {!selectedTrail && (
        <p className="status-text">
          Избери пътека от падащото меню или директно от картата, за да активираш офлайн карта и маршрутни насоки.
        </p>
      )}

      {selectedTrail && (
        <>
          <div className="trail-card" style={{ marginTop: '12px' }}>
            <p>
              <strong>Избрана пътека:</strong> {selectedTrail.name}
            </p>
            <p>
              <strong>Офлайн:</strong> Натисни бутона по-долу и изчакай 100%, за да имаш карта без интернет.
            </p>
            {weatherState && (
              <p>
                <strong>Текущо време:</strong> {weatherState.temperature}°C (за избраната екопътека)
              </p>
            )}
          </div>

          <OfflineMapDownload trailId={selectedTrail.id} mapBounds={selectedTrailBounds} />

          <div className="trail-card" style={{ marginTop: '12px' }}>
            <p>
              <strong>Маршрутни насоки за:</strong> {selectedTrail.name}
            </p>
            {selectedTrailHints.map((hint, index) => (
              <p key={`${selectedTrail.id}-hint-${index}`}>• {hint}</p>
            ))}
            <Link className="trail-link" to={`/trail/${selectedTrail.id}`}>
              Отвори детайли и GPX навигация
            </Link>
          </div>
        </>
      )}
    </>
  );
}

export default MapWidget;
