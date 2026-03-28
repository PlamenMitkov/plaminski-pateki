import { Trash2 } from 'lucide-react';
import type { ChatMessage } from '../../hooks/useAssistant';
import type {
  AssistantKnowledgeChip,
  AssistantQuickAction,
  AssistantSessionResponse,
  AssistantTrailContext,
} from '../../services/assistantService';
import AdminActionButton from '../AdminActionButton';
import { AiFeedbackLoop } from './AiFeedbackLoop';

interface AssistantPanelProps {
  assistantPrompt: string;
  isTyping: boolean;
  isAdmin: boolean;
  isAssistantEnriching: boolean;
  authUserEmail: string | null;
  assistantUserSessions: AssistantSessionResponse[];
  assistantSessionId: string;
  assistantChips: AssistantKnowledgeChip[];
  pinnedAssistantActions: AssistantQuickAction[];
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
    onFeedback?: (messageId: string, isPositive: boolean) => void;
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
  pinnedAssistantActions,
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
        Използва текущите филтри, резултатите от страницата и любимите ти пътеки за персонализирани препоръки.
      </p>

      {/* Input Section */}
      <input
        value={assistantPrompt}
        onChange={(event) => handlers.onPromptChange(event.target.value)}
        onKeyDown={(event) => event.key === 'Enter' && (event.preventDefault(), handlers.onGenerateReply())}
        className="search-input assistant-input"
        placeholder="Напр. препоръчай ми кратка пътека около София"
      />

      <div className="assistant-actions">
        <button
          type="button"
          className="primary-btn"
          onClick={handlers.onGenerateReply}
          disabled={isTyping || !assistantPrompt.trim()}
        >
          {isTyping ? 'Генериране...' : 'Генерирай препоръка'}
        </button>
        <button type="button" className="secondary-btn" onClick={handlers.onStartNewSession} disabled={isTyping}>
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

      <SessionList
        authUserEmail={authUserEmail}
        sessions={assistantUserSessions}
        currentSessionId={assistantSessionId}
        onOpen={handlers.onOpenAssistantSession}
        onDelete={handlers.onRequestDeleteSession}
        formatMessageCount={formatMessageCount}
      />

      <KnowledgeChips chips={assistantChips} />

      <QuickActions actions={[...pinnedAssistantActions, ...assistantActions]} onAction={handlers.onQuickAction} />

      {assistantUsedTrails.length > 0 && (
        <p className="assistant-meta">Контекст: {formatTrailCount(assistantUsedTrails.length)} от базата данни.</p>
      )}
      {assistantSessionId && <p className="assistant-meta">Сесия: {assistantSessionId.slice(0, 12)}...</p>}

      <StatusSection
        notice={assistantAdminNotice}
        adminError={assistantAdminError}
        chatError={chatError}
        isTyping={isTyping}
      />

      <MessageThread messages={assistantMessages} onFeedback={handlers.onFeedback} />
    </div>
  );
}

// Atomic Pure Components

const SessionList = ({
  authUserEmail,
  sessions,
  currentSessionId,
  onOpen,
  onDelete,
  formatMessageCount,
}: {
  authUserEmail: string | null;
  sessions: AssistantSessionResponse[];
  currentSessionId: string;
  onOpen: (sid: string) => void;
  onDelete: (sid: string) => void;
  formatMessageCount: (c: number) => string;
}) => {
  if (!authUserEmail || sessions.length === 0) return null;

  return (
    <div className="assistant-inline-sessions">
      <p className="assistant-meta">Сесии в профила</p>
      <div className="assistant-session-list">
        {sessions.map((session) => (
          <div
            key={session.sessionId}
            className={`assistant-session-item ${currentSessionId === session.sessionId ? 'assistant-session-item-active' : ''}`}
          >
            <button type="button" className="assistant-session-open" onClick={() => onOpen(session.sessionId)}>
              <span>{session.title}</span>
              <small>{formatMessageCount(session.messageCount)}</small>
            </button>
            <button
              type="button"
              className="assistant-session-delete"
              onClick={() => onDelete(session.sessionId)}
              title="Изтрий сесия"
            >
              <Trash2 size={14} />
            </button>
          </div>
        ))}
      </div>
    </div>
  );
};

const KnowledgeChips = ({ chips }: { chips: AssistantKnowledgeChip[] }) => {
  if (chips.length === 0) return null;
  return (
    <div className="assistant-chips">
      {chips.map((chip, i) => (
        <span key={`${chip.label}-${i}`} className={`assistant-chip assistant-chip-${chip.type}`}>
          {chip.label}
        </span>
      ))}
    </div>
  );
};

const QuickActions = ({
  actions,
  onAction,
}: {
  actions: AssistantQuickAction[];
  onAction: (a: AssistantQuickAction) => void;
}) => {
  if (actions.length === 0) return null;
  return (
    <div className="assistant-quick-actions">
      {actions.map((action) => (
        <button key={`${action.id}-${action.value}`} type="button" className="secondary-btn" onClick={() => onAction(action)}>
          {action.label}
        </button>
      ))}
    </div>
  );
};

const StatusSection = ({ notice, adminError, chatError, isTyping }: { notice: string; adminError: string; chatError: string | null; isTyping: boolean }) => (
  <>
    {notice && <p className="status-text">{notice}</p>}
    {adminError && <p className="status-text error">{adminError}</p>}
    {chatError && <p className="status-text error">{chatError}</p>}
    {isTyping && <p className="status-text">Асистентът пише...</p>}
  </>
);

const MessageThread = ({
  messages,
  onFeedback,
}: {
  messages: ChatMessage[];
  onFeedback?: (messageId: string, isPositive: boolean) => void;
}) => {
  if (messages.length === 0) return null;
  return (
    <div className="assistant-thread">
      {messages.map((m) => (
        <div key={m.id} className="assistant-message-wrapper" style={{ display: 'flex', flexDirection: 'column' }}>
          <p className={`assistant-reply ${m.role === 'assistant' ? 'assistant-reply-ai' : 'assistant-reply-user'}`}>
            {m.content}
          </p>
          {m.role === 'assistant' && (
            <AiFeedbackLoop
              onFeedback={(isPositive) => onFeedback?.(m.id, isPositive)}
              isPending={false}
            />
          )}
        </div>
      ))}
    </div>
  );
};

export default AssistantPanel;
