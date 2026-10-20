import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from './features/auth/AuthContext';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="app">
      <header className="app__header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '32px' }}>
          <Link className="app__brand" to="/tickets">
            Northwind Support
          </Link>

          {user && (
            <nav className="app__nav" aria-label="Main">
              <NavLink to="/tickets">Tickets</NavLink>
              <NavLink to="/customers">Customers</NavLink>
            </nav>
          )}
        </div>

        <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '16px' }}>
          {user ? (
            <>
              <span style={{ fontSize: '0.85rem', color: 'var(--colour-muted)' }}>
                Signed in as <strong style={{ color: 'var(--colour-text)' }}>{user.fullName}</strong>
              </span>
              <button
                type="button"
                className="button button--ghost"
                style={{ padding: '4px 10px', fontSize: '0.85rem' }}
                onClick={handleLogout}
              >
                Sign out
              </button>
            </>
          ) : (
            <Link
              to="/login"
              className="button button--ghost"
              style={{ padding: '4px 10px', fontSize: '0.85rem' }}
            >
              Sign in
            </Link>
          )}
        </div>
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
