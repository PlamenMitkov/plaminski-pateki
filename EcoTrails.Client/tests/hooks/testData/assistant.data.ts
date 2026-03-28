import type { AssistantChatResponse, AssistantChatMessage } from '../../../src/services/assistantService';
import type { ChatMessage } from '../../../src/hooks/useAssistant';

/**
 * Sample assistant chat response from API
 */
export const mockAssistantResponse: AssistantChatResponse = {
  sessionId: 'mock-session-id',
  reply: 'Hello! How can I help you today?',
  model: 'gpt-4o',
  provider: 'openai',
  usedTrails: [],
  suggestedAlternatives: [],
  suggestedAlternativeIds: [],
  knowledgeChips: [{ label: 'Verified', type: 'success' }],
  quickActions: [{ id: 'map', label: 'Show on Map', value: 'map_view' }],
};

/**
 * Sample user message
 */
export const mockUserMessage: ChatMessage = {
  id: 'user-123',
  role: 'user',
  content: 'Hello assistant',
  timestamp: new Date('2026-03-28T10:00:00Z'),
};

/**
 * Sample history snapshot
 */
export const mockHistorySnapshot: AssistantChatMessage[] = [
  { role: 'user', content: 'Hello assistant' },
];
