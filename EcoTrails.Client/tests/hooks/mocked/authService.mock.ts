import { vi } from 'vitest';

/**
 * Mock implementation of auth functions
 */
export const isAuthenticatedMock = vi.fn().mockReturnValue(true);
export const getAuthTokenMock = vi.fn().mockReturnValue('mock-token');
export const getAuthUserMock = vi.fn().mockReturnValue({ userId: 'user-001', username: 'plamen' });

/**
 * Global mock for the auth service
 */
vi.mock('../../../src/services/authService', () => ({
  isAuthenticated: isAuthenticatedMock,
  getAuthToken: getAuthTokenMock,
  getAuthUser: getAuthUserMock,
}));
