import { Sun, Cloud, CloudRain, Snowflake } from 'lucide-react';

export interface TrailWeatherWidgetProps {
  temperature: number;
  weatherIcon: 'sunny' | 'cloudy' | 'rainy' | 'snowy';
}

export function TrailWeatherWidget({ temperature, weatherIcon }: TrailWeatherWidgetProps) {
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
        top: '8px',
        right: '8px',
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        padding: '4px 8px',
        borderRadius: '8px',
        backgroundColor: 'rgba(8, 20, 44, 0.9)',
        backdropFilter: 'blur(5px)',
        color: '#f8fbff',
        fontSize: '14px',
        fontWeight: 'bold',
        zIndex: 10,
      }}
    >
      <span className="trail-weather-widget__temp">{temperature}°C</span>
      <div className="trail-weather-widget__icon">{renderIcon()}</div>
    </div>
  );
}
