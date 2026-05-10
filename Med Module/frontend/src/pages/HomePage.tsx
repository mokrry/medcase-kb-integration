import { Link } from 'react-router-dom';

export function HomePage() {
  return (
    <section className="grid gap-24">
      <article className="hero">
        <div className="hero__content">
          <h2>Модуль извлечения медицински значимой информации из историй болезни</h2>
          <p>
            Основной сценарий работы сосредоточен в разделе извлечения: пользователь вводит
            медицинский текст, получает симптомы с проверкой по исходному тексту, при необходимости
            вручную корректирует их и только после этого запрашивает диагноз.
          </p>

          <div className="hero__actions">
            <Link className="button button--primary" to="/extract">
              Открыть извлечение
            </Link>
          </div>
        </div>
      </article>

      <div className="quick-links-grid">
        <Link className="card card--interactive" to="/extract">
          <div className="card__body">
            <h3>Извлечение</h3>
            <p>
              Ввод медицинского текста, извлечение симптомов, подсветка подтверждений и ручная
              корректировка.
            </p>
          </div>
        </Link>

        <Link className="card card--interactive" to="/requests">
          <div className="card__body">
            <h3>Журнал</h3>
            <p>
              История извлечений и запросов диагноза с сохранением результатов для каждого
              пользователя.
            </p>
          </div>
        </Link>
      </div>
    </section>
  );
}
