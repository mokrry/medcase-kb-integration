# Med Module

Веб-приложение для интеграции медицинского текста с базой знаний интеллектуальной системы постановки диагноза.

Пользователь вводит жалобы или фрагмент истории болезни. Система извлекает симптомы с помощью LLM, показывает подтверждающие фрагменты текста, позволяет врачу вручную скорректировать список симптомов и затем отправляет итоговый набор признаков в solver базы знаний. Результат отображается как список возможных диагнозов.

Публичный демо-стенд:

```text
https://med-module.ru
```

## Текущий Статус

Готовый MVP:

- авторизация пользователей через JWT;
- PostgreSQL для пользователей и журнала запросов;
- извлечение симптомов через Gemini и ChatGPT;
- подготовка медицинского текста через Gemini;
- голосование между LLM-ответами;
- уточнение спорных симптомов;
- подсветка подтверждающих фрагментов текста;
- ручное удаление и добавление симптомов из whitelist;
- отдельная кнопка определения диагноза;
- интеграция с удаленным solver базы знаний;
- журнал запросов пользователя;
- административные страницы диагностики;
- production-деплой через Docker Compose;
- HTTPS на `med-module.ru`;
- автодеплой через GitHub Actions при push в `main`.

## Основной Сценарий

1. Пользователь входит в аккаунт.
2. На странице `Извлечение` вводит медицинский текст.
3. Backend отправляет текст в Gemini для нормализации.
4. Подготовленный текст отправляется в Gemini и ChatGPT для извлечения симптомов.
5. Программа сравнивает JSON-ответы моделей.
6. Спорные симптомы проверяются дополнительным запросом к Gemini.
7. Пользователь видит итоговые симптомы, статусы проверки и подтверждающие фрагменты текста.
8. Пользователь может удалить симптом или добавить новый из whitelist.
9. После нажатия `Узнать диагноз` backend маппит симптомы в `label/value` payload базы знаний.
10. Payload отправляется в solver.
11. Пользователь получает возможные диагнозы и подтверждающие признаки.
12. Запросы сохраняются в журнале.

## Архитектура

```text
frontend React/Vite
  -> ASP.NET Core Web API
    -> PostgreSQL
    -> Gemini / ChatGPT / GigaChat config
    -> Excel knowledge base file
    -> external ai-hippocrates solver
```

Компоненты:

- `frontend/` - пользовательский интерфейс на React, TypeScript, Vite.
- `backend/` - ASP.NET Core Web API на .NET 8.
- `backend/Data/База_знаний_v24.5.xlsx` - файл базы знаний.
- PostgreSQL - пользователи, роли, история запросов.
- Solver - удаленный endpoint базы знаний.
- Docker Compose - production-запуск frontend, backend и PostgreSQL.

## Технологии

Backend:

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- ClosedXML
- JWT Bearer authentication
- Swagger

Frontend:

- React 18
- TypeScript
- Vite
- React Router

Infrastructure:

- Docker
- Docker Compose
- Nginx
- Certbot / Let's Encrypt
- GitHub Actions

## Роли

`User`:

- ввод медицинского текста;
- извлечение симптомов;
- ручная корректировка симптомов;
- запрос диагноза;
- просмотр своего журнала.

`Admin`:

- все возможности пользователя;
- страница `База знаний`;
- страница `Интеграции`;
- диагностика состояния backend, PostgreSQL, solver и LLM-провайдеров;
- просмотр технических сведений без отображения API-ключей.

Админ создается при старте backend из настроек:

```text
SeedAdmin:Email
SeedAdmin:Password
```

## Локальный Запуск

### 1. PostgreSQL

Из папки `backend`:

```powershell
docker compose -f docker-compose.postgres.yml up -d
```

Применить миграции:

```powershell
dotnet ef database update
```

### 2. Backend

Из папки `backend`:

```powershell
dotnet restore
dotnet run
```

Локальные адреса:

```text
http://localhost:5274
https://localhost:7274
```

Swagger:

```text
https://localhost:7274/swagger
```

### 3. Frontend

Из папки `frontend`:

```powershell
npm install
npm run dev
```

Frontend:

```text
http://localhost:5173
```

Переменная для локального frontend:

```env
VITE_API_BASE_URL=http://localhost:5274/api
```

## Конфигурация

Локальные секреты не должны храниться в git.

Backend поддерживает настройки через:

