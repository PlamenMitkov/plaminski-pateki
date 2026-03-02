import { useCallback, useState } from 'react';
import {
  requestAssistantReply,
  type AssistantChatMessage,
  type AssistantKnowledgeChip,
  type AssistantQuickAction,
  type AssistantTrailContext,
} from '../services/assistantService';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

interface SendMessageOptions {
  filterSummary?: string;
  favoriteCount?: number;
  favoriteTrailIds?: number[];
  maxContextTrails?: number;
  onlyWithCoordinates?: boolean;
}

function createMessageId(prefix: string): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return `${prefix}-${crypto.randomUUID()}`;
  }

  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

function mapToAssistantHistory(messages: ChatMessage[]): AssistantChatMessage[] {
  return messages.map((message) => ({
    role: message.role,
    content: message.content,
  }));
}

export function useAssistant() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isTyping, setIsTyping] = useState(false);
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [usedTrails, setUsedTrails] = useState<AssistantTrailContext[]>([]);
  const [knowledgeChips, setKnowledgeChips] = useState<AssistantKnowledgeChip[]>([]);
  const [quickActions, setQuickActions] = useState<AssistantQuickAction[]>([]);

  const sendMessage = useCallback(
    async (content: string, options: SendMessageOptions = {}) => {
      const trimmed = content.trim();
      if (!trimmed) {
        return;
      }

      const userMessage: ChatMessage = {
        id: createMessageId('user'),
        role: 'user',
        content: trimmed,
        timestamp: new Date(),
      };

      const previousMessages = messages;
      setMessages((current) => [...current, userMessage]);
      setIsTyping(true);
      setError(null);

      try {
        const response = await requestAssistantReply({
          prompt: trimmed,
          sessionId: sessionId ?? undefined,
          history: mapToAssistantHistory(previousMessages),
          favoriteCount: options.favoriteCount ?? 0,
          favoriteTrailIds: options.favoriteTrailIds,
          filterSummary: options.filterSummary,
          maxContextTrails: options.maxContextTrails,
          onlyWithCoordinates: options.onlyWithCoordinates,
        });

        if (response.sessionId) {
          setSessionId(response.sessionId);
        }

        const assistantMessage: ChatMessage = {
          id: createMessageId('assistant'),
          role: 'assistant',
          content: response.reply,
          timestamp: new Date(),
        };

        setMessages((current) => [...current, assistantMessage]);
        setUsedTrails(response.usedTrails ?? []);
        setKnowledgeChips(response.knowledgeChips ?? []);
        setQuickActions(response.quickActions ?? []);
      } catch (requestError) {
        console.error('Грешка при комуникация с AI:', requestError);
        setError('Неуспешна комуникация с AI асистента.');
      } finally {
        setIsTyping(false);
      }
    },
    [messages, sessionId],
  );

  const clearChat = useCallback(() => {
    setMessages([]);
    setSessionId(null);
    setError(null);
    setUsedTrails([]);
    setKnowledgeChips([]);
    setQuickActions([]);
  }, []);

  return {
    messages,
    isTyping,
    sessionId,
    error,
    usedTrails,
    knowledgeChips,
    quickActions,
    sendMessage,
    clearChat,
    setSessionId,
    setMessages,
  };
}
