import { Trash2 } from 'lucide-react';
import type { ChatMessage } from '../../hooks/useAssistant';
import type {
  AssistantKnowledgeChip,
  AssistantQuickAction,
  AssistantSessionResponse,
  AssistantTrailContext,
} from '../../services/assistantService';
import AdminActionButton from '../AdminActionButton';

interface AssistantPanelProps {
  assistantPrompt: string;
  isTyping: boolean;
  isAdmin: boolean;
  isAssistantEnriching: boolean;
  authUserEmail: string | null;
  assistantUserSessions: AssistantSessionResponse[];
  assistantSessionId: string;
  assistantChips: AssistantKnowledgeChip[];
  assistantActions: AssistantQuickAction[];
  assistantUsedTrails: AssistantTrailContext[];
  assistantMessages: ChatMessage[];
  assistantAdminNotice: string;
  assistantAdminError: string;
  chatError: string | null;
  handlers: {
    onPromptChange: (value: string) => void;
    onGenerateReply: () => void;
    onStartNewSession: () => void;
    onOpenMapTab: () => void;
    onOpenFavoritesTab: () => void;
    onRunAdminEnrichment: () => void;
    onOpenAssistantSession: (sessionId: string) => void;
    onRequestDeleteSession: (sessionId: string) => void;
    onQuickAction: (action: AssistantQuickAction) => void;
  };
  formatMessageCount: (count: number) => string;
  formatTrailCount: (count: number) => string;
}

function AssistantPanel({
  assistantPrompt,
  isTyping,
  isAdmin,
  isAssistantEnriching,
  authUserEmail,
  assistantUserSessions,
  assistantSessionId,
  assistantChips,
  assistantActions,
  assistantUsedTrails,
  assistantMessages,
  assistantAdminNotice,
  assistantAdminError,
  chatError,
  handlers,
  formatMessageCount,
  formatTrailCount,
}: AssistantPanelProps) {
  return (
    <div className="assistant-card">
      <h3>Планински асистент</h3>
      <p>
        Използва текущите филтри, резултатите от страницата и любимите ти пътеки, за да ти даде
        бърза препоръка.
      </p>
      <input
        value={assistantPrompt}
        onChange={(event) => handlers.onPromptChange(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            handlers.onGenerateReply();
          }
        }}
        className="search-input assistant-input"
        placeholder="Напр. препоръчай ми кратка пътека около София"
      />
      <div className="assistant-actions">
        <button
          type="button"
          className="primary-btn"
          onClick={handlers.onGenerateReply}
          disabled={isTyping || assistantPrompt.trim().length === 0}
        >
          {isTyping ? 'Генериране...' : 'Генерирай препоръка'}
        </button>
        <button
          type="button"
          className="secondary-btn"
          onClick={handlers.onStartNewSession}
          disabled={isTyping}
        >
          Нова сесия
        </button>
        <button type="button" className="secondary-btn" onClick={handlers.onOpenMapTab}>
          Към карта
        </button>
        <button type="button" className="secondary-btn" onClick={handlers.onOpenFavoritesTab}>
          Към любими
        </button>
        <AdminActionButton
          isAdmin={isAdmin}
          onClick={handlers.onRunAdminEnrichment}
          disabled={isAssistantEnriching || isTyping}
        >
          {isAssistantEnriching ? 'Обогатяване...' : 'Обогати AI данни (Admin)'}
        </AdminActionButton>
      </div>
      {authUserEmail && (
        <div className="assistant-inline-sessions">
          <p className="assistant-meta">Сесии в профила</p>
          <div className="assistant-session-list">
            {assistantUserSessions.map((session) => (
              <div
                key={`assistant-${session.sessionId}`}
                className={`assistant-session-item ${
                  assistantSessionId === session.sessionId ? 'assistant-session-item-active' : ''
                }`}
              >
                <button
                  type="button"
                  className="assistant-session-open"
                  onClick={() => handlers.onOpenAssistantSession(session.sessionId)}
                >
                  <span>{session.title}</span>
                  <small>{formatMessageCount(session.messageCount)}</small>
                </button>
                <button
                  type="button"
                  className="assistant-session-delete"
                  onClick={() => handlers.onRequestDeleteSession(session.sessionId)}
                  title="Изтрий сесия"
                >
                  <Trash2 size={14} />
                </button>
              </div>
            ))}
          </div>
        </div>
      )}

      {assistantChips.length > 0 && (
        <div className="assistant-chips">
          {assistantChips.map((chip, index) => (
            <span key={`${chip.label}-${index}`} className={`assistant-chip assistant-chip-${chip.type}`}>
              {chip.label}
            </span>
          ))}
        </div>
      )}

      {assistantActions.length > 0 && (
        <div className="assistant-quick-actions">
          {assistantActions.map((action) => (
            <button
              key={`${action.id}-${action.value}`}
              type="button"
              className="secondary-btn"
              onClick={() => handlers.onQuickAction(action)}
            >
              {action.label}
            </button>
          ))}
        </div>
      )}

      {assistantUsedTrails.length > 0 && (
        <p className="assistant-meta">Контекст: {formatTrailCount(assistantUsedTrails.length)} от базата данни.</p>
      )}
      {assistantSessionId && <p className="assistant-meta">Сесия: {assistantSessionId.slice(0, 12)}...</p>}

      {assistantAdminNotice && <p className="status-text">{assistantAdminNotice}</p>}
      {assistantAdminError && <p className="status-text error">{assistantAdminError}</p>}
      {chatError && <p className="status-text error">{chatError}</p>}
      {isTyping && <p className="status-text">Асистентът пише...</p>}
      {assistantMessages.length > 0 && (
        <div className="assistant-thread">
          {assistantMessages.map((message) => (
            <p
              key={message.id}
              className={`assistant-reply ${message.role === 'assistant' ? 'assistant-reply-ai' : 'assistant-reply-user'}`}
            >
              {message.content}
            </p>
          ))}
        </div>
      )}
    </div>
  );
}

export default AssistantPanel;
