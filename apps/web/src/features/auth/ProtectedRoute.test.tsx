import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { setStoredToken } from '../../api/client';
import { AuthProvider } from './AuthContext';
import { ProtectedRoute } from './ProtectedRoute';

vi.mock('../../api/auth', () => ({
  authApi: {
    login: vi.fn(),
    getMe: vi.fn(),
  },
}));

function renderRoutes(initialEntries = ['/tickets']) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<div>Login Page Mock</div>} />
          <Route element={<ProtectedRoute />}>
            <Route path="/tickets" element={<div>Protected Tickets Content</div>} />
          </Route>
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('ProtectedRoute', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  it('redirects unauthenticated visitor to /login', async () => {
    setStoredToken(null);

    renderRoutes(['/tickets']);

    await waitFor(() => {
      expect(screen.getByText('Login Page Mock')).toBeInTheDocument();
    });
    expect(screen.queryByText('Protected Tickets Content')).not.toBeInTheDocument();
  });

  it('allows access to protected route when authenticated', async () => {
    setStoredToken('mock-valid-token');
    vi.mocked(authApi.getMe).mockResolvedValueOnce({
      id: 1,
      fullName: 'Alex Turner',
      email: 'alex.turner@northwind-support.example',
    });

    renderRoutes(['/tickets']);

    await waitFor(() => {
      expect(screen.getByText('Protected Tickets Content')).toBeInTheDocument();
    });
    expect(screen.queryByText('Login Page Mock')).not.toBeInTheDocument();
  });
});
