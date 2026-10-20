import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Spinner } from '../../components/Spinner';
import { useAuth } from './AuthContext';

/**
 * Ensures the user has an active authenticated session.
 * Unauthenticated users are redirected to /login with their original target preserved in state.
 */
export function ProtectedRoute() {
  const { user, token, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: '48px' }}>
        <Spinner />
      </div>
    );
  }

  if (!token || !user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <Outlet />;
}
