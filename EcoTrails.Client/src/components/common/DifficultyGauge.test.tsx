import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DifficultyGauge } from './DifficultyGauge';

describe('DifficultyGauge', () => {
  it('renders correctly with given level', () => {
    render(<DifficultyGauge level={3} />);
    expect(screen.getByText('3/5')).toBeDefined();
    expect(screen.getByTitle('Difficulty: 3/5')).toBeDefined();
  });

  it('renders all 5 dots with active and inactive modifiers', () => {
    const { container } = render(<DifficultyGauge level={2} />);
    const activeDots = container.querySelectorAll('.difficulty-gauge__dot--active');
    const inactiveDots = container.querySelectorAll('.difficulty-gauge__dot--inactive');
    
    expect(activeDots.length).toBe(2);
    expect(inactiveDots.length).toBe(3);
  });
});
