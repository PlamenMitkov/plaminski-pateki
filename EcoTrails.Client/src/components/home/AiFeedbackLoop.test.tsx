import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { AiFeedbackLoop } from './AiFeedbackLoop';

describe('AiFeedbackLoop', () => {
  it('calls onFeedback with true when thumbs up is clicked', () => {
    const handleFeedback = vi.fn();
    render(<AiFeedbackLoop onFeedback={handleFeedback} isPending={false} />);
    
    const upBtn = screen.getByTitle('Helpful');
    fireEvent.click(upBtn);
    expect(handleFeedback).toHaveBeenCalledWith(true);
  });

  it('calls onFeedback with false when thumbs down is clicked', () => {
    const handleFeedback = vi.fn();
    render(<AiFeedbackLoop onFeedback={handleFeedback} isPending={false} />);
    
    const downBtn = screen.getByTitle('Not Helpful');
    fireEvent.click(downBtn);
    expect(handleFeedback).toHaveBeenCalledWith(false);
  });

  it('disables buttons when isPending is true', () => {
    const { container } = render(<AiFeedbackLoop onFeedback={() => {}} isPending={true} />);
    const buttons = container.querySelectorAll('button');
    buttons.forEach((btn) => {
      expect(btn.disabled).toBe(true);
    });
    // Check if loader is rendered
    expect(container.querySelector('.ai-feedback-loop__loader')).toBeDefined();
  });
});
