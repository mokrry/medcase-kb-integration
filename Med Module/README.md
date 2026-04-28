# MedicalFeaturePrototype

Учебный MVP для анализа медицинских текстов пациентов.

Приложение читает Excel-файл с медицинскими данными, выбирает текст пациента, сопоставляет его с наборами признаков из словарей и показывает результат в веб-интерфейсе. На текущем этапе анализ выполняется простыми rule-based правилами, без LLM.

## Что делает проект

Основной пользовательский сценарий:

1. backend читает Excel-файл `backend/Data/Для студента.xlsx`;
2. загружает пациентов с листа `Сводные данные`;
3. загружает признаки с листов:
   - `Интересующие жалобы`
   - `Дополнительные данные из анамне`
4. frontend показывает список пациентов;
5. пользователь открывает карточку пациента;
6. выбирает, какие словари использовать;
7. frontend отправляет запрос на анализ;
8. backend возвращает результат по каждому признаку со статусом:
   - `Found`
   - `NotFound`
   - `NeedsReview`

## Технологии

Backend:
- C#
- .NET 8
- ASP.NET Core Web API
- ClosedXML

Frontend:
- React
- TypeScript
- Vite

## Архитектура

Проект состоит из двух частей.

### Backend

`backend/` отвечает за:

- чтение Excel-файла;
- хранение данных в памяти;
- API для frontend;
- анализ текста пациента.

Основные сервисы:

- `IExcelDataService` / `ExcelDataService`
- `IPatientTextService` / `PatientTextService`
- `IFeatureAnalysisService` / `FeatureAnalysisService`

### Frontend

`frontend/` отвечает за:

- загрузку списка пациентов;
- показ деталей пациента;
- выбор словарей признаков;
- запуск анализа;
- отображение итоговой таблицы результатов.

## Структура проекта

```text
MedicalFeaturePrototype/
  backend/
    Controllers/
    Data/
    Dtos/
    Models/
    Services/
      Interfaces/
    Program.cs
    appsettings.json
    MedicalFeaturePrototype.Api.csproj

  frontend/
    src/
      api/
      components/
      hooks/
      pages/
      styles/
      types/
      utils/
      App.tsx
      main.tsx

  docs/
  README.md
```

## Источник данных

Excel-файл содержит три ключевых листа:

1. `Сводные данные`
   Поля пациента:
   - `Жалобы`
   - `Анамнез заболевания`
   - `ФИЗИКАЛЬНОЕ ОБСЛЕДОВАНИЕ`

2. `Интересующие жалобы`
   Словарь признаков жалоб.

3. `Дополнительные данные из анамне`
   Словарь признаков анамнеза.

## Логика анализа

Сейчас проект **не использует LLM**.

В `FeatureAnalysisService` применяется простой rule-based подход:

- прямой поиск признака в тексте;
- поиск по словарю синонимов;
- несколько специальных правил для отдельных признаков.

Примеры:

- `Продуктивный кашель`:
  - `кашель с мокротой`
  - `продуктивный кашель`

- `Одышка при физической активности`:
  - `одышка при нагрузке`
  - `одышка при физической нагрузке`

- `Снижение сатурации`:
  - явное упоминание `SpO2`
  - интерпретация значения `SpO2 < 95`

Такое разделение нужно сохранить, чтобы позже можно было заменить rule-based анализ на LLM без переписывания всего приложения.

## API

Backend предоставляет такие endpoint'ы:

- `GET /api/health`  
  Проверка работоспособности backend.

- `GET /api/patients?page=1&pageSize=20`  
  Получение списка пациентов.

- `GET /api/patients/{id}`  
  Получение деталей пациента.

- `GET /api/patients/features?includeComplaintsFeatures=true&includeAnamnesisFeatures=true`  
  Получение списка признаков из выбранных словарей.

- `POST /api/patients/analyze`  
  Запуск анализа пациента.

Пример тела запроса:

```json
{
  "patientId": 0,
  "includeComplaintsFeatures": true,
  "includeAnamnesisFeatures": true
}
```

## Запуск проекта

### 1. Backend

Из папки `backend`:

```powershell
dotnet restore
dotnet run
```

После запуска backend доступен по адресам:

- `http://localhost:5274`
- `https://localhost:7274`

Swagger UI:

- `https://localhost:7274/swagger`

### 2. Frontend

Из папки `frontend`:

```powershell
npm install
npm run dev
```

По умолчанию frontend запускается на:

- `http://localhost:5173`

Для связи с backend используется переменная:

```text
VITE_API_BASE_URL=http://localhost:5274/api
```

## Что важно для проекта

- Это MVP и учебный прототип, а не production-система.
- Backend остаётся источником данных и бизнес-логики.
- Frontend должен быть простым и понятным.
- Архитектуру не нужно усложнять без необходимости.
- На текущем этапе не нужны:
  - база данных;
  - Docker;
  - авторизация;
  - Redux / Zustand / React Query;
  - микросервисы;
  - LLM-интеграция.

## Дальнейшее развитие

Следующие реалистичные шаги:

- улучшать `FeatureAnalysisService`;
- расширять словарь синонимов и специальные правила;
- улучшать UI страниц frontend;
- добавлять новые backend endpoint'ы при необходимости;
- подготовить интерфейс анализа к будущей замене на LLM.
