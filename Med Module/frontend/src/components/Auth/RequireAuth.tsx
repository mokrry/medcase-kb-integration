import { useEffect, useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { getAccessToken } from '../../api/client';

interface RequireAuthProps {
  children: JSX.Element;
}

export function RequireAuth({ children }: RequireAuthProps) {
  const location = useLocation();
  const [isAuthenticated, setIsAuthenticated] = useState(Boolean(getAccessToken()));

  useEffect(() => {
    function syncAuthState() {
      setIsAuthenticated(Boolean(getAccessToken()));
    }

    window.addEventListener('med-module:auth-changed', syncAuthState);
    window.addEventListener('med-module:unauthorized', syncAuthState);
    window.addEventListener('storage', syncAuthState);

    return () => {
      window.removeEventListener('med-module:auth-changed', syncAuthState);
      window.removeEventListener('med-module:unauthorized', syncAuthState);
      window.removeEventListener('storage', syncAuthState);
    };
  }, []);

  if (!isAuthenticated) {
    return <Navigate to="/auth" replace state={{ from: location.pathname }} />;
  }

  return children;
}
