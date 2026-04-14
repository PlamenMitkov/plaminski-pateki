import { Sun, Cloud, CloudRain, Snowflake } from 'lucide-react';

export interface TrailWeatherWidgetProps {
  temperature: number;
  weatherIcon: 'sunny' | 'cloudy' | 'rainy' | 'snowy';
  label?: string;
}

export function TrailWeatherWidget({ temperature, weatherIcon, label }: TrailWeatherWidgetProps) {
  const renderIcon = () => {
    switch (weatherIcon) {
      case 'sunny':
        return <Sun size={16} color="#f59e0b" />;
      case 'cloudy':
        return <Cloud size={16} color="#f8fbff" />;
      case 'rainy':
        return <CloudRain size={16} color="#f8fbff" />;
      case 'snowy':
        return <Snowflake size={16} color="#f8fbff" />;
      default:
        return <Sun size={16} color="#f8fbff" />;
    }
  };

  return (
    <div
      className="trail-weather-widget"
      style={{
        position: 'absolute',
        top: '10px',
        right: '10px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '6px',
        padding: '8px 10px',
        borderRadius: '10px',
        border: '1px solid rgba(78, 204, 163, 0.75)',
        backgroundColor: 'rgba(8, 20, 44, 0.95)',
        backdropFilter: 'blur(6px)',
        boxShadow: '0 8px 22px rgba(4, 10, 24, 0.45)',
        color: '#f8fbff',
        fontSize: '14px',
        fontWeight: 700,
        zIndex: 10,
        minWidth: '132px',
      }}
    >
      {label && (
        <span style={{ fontSize: '11px', opacity: 0.9, lineHeight: 1.2, textAlign: 'center' }}>
          {label}
        </span>
      )}
      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
        <span className="trail-weather-widget__temp">{temperature}°C</span>
        <div className="trail-weather-widget__icon">{renderIcon()}</div>
      </div>
    </div>
  );
}