- `appsettings.json`;
- `appsettings.Development.json`;
- .NET user-secrets;
- environment variables в Docker.

Шаблоны:

- `backend/appsettings.example.json`
- `frontend/.env.example`
- `.env.production.example`

Основные production-переменные:

```env
POSTGRES_DB=med_module
POSTGRES_USER=med_module_user
POSTGRES_PASSWORD=...

JWT_ISSUER=MedicalFeaturePrototype
JWT_AUDIENCE=MedicalFeaturePrototype
JWT_SECRET=...
JWT_ACCESS_TOKEN_LIFETIME_MINUTES=120

SEED_ADMIN_EMAIL=admin@med-module.ru
SEED_ADMIN_PASSWORD=...

CHATGPT_PROXY_API_KEY=...
CHATGPT_MODEL=gpt-5.4-mini
CHATGPT_PROXY_BASE_URL=https://api.proxyapi.ru/openai

GEMINI_PROXY_API_KEY=...
GEMINI_MODEL=gemini-2.5-flash
GEMINI_PROXY_BASE_URL=https://api.proxyapi.ru/google
```

GigaChat можно оставить не настроенным, если он временно не участвует в голосовании:

```env
GIGACHAT_AUTHORIZATION_KEY=
```

## Production-Деплой

Production-схема:

```text
Internet
  -> Nginx host 80/443
    -> 127.0.0.1:8080
      -> frontend container nginx
        -> /api proxy to backend container
          -> postgres container
```

Основные файлы:

- `docker-compose.prod.yml`
- `backend/Dockerfile`
- `frontend/Dockerfile`
- `frontend/nginx.conf`
- `.env.production.example`
- `DEPLOY.md`

Запуск на сервере:

```bash
cd /opt/med-module
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Проверка:

```bash
docker compose -f docker-compose.prod.yml --env-file .env ps
curl -I http://127.0.0.1:8080
curl -I https://med-module.ru
```

## Автодеплой Через GitHub Actions

Workflow:

```text
.github/workflows/deploy-production.yml
```

При push в `main`:

1. GitHub Actions собирает архив проекта.
2. Загружает архив на VPS по SSH.
3. Обновляет `/opt/med-module`.
4. Сохраняет существующий `/opt/med-module/.env`.
5. Выполняет `docker compose up -d --build`.

Нужные GitHub Secrets:

```text
VPS_HOST=130.12.47.15
VPS_USER=root
VPS_PORT=22
VPS_SSH_KEY=<private SSH key>
```

Проверка деплоя:

```text
GitHub -> repository -> Actions -> Deploy production
```

На сервере:

```bash
cd /opt/med-module
docker compose -f docker-compose.prod.yml --env-file .env ps
```

Если `backend` и `frontend` имеют свежий `Up`, новая версия применена.

## API

Основные группы endpoint'ов:

- `/api/auth/*` - регистрация, вход, текущий пользователь.
- `/api/prompt/*` - извлечение симптомов и запрос диагноза.
- `/api/requests/*` - журнал запросов.
- `/api/admin/*` - административная диагностика.
- `/api/patients/*` - legacy endpoints для раннего этапа проекта.

Swagger доступен в development-сценарии:

```text
https://localhost:7274/swagger
```

## Безопасность

Не коммитить:

- `.env`;
- `.env.*`, кроме `.env.example` и `.env.production.example`;
- `appsettings.Development.json`;
- API-ключи;
- пароли;
- deployment-архивы;
- `bin/`, `obj/`, `dist/`, `node_modules/`.

Если ключ был показан в чате, скриншоте или терминале, его лучше перевыпустить.

## Полезные Команды

Логи production:

```bash
cd /opt/med-module
docker compose -f docker-compose.prod.yml --env-file .env logs -f
```

Перезапуск:

```bash
cd /opt/med-module
docker compose -f docker-compose.prod.yml --env-file .env restart
```

Обновление после ручной загрузки файлов:

```bash
cd /opt/med-module
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Проверка HTTPS-автопродления:

```bash
certbot renew --dry-run
```

## Ограничения MVP

- Solver используется как внешний сервис, локальный solver не реализован.
- Качество извлечения симптомов зависит от LLM и prompt engineering.
- GigaChat может быть настроен, но временно не обязан участвовать в голосовании.
- Интерфейс администрирования диагностический: редактирование базы знаний через UI не предусмотрено.
- Для production-сценария секреты хранятся на сервере в `/opt/med-module/.env`.
