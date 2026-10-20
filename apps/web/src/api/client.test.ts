import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AUTH_TOKEN_KEY, getStoredToken, request, setStoredToken } from './client';

describe('client API', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('stores and retrieves token from localStorage', () => {
    expect(getStoredToken()).toBeNull();

    setStoredToken('test-jwt-token');
    expect(localStorage.getItem(AUTH_TOKEN_KEY)).toBe('test-jwt-token');
    expect(getStoredToken()).toBe('test-jwt-token');

    setStoredToken(null);
    expect(getStoredToken()).toBeNull();
    expect(localStorage.getItem(AUTH_TOKEN_KEY)).toBeNull();
  });

  it('attaches Authorization: Bearer header when token exists', async () => {
    setStoredToken('valid-token-123');

    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ success: true }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await request('/tickets');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/tickets',
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer valid-token-123',
          'Content-Type': 'application/json',
        }),
      }),
    );
  });

  it('does not attach Authorization header when no token exists', async () => {
    setStoredToken(null);

    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ success: true }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await request('/tickets');

    const calledHeaders = fetchMock.mock.calls[0][1].headers;
    expect(calledHeaders.Authorization).toBeUndefined();
  });

  it('clears token and fires unauthorized event on 401 response from protected endpoint', async () => {
    setStoredToken('expired-token');

    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      statusText: 'Unauthorized',
      json: () => Promise.resolve({ detail: 'Token has expired.' }),
    });
    vi.stubGlobal('fetch', fetchMock);

    const unauthorizedListener = vi.fn();
    window.addEventListener('northwind:unauthorized', unauthorizedListener);

    await expect(request('/tickets')).rejects.toThrow('Token has expired.');

    expect(getStoredToken()).toBeNull();
    expect(unauthorizedListener).toHaveBeenCalledTimes(1);

    window.removeEventListener('northwind:unauthorized', unauthorizedListener);
  });

  it('does not clear token or fire unauthorized event on 401 from /auth/login', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      statusText: 'Unauthorized',
      json: () => Promise.resolve({ detail: 'Invalid email or password.' }),
    });
    vi.stubGlobal('fetch', fetchMock);

    const unauthorizedListener = vi.fn();
    window.addEventListener('northwind:unauthorized', unauthorizedListener);

    await expect(request('/auth/login', { method: 'POST' })).rejects.toThrow('Invalid email or password.');

    expect(unauthorizedListener).not.toHaveBeenCalled();

    window.removeEventListener('northwind:unauthorized', unauthorizedListener);
  });
});
