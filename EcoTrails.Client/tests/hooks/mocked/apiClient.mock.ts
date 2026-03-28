import { vi } from 'vitest';

/**
 * Mock implementation of apiClient.post
 */
export const apiClientPostMock = vi.fn().mockResolvedValue({ data: { success: true } });

/**
 * Global mock for the api client
 */
vi.mock('../../../src/services/apiClient', () => ({
  default: {
    post: apiClientPostMock,
    isCancel: vi.fn((err: any) => err?.name === 'CanceledError' || err?.__CANCEL__),
  },
}));
