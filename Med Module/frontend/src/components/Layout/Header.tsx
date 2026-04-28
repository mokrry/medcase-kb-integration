import { NavLink } from 'react-router-dom';

export function Header() {
  return (
    <header className="header">
      <div className="container header__inner">
        <div className="header__branding">
          <h1 className="header__title">Med Module</h1>
          <p className="header__subtitle">Интеграция историй болезни с базой знаний интеллектуальной системы постановки диагноза</p>
        </div>

        <nav className="nav">
          <NavLink to="/" className="nav__link">
            Главная
          </NavLink>
          <NavLink to="/extract" className="nav__link">
            Извлечение
          </NavLink>
          <NavLink to="/reliability" className="nav__link">
            Проверка
          </NavLink>
        </nav>
      </div>
    </header>
  );
}
