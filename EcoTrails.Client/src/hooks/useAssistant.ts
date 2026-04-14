import { useCallback, useReducer, useRef } from 'react';
import axios from 'axios';
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

interface AssistantState {
  messages: ChatMessage[];
  isTyping: boolean;
  sessionId: string | null;
  error: string | null;
  usedTrails: AssistantTrailContext[];
  knowledgeChips: AssistantKnowledgeChip[];
  quickActions: AssistantQuickAction[];
}

type AssistantAction =
  | { type: 'APPEND_USER_MESSAGE'; message: ChatMessage }
  | { type: 'START_TYPING' }
  | { type: 'SET_ASSISTANT_REPLY'; sessionId: string; message: ChatMessage; usedTrails: AssistantTrailContext[]; knowledgeChips: AssistantKnowledgeChip[]; quickActions: AssistantQuickAction[] }
  | { type: 'SET_ERROR'; error: string | null }
  | { type: 'CLEAR_CHAT' }
  | { type: 'SET_SESSION_ID'; sessionId: string | null }
  | { type: 'SET_MESSAGES'; messages: ChatMessage[] }
  | { type: 'SET_QUICK_ACTIONS'; quickActions: AssistantQuickAction[] };

const initialState: AssistantState = {
  messages: [],
  isTyping: false,
  sessionId: null,
  error: null,
  usedTrails: [],
  knowledgeChips: [],
  quickActions: [],
};

function assistantReducer(state: AssistantState, action: AssistantAction): AssistantState {
  switch (action.type) {
    case 'APPEND_USER_MESSAGE':
      return { ...state, messages: [...state.messages, action.message], isTyping: true, error: null };
    case 'START_TYPING':
      return { ...state, isTyping: true, error: null };
    case 'SET_ASSISTANT_REPLY':
      return {
        ...state,
        sessionId: action.sessionId,
        messages: [...state.messages, action.message],
        isTyping: false,
        usedTrails: action.usedTrails,
        knowledgeChips: action.knowledgeChips,
        quickActions: action.quickActions,
      };
    case 'SET_ERROR':
      return { ...state, error: action.error, isTyping: false };
    case 'CLEAR_CHAT':
      return initialState;
    case 'SET_SESSION_ID':
      return { ...state, sessionId: action.sessionId };
    case 'SET_MESSAGES':
      return { ...state, messages: action.messages };
    case 'SET_QUICK_ACTIONS':
      return { ...state, quickActions: action.quickActions };
    default:
      return state;
  }
}

function createMessageId(prefix: string): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return `${prefix}-${crypto.randomUUID()}`;
  }
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

function mapToAssistantHistory(messages: ChatMessage[]): AssistantChatMessage[] {
  return messages.map((m) => ({ role: m.role, content: m.content }));
}

export function useAssistant() {
  const [state, dispatch] = useReducer(assistantReducer, initialState);
  const lastSubmittedPromptRef = useRef<{ content: string; at: number } | null>(null);
  const sessionIdRef = useRef<string | null>(null);
  sessionIdRef.current = state.sessionId;

  const abortControllerRef = useRef<AbortController | null>(null);

  const sendMessage = useCallback(
    async (content: string, options: SendMessageOptions = {}) => {
      const trimmed = content.trim();
      if (!trimmed) return;

      const now = Date.now();
      const lastSubmitted = lastSubmittedPromptRef.current;
      if (lastSubmitted && lastSubmitted.content === trimmed && now - lastSubmitted.at < 3000) return;
      lastSubmittedPromptRef.current = { content: trimmed, at: now };

      // Cancel previous request if any
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      const controller = new AbortController();
      abortControllerRef.current = controller;

      const userMessage: ChatMessage = {
        id: createMessageId('user'),
        role: 'user',
        content: trimmed,
        timestamp: new Date(),
      };

      // We capture current messages for history before updating state
      const historySnapshot = mapToAssistantHistory(state.messages);

      dispatch({ type: 'APPEND_USER_MESSAGE', message: userMessage });

      try {
        const response = await requestAssistantReply({
          prompt: trimmed,
          sessionId: sessionIdRef.current ?? undefined,
          history: historySnapshot,
          favoriteCount: options.favoriteCount ?? 0,
          favoriteTrailIds: options.favoriteTrailIds,
          filterSummary: options.filterSummary,
          maxContextTrails: options.maxContextTrails,
          onlyWithCoordinates: options.onlyWithCoordinates,
          signal: controller.signal,
        });

        const assistantMessage: ChatMessage = {
          id: createMessageId('assistant'),
          role: 'assistant',
          content: response.reply,
          timestamp: new Date(),
        };

        dispatch({
          type: 'SET_ASSISTANT_REPLY',
          sessionId: response.sessionId,
          message: assistantMessage,
          usedTrails: response.usedTrails ?? [],
          knowledgeChips: response.knowledgeChips ?? [],
          quickActions: response.quickActions ?? [],
        });
      } catch (err) {
        if (axios.isCancel(err)) return;

        console.error('AI Error:', err);
        let errorMsg = 'Неуспешна комуникация с AI асистента.';
        if (axios.isAxiosError(err)) {
          const status = err.response?.status;
          if (status === 401) errorMsg = 'Сесията е изтекла. Моля, влез отново.';
          else if (status === 404) {
            errorMsg = 'AI услугата е недостъпна: проверете конфигурацията на модел/ключ в сървъра.';
          }
          else if (status === 503) {
            errorMsg = 'AI услугата е бавна или временно недостъпна. Опитай отново след няколко секунди.';
          }
          else if (status === 429) errorMsg = 'Прекалено много заявки. Опитай отново след минута.';
        }
        dispatch({ type: 'SET_ERROR', error: errorMsg });
      }
    },
    [state.messages],
  );

  const clearChat = useCallback(() => dispatch({ type: 'CLEAR_CHAT' }), []);
  const setSessionId = useCallback((sid: string | null) => dispatch({ type: 'SET_SESSION_ID', sessionId: sid }), []);
  const setMessages = useCallback((msgs: ChatMessage[]) => dispatch({ type: 'SET_MESSAGES', messages: msgs }), []);
  const setQuickActions = useCallback((acts: AssistantQuickAction[]) => dispatch({ type: 'SET_QUICK_ACTIONS', quickActions: acts }), []);

  return {
    ...state,
    sendMessage,
    clearChat,
    setSessionId,
    setMessages,
    setQuickActions,
  };
}
