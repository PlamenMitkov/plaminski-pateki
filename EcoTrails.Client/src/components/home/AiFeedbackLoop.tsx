import { ThumbsUp, ThumbsDown, Loader2 } from 'lucide-react';

export interface AiFeedbackLoopProps {
  onFeedback: (isPositive: boolean) => void;
  isPending: boolean;
}

export function AiFeedbackLoop({ onFeedback, isPending }: AiFeedbackLoopProps) {
  return (
    <div
      className="ai-feedback-loop"
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        marginTop: '8px',
      }}
    >
      <button
        type="button"
        className="ai-feedback-loop__button ai-feedback-loop__button--up round-button"
        onClick={() => onFeedback(true)}
        disabled={isPending}
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          width: '32px',
          height: '32px',
          borderRadius: '50%',
          border: '1px solid #4ecca3', // Eco-Green
          backgroundColor: 'transparent',
          cursor: isPending ? 'not-allowed' : 'pointer',
          opacity: isPending ? 0.5 : 1,
        }}
        title="Helpful"
      >
        <ThumbsUp size={16} color="#4ecca3" />
      </button>
      <button
        type="button"
        className="ai-feedback-loop__button ai-feedback-loop__button--down round-button"
        onClick={() => onFeedback(false)}
        disabled={isPending}
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          width: '32px',
          height: '32px',
          borderRadius: '50%',
          border: '1px solid #ef4444', // Red
          backgroundColor: 'transparent',
          cursor: isPending ? 'not-allowed' : 'pointer',
          opacity: isPending ? 0.5 : 1,
        }}
        title="Not Helpful"
      >
        <ThumbsDown size={16} color="#ef4444" />
      </button>
      {isPending && (
        <Loader2 className="ai-feedback-loop__loader" size={16} color="#f8fbff" style={{ animation: 'spin 1s linear infinite' }} />
      )}
    </div>
  );
}
