import React, { createContext, useContext, useEffect, useState } from 'react';
import { authApi } from '../../api/auth';
import { getStoredToken, setStoredToken } from '../../api/client';
import type { CurrentUser, LoginPayload } from '../../types/api';

export interface AuthContextValue {
  user: CurrentUser | null;
  token: string | null;
  isLoading: boolean;
  login: (payload: LoginPayload) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(() => getStoredToken());
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  // Synchronize authentication status and current user on initial startup
  useEffect(() => {
    let isMounted = true;

    async function initializeAuth() {
      const stored = getStoredToken();
      if (!stored) {
        if (isMounted) {
          setToken(null);
          setUser(null);
          setIsLoading(false);
        }
        return;
      }

      try {
        const currentUser = await authApi.getMe();
        if (isMounted) {
          setUser(currentUser);
          setToken(stored);
        }
      } catch {
        if (isMounted) {
          setStoredToken(null);
          setToken(null);
          setUser(null);
        }
      } finally {
        if (isMounted) {
          setIsLoading(false);
        }
      }
    }

    void initializeAuth();

    function handleUnauthorized() {
      if (isMounted) {
        setToken(null);
        setUser(null);
      }
    }

    window.addEventListener('northwind:unauthorized', handleUnauthorized);

    return () => {
      isMounted = false;
      window.removeEventListener('northwind:unauthorized', handleUnauthorized);
    };
  }, []);

  const login = async (payload: LoginPayload): Promise<void> => {
    const result = await authApi.login(payload);
    setStoredToken(result.token);
    setToken(result.token);
    setUser(result.user);
  };

  const logout = (): void => {
    setStoredToken(null);
    setToken(null);
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, token, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
