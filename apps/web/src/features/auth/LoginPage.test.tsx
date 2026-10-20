import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { ApiError } from '../../api/client';
import { AuthProvider } from './AuthContext';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({
  authApi: {
    login: vi.fn(),
    getMe: vi.fn(),
  },
}));

function renderLoginPage(initialEntries = ['/login']) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/tickets" element={<div>Tickets Destination Page</div>} />
          <Route path="/tickets/42" element={<div>Ticket 42 Target</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('LoginPage', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  it('renders login form with labelled accessible fields', () => {
    renderLoginPage();

    expect(screen.getByRole('heading', { name: /Sign In/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Password/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Sign In/i })).toBeInTheDocument();
    expect(screen.getByText(/alex.turner@northwind-support.example/i)).toBeInTheDocument();
  });

  it('performs client-side validation on empty submission and does not call API', async () => {
    renderLoginPage();

    const submitBtn = screen.getByRole('button', { name: /Sign In/i });
    fireEvent.click(submitBtn);

    expect(await screen.findByText('Email address is required.')).toBeInTheDocument();
    expect(await screen.findByText('Password is required.')).toBeInTheDocument();
    expect(authApi.login).not.toHaveBeenCalled();
  });

  it('validates invalid email format', async () => {
    renderLoginPage();

    const emailInput = screen.getByLabelText(/Email Address/i);
    const passwordInput = screen.getByLabelText(/Password/i);
    const submitBtn = screen.getByRole('button', { name: /Sign In/i });

    await userEvent.type(emailInput, 'not-an-email');
    await userEvent.type(passwordInput, 'somepassword');
    fireEvent.click(submitBtn);

    expect(await screen.findByText('Please enter a valid email address.')).toBeInTheDocument();
    expect(authApi.login).not.toHaveBeenCalled();
  });

  it('displays error alert when API rejects credentials with 401', async () => {
    vi.mocked(authApi.login).mockRejectedValueOnce(
      new ApiError(401, 'Invalid email or password.'),
    );

    renderLoginPage();

    const emailInput = screen.getByLabelText(/Email Address/i);
    const passwordInput = screen.getByLabelText(/Password/i);
    const submitBtn = screen.getByRole('button', { name: /Sign In/i });

    await userEvent.type(emailInput, 'alex.turner@northwind-support.example');
    await userEvent.type(passwordInput, 'WrongPassword!');
    fireEvent.click(submitBtn);

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(authApi.login).toHaveBeenCalledWith({
      email: 'alex.turner@northwind-support.example',
      password: 'WrongPassword!',
    });
  });

  it('successfully logs in and redirects to default /tickets page', async () => {
    vi.mocked(authApi.login).mockResolvedValueOnce({
      token: 'valid-jwt-token',
      expiresAtUtc: '2026-10-21T00:00:00Z',
      user: {
        id: 1,
        fullName: 'Alex Turner',
        email: 'alex.turner@northwind-support.example',
      },
    });

    renderLoginPage();

    const emailInput = screen.getByLabelText(/Email Address/i);
    const passwordInput = screen.getByLabelText(/Password/i);
    const submitBtn = screen.getByRole('button', { name: /Sign In/i });

    await userEvent.type(emailInput, 'alex.turner@northwind-support.example');
    await userEvent.type(passwordInput, 'Password123!');
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(screen.getByText('Tickets Destination Page')).toBeInTheDocument();
    });
  });

  it('preserves return destination from state after login', async () => {
    vi.mocked(authApi.login).mockResolvedValueOnce({
      token: 'valid-jwt-token',
      expiresAtUtc: '2026-10-21T00:00:00Z',
      user: {
        id: 1,
        fullName: 'Alex Turner',
        email: 'alex.turner@northwind-support.example',
      },
    });

    renderLoginPage([
      { pathname: '/login', state: { from: { pathname: '/tickets/42' } } } as unknown as string,
    ]);

    const emailInput = screen.getByLabelText(/Email Address/i);
    const passwordInput = screen.getByLabelText(/Password/i);
    const submitBtn = screen.getByRole('button', { name: /Sign In/i });

    await userEvent.type(emailInput, 'alex.turner@northwind-support.example');
    await userEvent.type(passwordInput, 'Password123!');
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(screen.getByText('Ticket 42 Target')).toBeInTheDocument();
    });
  });
});
