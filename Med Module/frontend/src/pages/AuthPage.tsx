import { FormEvent, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getCurrentUser, login, logout, register } from '../api/authApi';
import { getAccessToken } from '../api/client';
import { useAppState } from '../context/AppStateContext';
import type { UserProfile } from '../types/auth';

export function AuthPage() {
  const navigate = useNavigate();
  const { authPageMode, setAuthPageMode, authPageEmail, setAuthPageEmail } = useAppState();
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [currentUser, setCurrentUser] = useState<UserProfile | null>(null);
  const [isLoadingProfile, setIsLoadingProfile] = useState(Boolean(getAccessToken()));

  useEffect(() => {
    if (!getAccessToken()) {
      setCurrentUser(null);
      setIsLoadingProfile(false);
      return;
    }

    let cancelled = false;

    async function loadCurrentUser() {
      setIsLoadingProfile(true);
      setError('');

      try {
        const user = await getCurrentUser();
        if (!cancelled) {
          setCurrentUser(user);
          setAuthPageEmail(user.email);
        }
      } catch (loadError) {
        if (!cancelled) {
          setCurrentUser(null);
          setError(loadError instanceof Error ? loadError.message : 'Не удалось загрузить данные аккаунта.');
        }
      } finally {
        if (!cancelled) {
          setIsLoadingProfile(false);
        }
      }
    }

    void loadCurrentUser();

    return () => {
      cancelled = true;
    };
  }, [setAuthPageEmail]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError('');
    setIsSubmitting(true);

    try {
      if (authPageMode === 'login') {
        await login({ email: authPageEmail, password });
      } else {
        await register({ email: authPageEmail, password });
      }

      const user = await getCurrentUser();
      setCurrentUser(user);
      setAuthPageEmail(user.email);
      setPassword('');
      navigate('/extract');
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : 'Не удалось выполнить запрос авторизации.');
    } finally {
      setIsSubmitting(false);
    }
  }

  function handleLogout() {
    logout();
    setCurrentUser(null);
    setPassword('');
    setError('');
    setAuthPageMode('login');
    navigate('/auth', { replace: true });
  }

  if (isLoadingProfile) {
    return (
      <section className="grid gap-24">
        <article className="card">
          <div className="card__body">
            <h2>Аккаунт</h2>
            <p className="muted">Загружаю данные текущей сессии...</p>
          </div>
        </article>
      </section>
    );
  }

  if (currentUser) {
    return (
      <section className="grid gap-24">
        <article className="card">
          <div className="card__body">
            <h2>Аккаунт</h2>
            <p className="muted">Сейчас вы вошли в систему под этим пользователем.</p>

            <div className="grid gap-16">
              <p>
                <strong>Email:</strong> {currentUser.email}
              </p>
              <p>
                <strong>Роль:</strong> {currentUser.role}
              </p>
            </div>

            <div className="button-row">
              <button className="button button--secondary" type="button" onClick={handleLogout}>
                Выйти
              </button>
            </div>
          </div>
        </article>
      </section>
    );
  }

  return (
    <section className="grid gap-24">
      <article className="card">
        <div className="card__body">
          <h2>{authPageMode === 'login' ? 'Вход' : 'Регистрация'}</h2>
          <p className="muted">
            Аккаунт нужен для привязки запросов к пользователю и просмотра собственной истории
            обработки.
          </p>

          <form className="form-grid" onSubmit={handleSubmit}>
            <label className="form-field">
              <span>Email</span>
              <input
                type="email"
                value={authPageEmail}
                autoComplete="email"
                onChange={(event) => setAuthPageEmail(event.target.value)}
                required
              />
            </label>

            <label className="form-field">
              <span>Пароль</span>
              <input
                type="password"
                value={password}
                autoComplete={authPageMode === 'login' ? 'current-password' : 'new-password'}
                onChange={(event) => setPassword(event.target.value)}
                minLength={8}
                required
              />
            </label>

            {error && <div className="error-message">{error}</div>}

            <div className="button-row">
              <button className="button button--primary" type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Отправка...' : authPageMode === 'login' ? 'Войти' : 'Создать аккаунт'}
              </button>
              <button
                className="button button--secondary"
                type="button"
                onClick={() => setAuthPageMode(authPageMode === 'login' ? 'register' : 'login')}
              >
                {authPageMode === 'login' ? 'Нужна регистрация' : 'Уже есть аккаунт'}
              </button>
            </div>
          </form>
        </div>
      </article>
    </section>
  );
}
