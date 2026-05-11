import { useEffect, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { getCurrentUser } from '../../api/authApi';
import { getAccessToken } from '../../api/client';
import type { UserProfile } from '../../types/auth';

interface RequireAdminProps {
  children: JSX.Element;
}

export function RequireAdmin({ children }: RequireAdminProps) {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [isLoading, setIsLoading] = useState(Boolean(getAccessToken()));

  useEffect(() => {
    let requestVersion = 0;

    async function syncUser() {
      requestVersion += 1;
      const version = requestVersion;

      if (!getAccessToken()) {
        setUser(null);
        setIsLoading(false);
        return;
      }

      setIsLoading(true);

      try {
        const profile = await getCurrentUser();
        if (version === requestVersion) {
          setUser(profile);
        }
      } catch {
        if (version === requestVersion) {
          setUser(null);
        }
      } finally {
        if (version === requestVersion) {
          setIsLoading(false);
        }
      }
    }

    void syncUser();

    window.addEventListener('med-module:auth-changed', syncUser);
    window.addEventListener('med-module:unauthorized', syncUser);
    window.addEventListener('storage', syncUser);

    return () => {
      window.removeEventListener('med-module:auth-changed', syncUser);
      window.removeEventListener('med-module:unauthorized', syncUser);
      window.removeEventListener('storage', syncUser);
    };
  }, []);

  if (isLoading) {
    return null;
  }

  if (!user) {
    return <Navigate to="/auth" replace />;
  }

  if (user.role !== 'Admin') {
    return <Navigate to="/" replace />;
  }

  return children;
}
