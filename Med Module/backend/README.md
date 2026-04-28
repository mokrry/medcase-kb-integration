# MedicalFeaturePrototype Backend

Backend-часть учебного MVP для анализа медицинских текстов.

Этот сервис читает Excel-файл, хранит данные в памяти, собирает полный текст пациента, выполняет rule-based анализ признаков и отдаёт результат через ASP.NET Core Web API.

## Что делает backend

Backend отвечает за:

- чтение файла `Data/Для студента.xlsx`;
- загрузку пациентов из листа `Сводные данные`;
- загрузку признаков из листов:
  - `Интересующие жалобы`
  - `Дополнительные данные из анамне`
- формирование полного текста пациента;
- анализ текста по простым правилам;
- выдачу JSON-ответов для frontend.

## Технологии

- C#
- .NET 8
- ASP.NET Core Web API
- ClosedXML
- Swagger

## Основные сервисы

- `IExcelDataService` / `ExcelDataService`  
  Чтение Excel, загрузка пациентов и признаков, кэширование в памяти.

- `IPatientTextService` / `PatientTextService`  
  Сбор полного текста пациента из жалоб, анамнеза и физикального обследования.

- `IFeatureAnalysisService` / `FeatureAnalysisService`  
  Rule-based анализ текста пациента и возврат статусов `Found`, `NotFound`, `NeedsReview`.

## API

### `GET /api/health`

Проверка работоспособности backend.

### `GET /api/patients?page=1&pageSize=20`

Получение списка пациентов.

### `GET /api/patients/{id}`

Получение полной информации по пациенту.

### `GET /api/patients/features?includeComplaintsFeatures=true&includeAnamnesisFeatures=true`

Получение признаков из выбранных словарей.

### `POST /api/patients/analyze`

Запуск анализа пациента.

Пример запроса:

```json
{
  "patientId": 0,
  "includeComplaintsFeatures": true,
  "includeAnamnesisFeatures": true
}
```

## Запуск

Из папки `backend`:

```powershell
dotnet restore
dotnet run
```

После запуска сервис доступен по адресам:

- `http://localhost:5274`
- `https://localhost:7274`

Swagger UI:

- `https://localhost:7274/swagger`

## Конфигурация

Основные настройки лежат в `appsettings.json`:

- `Excel:FilePath` - путь к Excel-файлу;
- `Cors:AllowedOrigins` - список разрешённых frontend origin.

По умолчанию используется:

```json
{
  "Excel": {
    "FilePath": "Data/Для студента.xlsx"
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173"
    ]
  }
}
```

## Ограничения текущего MVP

- данные не сохраняются в БД;
- всё хранится в памяти;
- анализ не использует LLM;
- нет авторизации;
- логика анализа остаётся простой и объяснимой.

## Дальнейшее развитие

Следующие шаги для backend:

- улучшать `FeatureAnalysisService`;
- добавлять новые правила и синонимы;
- при необходимости расширять API;
- сохранить структуру сервисов такой, чтобы потом можно было заменить rule-based анализ на LLM.
