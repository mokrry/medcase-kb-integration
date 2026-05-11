import { useEffect, useState } from 'react';
import { NavLink } from 'react-router-dom';
import { getCurrentUser } from '../../api/authApi';
import { getAccessToken } from '../../api/client';
import type { UserProfile } from '../../types/auth';

export function Header() {
  const [currentUser, setCurrentUser] = useState<UserProfile | null>(null);
  const isAuthenticated = Boolean(getAccessToken());
  const isAdmin = currentUser?.role === 'Admin';

  useEffect(() => {
    let requestVersion = 0;

    async function syncAuthState() {
      requestVersion += 1;
      const version = requestVersion;

      if (!getAccessToken()) {
        setCurrentUser(null);
        return;
      }

      try {
        const user = await getCurrentUser();
        if (version === requestVersion) {
          setCurrentUser(user);
        }
      } catch {
        if (version === requestVersion) {
          setCurrentUser(null);
        }
      }
    }

    void syncAuthState();

    window.addEventListener('med-module:auth-changed', syncAuthState);
    window.addEventListener('med-module:unauthorized', syncAuthState);
    window.addEventListener('storage', syncAuthState);

    return () => {
      window.removeEventListener('med-module:auth-changed', syncAuthState);
      window.removeEventListener('med-module:unauthorized', syncAuthState);
      window.removeEventListener('storage', syncAuthState);
    };
  }, []);

  return (
    <header className="header">
      <div className="container header__inner">
        <div className="header__branding">
          <h1 className="header__title">Med Module</h1>
          <p className="header__subtitle">
            Интеграция историй болезни с базой знаний интеллектуальной системы постановки диагноза
          </p>
        </div>

        <nav className="nav">
          <NavLink to="/" className="nav__link">
            Главная
          </NavLink>
          {isAuthenticated && (
            <>
              <NavLink to="/extract" className="nav__link">
                Извлечение
              </NavLink>
              <NavLink to="/requests" className="nav__link">
                Журнал
              </NavLink>
            </>
          )}
          {isAdmin && (
            <>
              <NavLink to="/knowledge-base" className="nav__link">
                База знаний
              </NavLink>
              <NavLink to="/integrations" className="nav__link">
                Интеграции
              </NavLink>
            </>
          )}
          <NavLink to="/auth" className="nav__link">
            Аккаунт
          </NavLink>
        </nav>
      </div>
    </header>
  );
}
