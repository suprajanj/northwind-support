import React, { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { toErrorMessage } from '../../api/client';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Field } from '../../components/Field';
import { useAuth } from './AuthContext';

interface FormErrors {
  email?: string;
  password?: string;
}

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, token, isLoading, login } = useAuth();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Determine redirection target from state or default to /tickets
  const fromState = (location.state as { from?: { pathname: string; search?: string } })?.from;
  const redirectTarget = fromState
    ? `${fromState.pathname}${fromState.search ?? ''}`
    : '/tickets';

  if (!isLoading && user && token) {
    return <Navigate to={redirectTarget} replace />;
  }

  const validate = (): boolean => {
    const nextErrors: FormErrors = {};

    const trimmedEmail = email.trim();
    if (!trimmedEmail) {
      nextErrors.email = 'Email address is required.';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      nextErrors.email = 'Please enter a valid email address.';
    }

    if (!password) {
      nextErrors.password = 'Password is required.';
    }

    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setServerError(null);

    if (!validate()) {
      return;
    }

    setIsSubmitting(true);
    try {
      await login({ email: email.trim(), password });
      navigate(redirectTarget, { replace: true });
    } catch (err) {
      setServerError(toErrorMessage(err, 'Failed to sign in. Please verify your credentials.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div style={{ maxWidth: '440px', margin: '48px auto' }}>
      <div className="card">
        <h1 style={{ marginBottom: '8px' }}>Sign In</h1>
        <p className="page-header__subtitle" style={{ marginBottom: '24px' }}>
          Enter your credentials to access the Northwind Support Desk.
        </p>

        {serverError && <ErrorMessage message={serverError} />}

        <form onSubmit={handleSubmit} noValidate>
          <Field id="login-email" label="Email Address" error={errors.email}>
            {(props) => (
              <input
                {...props}
                type="email"
                value={email}
                onChange={(e) => {
                  setEmail(e.target.value);
                  if (errors.email) setErrors((prev) => ({ ...prev, email: undefined }));
                }}
                disabled={isSubmitting}
                autoComplete="email"
                autoFocus
              />
            )}
          </Field>

          <Field id="login-password" label="Password" error={errors.password}>
            {(props) => (
              <input
                {...props}
                type="password"
                value={password}
                onChange={(e) => {
                  setPassword(e.target.value);
                  if (errors.password) setErrors((prev) => ({ ...prev, password: undefined }));
                }}
                disabled={isSubmitting}
                autoComplete="current-password"
              />
            )}
          </Field>

          <div style={{ marginTop: '20px' }}>
            <button
              type="submit"
              className="button button--primary"
              style={{ width: '100%' }}
              disabled={isSubmitting}
            >
              {isSubmitting ? 'Signing in…' : 'Sign In'}
            </button>
          </div>
        </form>
      </div>

      <div className="card notice" style={{ marginTop: '16px', fontSize: '0.85rem' }}>
        <strong>Development Credentials (Seeded):</strong>
        <p style={{ margin: '4px 0 0' }}>
          Email: <code>alex.turner@northwind-support.example</code>
          <br />
          Password: <code>Password123!</code>
        </p>
      </div>
    </div>
  );
}
