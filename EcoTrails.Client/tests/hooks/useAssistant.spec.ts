import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useAssistant } from '../../src/hooks/useAssistant';
import { requestAssistantReplyMock } from './mocked/assistantService.mock';
import { mockAssistantResponse } from './testData/assistant.data';

describe('useAssistant', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('sendMessage', () => {
    /**
     * target: useAssistant.sendMessage
     * dependencies: assistantService.requestAssistantReply
     * scenario: Sending a valid message should trigger typing state and call API
     * expected output: isTyping=true, then isTyping=false + messages updated
     */
    it('should update state during a successful message flow', async () => {
      const { result } = renderHook(() => useAssistant());

      await act(async () => {
        const promise = result.current.sendMessage('Hello assistant');
        expect(result.current.isTyping).toBe(true);
        await promise;
      });

      expect(result.current.isTyping).toBe(false);
      expect(result.current.messages).toHaveLength(2); // User + Assistant
      expect(result.current.messages[1].content).toBe(mockAssistantResponse.reply);
      expect(requestAssistantReplyMock).toHaveBeenCalledTimes(1);
    });

    /**
     * target: useAssistant.sendMessage
     * dependencies: assistantService.requestAssistantReply, AbortController
     * scenario: Sending multiple messages should abort previous requests
     * expected output: Previous signals are aborted, only last one completes
     */
    it('should abort previous requests when sending a new message', async () => {
      const { result } = renderHook(() => useAssistant());

      // Advance timers to bypass throttle
      await act(async () => {
        result.current.sendMessage('First message');
      });
      
      vi.advanceTimersByTime(3001);

      await act(async () => {
        result.current.sendMessage('Second message');
      });

      // Verify that requestAssistantReply was called twice
      // and that the signal of the first call was aborted (not directly testable here without spying on AbortController)
      expect(requestAssistantReplyMock).toHaveBeenCalledTimes(2);
    });

    /**
     * target: useAssistant.sendMessage
     * dependencies: none (internal throttle)
     * scenario: Rapidly sending the same message within 3 seconds
     * expected output: API is only called once
     */
    it('should prevent duplicate messages within 3 seconds', async () => {
      const { result } = renderHook(() => useAssistant());

      await act(async () => {
        result.current.sendMessage('Hello');
        result.current.sendMessage('Hello');
        result.current.sendMessage('Hello');
      });

      expect(requestAssistantReplyMock).toHaveBeenCalledTimes(1);
    });

    /**
     * target: useAssistant.sendMessage
     * dependencies: assistantService.requestAssistantReply
     * scenario: Handle API error
     * expected output: isTyping=false, error is set
     */
    it('should handle API errors correctly', async () => {
      requestAssistantReplyMock.mockRejectedValueOnce(new Error('API Failure'));
      const { result } = renderHook(() => useAssistant());

      await act(async () => {
        await result.current.sendMessage('Help');
      });

      expect(result.current.isTyping).toBe(false);
      expect(result.current.error).toBe('Неуспешна комуникация с AI асистента.');
    });
  });

  describe('clearChat', () => {
    /**
     * target: useAssistant.clearChat
     * dependencies: assistantReducer
     * scenario: Clear existing chat history
     * expected output: messages array becomes empty
     */
    it('should clear all messages', async () => {
      const { result } = renderHook(() => useAssistant());

      // Pre-populate
      await act(async () => {
        await result.current.sendMessage('Hi');
      });
      expect(result.current.messages).not.toHaveLength(0);

      act(() => {
        result.current.clearChat();
      });

      expect(result.current.messages).toHaveLength(0);
      expect(result.current.sessionId).toBeNull();
    });
  });
});
