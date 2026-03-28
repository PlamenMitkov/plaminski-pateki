import { vi } from 'vitest';
import { mockAssistantResponse } from '../testData/assistant.data';

/**
 * Mock implementation of requestAssistantReply
 */
export const requestAssistantReplyMock = vi.fn().mockResolvedValue(mockAssistantResponse);

/**
 * Global mock for the assistant service
 */
vi.mock('../../../src/services/assistantService', () => ({
  requestAssistantReply: requestAssistantReplyMock,
}));
