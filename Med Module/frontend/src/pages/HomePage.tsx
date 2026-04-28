import { Link } from 'react-router-dom';

export function HomePage() {
  return (
    <section className="grid gap-24">
      <article className="hero">
        <div className="hero__content">
          <h2>Модуль извлечения медицински значимой информации из историй болезни</h2>
          <p>
            Основной сценарий: загрузка XML или XLSX, ввод списка сущностей текстом, запуск обработки и переход к
            проверке надёжности без обязательных вызовов реальных нейросетевых API.
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
            <p>Загрузка файла, ввод сущностей текстом и структурированный результат по каждой сущности.</p>
          </div>
        </Link>

        <Link className="card card--interactive" to="/reliability">
          <div className="card__body">
            <h3>Проверка надёжности</h3>
            <p>Подтверждение по тексту, неподтверждённые сущности и ручная верификация.</p>
          </div>
        </Link>
      </div>
    </section>
  );
}
