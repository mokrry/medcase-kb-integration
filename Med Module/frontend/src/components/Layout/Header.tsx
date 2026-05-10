import { useEffect, useState } from 'react';
import { NavLink } from 'react-router-dom';
import { getAccessToken } from '../../api/client';

export function Header() {
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
          <NavLink to="/auth" className="nav__link">
            Аккаунт
          </NavLink>
        </nav>
      </div>
    </header>
  );
}
