# Med Module Backend

Backend-часть проекта `Med Module`.

Актуальное описание архитектуры, локального запуска, production-деплоя и CI/CD находится в основном README:

```text
../README.md
```

Кратко:

- .NET 8 / ASP.NET Core Web API;
- PostgreSQL через Entity Framework Core;
- JWT-аутентификация;
- интеграции с Gemini, ChatGPT, GigaChat config;
- чтение `Data/База_знаний_v24.5.xlsx`;
- маппинг симптомов в `label/value` payload;
- отправка payload в внешний solver;
- журнал запросов пользователей.

Локальный запуск backend:

```powershell
docker compose -f docker-compose.postgres.yml up -d
dotnet ef database update
dotnet run
```

Адреса:

```text
http://localhost:5274
https://localhost:7274
```
