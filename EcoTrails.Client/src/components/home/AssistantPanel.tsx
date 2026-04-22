import type { ChatMessage } from '../../hooks/useAssistant';
import type {
  AssistantKnowledgeChip,
  AssistantQuickAction,
  AssistantTrailContext,
} from '../../services/assistantService';
import AdminActionButton from '../AdminActionButton';
import { AiFeedbackLoop } from './AiFeedbackLoop';

const MESSAGE_LINK_REGEX = /(https?:\/\/[^\s]+|(?:\\\/|\/)\?tab=map[^\s]*|(?:\\\/|\/)trail(?:\\\/|\/)\d+(?:[/?#][^\s]*)?)/g;

function normalizeEscapedLink(value: string) {
  return value.replace(/\\\//g, '/');
}

function renderMessageContent(content: string) {
  const parts = content.split(MESSAGE_LINK_REGEX);
  return parts.map((part, index) => {
    const normalizedPart = normalizeEscapedLink(part);

    if (/^https?:\/\//.test(normalizedPart)) {
      return (
        <a
          key={`msg-link-${index}`}
          href={normalizedPart}
          target="_blank"
          rel="noreferrer"
          className="assistant-reply-link"
        >
          {normalizedPart}
        </a>
      );
    }

    const internalLinkMatch = normalizedPart.match(/^(\/\?tab=map[^\s]*|\/trail\/\d+(?:[/?#][^\s]*)?)([)\].,!?]*)$/);
    if (internalLinkMatch) {
      const href = internalLinkMatch[1];
      const suffix = internalLinkMatch[2] ?? '';

      return (
        <span key={`msg-map-wrapper-${index}`}>
          <a
            href={href}
            className="assistant-reply-link"
          >
            {href}
          </a>
          {suffix ? <span>{suffix}</span> : null}
        </span>
      );
    }

    return <span key={`msg-text-${index}`}>{normalizedPart}</span>;
  });
}

interface AssistantPanelProps {
  assistantPrompt: string;
  isTyping: boolean;
  isAdmin: boolean;
  isAssistantEnriching: boolean;
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
    onRunAdminEnrichment: () => void;
    onQuickAction: (action: AssistantQuickAction) => void;
    onFeedback?: (messageId: string, isPositive: boolean) => void;
  };
  formatTrailCount: (count: number) => string;
}

function AssistantPanel({
  assistantPrompt,
  isTyping,
  isAdmin,
  isAssistantEnriching,
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
        <AdminActionButton
          isAdmin={isAdmin}
          onClick={handlers.onRunAdminEnrichment}
          disabled={isAssistantEnriching || isTyping}
        >
          {isAssistantEnriching ? 'Обогатяване...' : 'Обогати AI данни (Admin)'}
        </AdminActionButton>
      </div>

      <KnowledgeChips chips={assistantChips} />

      <ToolStrip title="Предложения за тази сесия" actions={assistantActions} onAction={handlers.onQuickAction} />
      <ToolStrip title="Инструменти за време и подготовка" actions={pinnedAssistantActions} onAction={handlers.onQuickAction} />

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

const ToolStrip = ({
  title,
  actions,
  onAction,
}: {
  title: string;
  actions: AssistantQuickAction[];
  onAction: (a: AssistantQuickAction) => void;
}) => {
  if (actions.length === 0) return null;

  return (
    <div className="assistant-inline-sessions">
      <p className="assistant-meta">{title}</p>
      <div className="assistant-quick-actions">
        {actions.map((action, index) => (
          <button key={`${action.id}-${action.value}-${action.label}-${index}`} type="button" className="secondary-btn" onClick={() => onAction(action)}>
            {action.label}
          </button>
        ))}
      </div>
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
            {renderMessageContent(m.content)}
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
