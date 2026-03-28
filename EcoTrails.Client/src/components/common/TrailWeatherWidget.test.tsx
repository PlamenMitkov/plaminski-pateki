import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TrailWeatherWidget } from './TrailWeatherWidget';

describe('TrailWeatherWidget', () => {
  it('renders temperature correctly', () => {
    render(<TrailWeatherWidget temperature={24} weatherIcon="sunny" />);
    expect(screen.getByText('24°C')).toBeDefined();
  });

  it('applies correct class names for BEM convention', () => {
    const { container } = render(<TrailWeatherWidget temperature={10} weatherIcon="snowy" />);
    expect(container.querySelector('.trail-weather-widget')).toBeDefined();
    expect(container.querySelector('.trail-weather-widget__temp')).toBeDefined();
    expect(container.querySelector('.trail-weather-widget__icon')).toBeDefined();
  });
});
