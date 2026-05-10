﻿using System.Text;
using ClosedXML.Excel;
using MedicalFeaturePrototype.Api.Dtos;
using MedicalFeaturePrototype.Api.Models;
using MedicalFeaturePrototype.Api.Services.Interfaces;

namespace MedicalFeaturePrototype.Api.Services;

public class PromptComposerService : IPromptComposerService
{
    private const string KnowledgeBaseFilePrefix = "База_знаний";
    private const string KnowledgeBaseWorksheetName = "Скрипт nodes";

    private readonly ILogger<PromptComposerService> _logger;
    private readonly IWebHostEnvironment _environment;

    public PromptComposerService(
        ILogger<PromptComposerService> logger,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async Task<PromptBuildResponseDto> BuildPromptAsync(
        PromptBuildRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return await BuildPromptCoreAsync(
            request.ComplaintsText,
            request.ComplaintsText,
            request.SymptomsFile,
            cancellationToken);
    }

    public async Task<PromptExecutionPayload> BuildExecutionPayloadAsync(
        PromptRunRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return await BuildExecutionPayloadAsync(
            request,
            request.ComplaintsText,
            cancellationToken);
    }

    public async Task<PromptExecutionPayload> BuildExecutionPayloadAsync(
        PromptRunRequestDto request,
        string preparedComplaintsText,
        CancellationToken cancellationToken = default)
    {
        var promptBuild = await BuildPromptCoreAsync(
            request.ComplaintsText,
            preparedComplaintsText,
            request.SymptomsFile,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.RequestId))
        {
            promptBuild.RequestId = request.RequestId;
        }

        var provider = PromptExecutionService.NormalizeProvider(request.Provider);
        var llmRequest = new LlmPromptRequest(
            provider,
            promptBuild.RequestId,
            promptBuild.Prompt,
            promptBuild.PreparedComplaintsText,
            promptBuild.Symptoms
                .Select(symptom => symptom.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray());

        return new PromptExecutionPayload(promptBuild, llmRequest);
    }

    private async Task<PromptBuildResponseDto> BuildPromptCoreAsync(
        string? sourceComplaintsText,
        string? preparedComplaintsText,
        IFormFile? symptomsFile,
        CancellationToken cancellationToken)
    {
        var sourceComplaints = sourceComplaintsText?.Trim() ?? string.Empty;
        var normalizedComplaints = preparedComplaintsText?.Trim() ?? string.Empty;
        var parsedSymptoms = await ParseSymptomsAsync(symptomsFile, cancellationToken);
        var normalizedSymptoms = parsedSymptoms.Symptoms;

        _logger.LogInformation(
            "[SERVER] Prompt builder received input ComplaintsLength={ComplaintsLength} SymptomsCount={SymptomsCount} SourceFile={SourceFile} Worksheet={Worksheet}",
            normalizedComplaints.Length,
            normalizedSymptoms.Count,
            parsedSymptoms.SourceFileName,
            parsedSymptoms.WorksheetName);

        var warnings = BuildWarnings(normalizedComplaints, normalizedSymptoms);
        var prompt = ComposePrompt(normalizedComplaints, normalizedSymptoms);

        return new PromptBuildResponseDto
        {
            RequestId = $"PROMPT-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Prompt = prompt,
            ComplaintsText = normalizedComplaints,
            SourceComplaintsText = sourceComplaints,
            PreparedComplaintsText = normalizedComplaints,
            TotalSymptoms = normalizedSymptoms.Count,
            FilledSymptoms = normalizedSymptoms.Count,
            Symptoms = normalizedSymptoms,
            Warnings = warnings
        };
    }

    private static List<string> BuildWarnings(string complaintsText, List<SymptomPromptItemDto> symptoms)
    {
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(complaintsText))
        {
            warnings.Add("Не заполнен текст жалоб пациента.");
        }

        if (symptoms.Count == 0)
        {
            warnings.Add("Таблица симптомов пуста.");
        }

        var duplicatedSymptoms = symptoms
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicatedSymptoms.Count > 0)
        {
            warnings.Add($"Обнаружены дубли симптомов: {string.Join(", ", duplicatedSymptoms)}.");
        }

        return warnings;
    }

    private static string ComposePrompt(string complaintsText, List<SymptomPromptItemDto> symptoms)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Ты — врач-эксперт по анализу жалоб пациента и извлечению симптомов.");
        builder.AppendLine();
        builder.AppendLine("Твоя задача — вернуть только симптомы, которые:");
        builder.AppendLine("1. прямо и надежно подтверждены в тексте жалобы;");
        builder.AppendLine("2. описаны в тексте как имеющиеся у пациента сейчас или упоминаются как имевшие место ранее;");
        builder.AppendLine("3. есть в списке допустимых симптомов;");
        builder.AppendLine("4. выведены ровно в каноническом написании из списка допустимых симптомов.");
        builder.AppendLine();
        builder.AppendLine("Ты работаешь как врач-эксперт и извлекаешь симптомы строго по тексту жалобы и whitelist.");
        builder.AppendLine("Ты не объясняешь рассуждения.");
        builder.AppendLine("Ты не добавляешь комментарии.");
        builder.AppendLine("Ты не добавляешь симптомы вне whitelist.");
        builder.AppendLine();
        builder.AppendLine("Входные данные:");
        builder.AppendLine("1. текст жалобы пациента;");
        builder.AppendLine("2. список допустимых симптомов из базы знаний.");
        builder.AppendLine();
        builder.AppendLine("Главное правило whitelist:");
        builder.AppendLine("1. Список допустимых симптомов — единственный источник разрешенных названий.");
        builder.AppendLine("2. Нельзя выводить симптом, которого нет в списке допустимых симптомов.");
        builder.AppendLine("3. Нельзя переименовывать симптом своими словами.");
        builder.AppendLine("4. Нельзя менять порядок слов в каноническом названии.");
        builder.AppendLine("5. Нельзя добавлять скобки, уточнения, комментарии или значения.");
        builder.AppendLine("6. Каждая строка ответа должна быть ровно равна одному названию из whitelist.");
        builder.AppendLine("7. Если в whitelist указано «Продуктивный кашель», нельзя выводить «Кашель продуктивный».");
        builder.AppendLine("8. Если в whitelist указано «Одышка при физической активности», нельзя выводить «Одышка (при физической активности)».");
        builder.AppendLine();
        builder.AppendLine("ПОРЯДОК ДЕЙСТВИЙ МОДЕЛИ");
        builder.AppendLine();
        builder.AppendLine("1. Анализ подготовленного текста");
        builder.AppendLine();
        builder.AppendLine("На вход подается уже подготовленный медицинский текст.");
        builder.AppendLine("Исправление опечаток, смешения кириллицы и латиницы, грамматических ошибок и расшифровка сокращений выполняются на предыдущем этапе программы.");
        builder.AppendLine("В этом промпте не исправляй текст и не занимайся его предобработкой.");
        builder.AppendLine("Извлекай симптомы только из того текста, который передан во входных данных.");
        builder.AppendLine("Не добавляй новую информацию и не меняй смысл текста.");
        builder.AppendLine();
        builder.AppendLine("2. Извлечение нужных сущностей");
        builder.AppendLine();
        builder.AppendLine("Извлекай положительные симптомы, подтвержденные текстом жалобы.");
        builder.AppendLine();
        builder.AppendLine("Не извлекай:");
        builder.AppendLine("1. отрицания;");
        builder.AppendLine("2. предположения;");
        builder.AppendLine("3. сомнительные интерпретации;");
        builder.AppendLine("4. симптомы, которых нет в whitelist;");
        builder.AppendLine("5. признаки, для которых нет точного соответствия в whitelist и нет однозначного сопоставления.");
        builder.AppendLine();
        builder.AppendLine("Примеры отрицательных маркеров:");
        builder.AppendLine();
        builder.AppendLine("Ниже приведены примеры, а не исчерпывающий список. Модель должна учитывать и другие явные формулировки отрицания, если они однозначно показывают, что симптом отсутствует.");
        builder.AppendLine();
        builder.AppendLine("Примеры отрицательных маркеров:");
        builder.AppendLine("«нет», «отрицает», «не отмечает», «не было», «без», «не беспокоит», «не предъявляет», «не жалуется на», «отсутствует».");
        builder.AppendLine();
        builder.AppendLine("Примеры исторических маркеров:");
        builder.AppendLine();
        builder.AppendLine("Ниже приведены примеры, а не исчерпывающий список. Эти формулировки указывают на исторический контекст симптома, но сами по себе не запрещают его извлечение.");
        builder.AppendLine();
        builder.AppendLine("Примеры исторических маркеров:");
        builder.AppendLine("«ранее», «раньше», «в прошлом», «в начале заболевания», «до этого», «перенес», «было ранее», «наблюдалось ранее», «в анамнезе», «несколько лет назад».");
        builder.AppendLine();
        builder.AppendLine("Исторические симптомы учитывай наравне с текущими, если они прямо подтверждены текстом и им соответствует узел whitelist.");
        builder.AppendLine("Если исторический маркер относится только к одной характеристике или подтипу симптома, эту характеристику тоже можно извлекать, если она прямо сформулирована в тексте и имеет точное соответствие в whitelist.");
        builder.AppendLine("Пример: «Кашель с гнойной мокротой с неприятным запахом, в начале заболевания - с прожилками крови» -> текущая часть и историческая часть рассматриваются как подтвержденные текстом признаки. Если в whitelist есть соответствующие узлы, их можно извлекать.");
        builder.AppendLine();
        builder.AppendLine("Если маркер неоднозначен и неясно, относится ли он к конкретному симптому, не делай вывод о временной отнесенности симптома только на основании этого маркера.");
        builder.AppendLine();
        builder.AppendLine("Если симптом указан с длительностью, например «в течение 1,5 месяцев», считай это допустимым подтверждением симптома, если нет отрицания.");
        builder.AppendLine("Формулировки длительности сами по себе не запрещают извлечение симптома.");
        builder.AppendLine();
        builder.AppendLine("Если сам whitelist-узел содержит указание на анамнез, например «... в анамнезе», и текст прямо подтверждает это, такой узел также можно извлекать.");
        builder.AppendLine();
        builder.AppendLine("Что считается подтверждением симптома:");
        builder.AppendLine("Симптом считается подтвержденным, если выполнено хотя бы одно условие:");
        builder.AppendLine("1. в тексте есть точное совпадение с каноническим названием из whitelist;");
        builder.AppendLine("2. в тексте есть грамматическая форма того же выражения, если смысл остается тем же и не добавляется новая информация;");
        builder.AppendLine("3. в тексте есть очевидная языковая нормализация того же симптома без медицинской догадки;");
        builder.AppendLine("4. в тексте есть однозначное языковое соответствие симптому из whitelist;");
        builder.AppendLine("5. для температуры выполнено специальное числовое правило.");
        builder.AppendLine();
        builder.AppendLine("Нельзя считать симптом подтвержденным, если для этого нужно добавить отсутствующий признак: локализацию, степень, длительность, цвет, характер, подтип, связь с физической активностью или анатомическую область.");
        builder.AppendLine("Если текст содержит симптом с дополнительной деталью, а whitelist содержит только более общий узел, можно вывести общий узел, если общий признак прямо подтвержден текстом. При этом нельзя добавлять эту деталь в название симптома.");
        builder.AppendLine("Пример: «боли в грудной клетке справа» -> «Боль в грудной клетке», если такой узел есть в whitelist.");
        builder.AppendLine();
        builder.AppendLine("Запрет интерпретаций:");
        builder.AppendLine("1. Не интерпретируй клинически.");
        builder.AppendLine("2. Не достраивай симптомы.");
        builder.AppendLine("3. Не выводи симптом по ассоциации.");
        builder.AppendLine("4. Не выводи симптом по клинической типичности.");
        builder.AppendLine("5. Не превращай общий признак в частный.");
        builder.AppendLine("6. Не превращай частный признак в другой частный.");
        builder.AppendLine("7. Не добавляй отсутствующие характеристики: локализацию, степень, цвет, характер, подтип или анатомическую область.");
        builder.AppendLine("8. Лучше пропустить симптом, чем вывести неточное соответствие.");
        builder.AppendLine();
        builder.AppendLine("Примеры нормализаций и сопоставлений:");
        builder.AppendLine();
        builder.AppendLine("Ниже приведены примеры, а не исчерпывающий список. Модель может применять и другие очевидные языковые варианты, грамматические формы, синонимичные формулировки и сопоставления с whitelist только если они описывают тот же симптом на том же уровне детализации и не требуют медицинского вывода, а также если одновременно выполняются условия:");
        builder.AppendLine("1. смысл исходного текста не меняется;");
        builder.AppendLine("2. новая информация не добавляется;");
        builder.AppendLine("3. сопоставление не требует медицинского вывода;");
        builder.AppendLine("4. сопоставление однозначно;");
        builder.AppendLine("5. итоговый симптом есть в whitelist;");
        builder.AppendLine("6. симптом актуален сейчас и не находится под отрицанием.");
        builder.AppendLine();
        builder.AppendLine("Очевидная нормализация — это языковое преобразование, при котором исходная фраза и симптом из whitelist обозначают один и тот же признак, без добавления причины, локализации, степени, длительности, цвета, характера, подтипа или условия возникновения.");
        builder.AppendLine();
        builder.AppendLine("Синонимичная формулировка допустима только если она описывает тот же симптом на том же уровне детализации. Нельзя добавлять отсутствующие условия возникновения, например физическую активность, время суток, степень, локализацию, цвет, характер или подтип.");
        builder.AppendLine("Если в whitelist есть более точный узел, соответствующий формулировке текста, выбирай его. Не заменяй один близкий симптом другим, если они различаются по атрибутам: причина, условие возникновения, локализация, характер, степень или время появления.");
        builder.AppendLine();
        builder.AppendLine("Общие симптомы, примеры:");
        builder.AppendLine("«общая слабость» -> «Слабость», если такой узел есть в whitelist.");
        builder.AppendLine("«слабость» -> «Слабость», если такой узел есть в whitelist.");
        builder.AppendLine("«снижение веса» -> «Снижение массы тела», если такой узел есть в whitelist.");
        builder.AppendLine("«потеря веса» -> «Снижение массы тела», если такой узел есть в whitelist.");
        builder.AppendLine("«похудание» -> «Снижение массы тела», если такой узел есть в whitelist.");
        builder.AppendLine("Указание потери веса или потери килограммов подтверждает «Снижение массы тела», если такой узел есть в whitelist.");
        builder.AppendLine();
        builder.AppendLine("Одышка, примеры:");
        builder.AppendLine("«одышка при физической нагрузке» -> «Одышка при физической активности», если такой узел есть в whitelist.");
        builder.AppendLine("«одышка при незначительной ФН» -> «Одышка при физической активности», если такой узел есть в whitelist.");
        builder.AppendLine("«одышка при минимальной ФН» -> «Одышка при физической активности», если такой узел есть в whitelist.");
        builder.AppendLine("Не выводи «Усиление одышки», если в тексте нет прямого указания на усиление одышки.");
        builder.AppendLine();
        builder.AppendLine("Кашель и мокрота, примеры:");
        builder.AppendLine("«кашель с мокротой» -> «Продуктивный кашель», если такой узел есть в whitelist.");
        builder.AppendLine("«кашель со ... мокротой» -> «Продуктивный кашель», если такой узел есть в whitelist.");
        builder.AppendLine("«кашель и мокрота» -> «Продуктивный кашель», если такой узел есть в whitelist.");
        builder.AppendLine("«мокрота при кашле» -> «Продуктивный кашель», если такой узел есть в whitelist.");
        builder.AppendLine("«кашель с небольшим количеством мокроты» -> «Малопродуктивный кашель», если такой узел есть в whitelist.");
        builder.AppendLine();
        builder.AppendLine("Если в тексте есть положительное упоминание кашля и в той же жалобе, фразе или предложении есть положительное упоминание мокроты любого типа, это подтверждает «Продуктивный кашель», если такой узел есть в whitelist.");
        builder.AppendLine();
        builder.AppendLine("«Продуктивный кашель» и подтип мокроты являются совместимыми симптомами, а не дублями.");
        builder.AppendLine("Подтип мокроты не заменяет «Продуктивный кашель».");
        builder.AppendLine("Не удаляй «Продуктивный кашель» только из-за наличия подтипа мокроты.");
        builder.AppendLine();
        builder.AppendLine("Если одновременно указан подтип мокроты, можно вывести и «Продуктивный кашель», и подтип мокроты, если оба узла есть в whitelist.");
        builder.AppendLine();
        builder.AppendLine("Падежные формы мокроты, примеры:");
        builder.AppendLine("«слизисто-гнойной мокротой» -> «Слизисто-гнойная мокрота», если такой узел есть в whitelist.");
        builder.AppendLine("«слизистой мокротой» -> «Слизистая мокрота», если такой узел есть в whitelist.");
        builder.AppendLine("«серозно-геморрагической мокротой» -> «Серозно-геморрагическая мокрота», если такой узел есть в whitelist.");
        builder.AppendLine("«ржавой мокротой» -> «\"Ржавая\" мокрота», если такой узел есть в whitelist.");
        builder.AppendLine();
        builder.AppendLine("Кровохарканье и кровь, примеры:");
        builder.AppendLine("«кровохаркание» -> «Кровохарканье», если такой узел есть в whitelist.");
        builder.AppendLine("«кровохарканье» -> «Кровохарканье», если такой узел есть в whitelist.");
        builder.AppendLine("Не заменяй «Кровохарканье» на «Мокрота с примесью крови».");
        builder.AppendLine("Не заменяй «Мокрота с примесью крови» на «Кровохарканье».");
        builder.AppendLine("«Кровь при кашле» выводи только при прямой формулировке «кровь при кашле» или эквивалентной конструкции «при кашле кровь».");
        builder.AppendLine("Не заменяй «Кровохарканье» на «Кровь при кашле» и наоборот.");
        builder.AppendLine();
        builder.AppendLine("Боль в грудной клетке, примеры:");
        builder.AppendLine("«боль в грудной клетке» -> «Боль в грудной клетке», если такой узел есть в whitelist.");
        builder.AppendLine("«боли в грудной клетке» -> «Боль в грудной клетке», если такой узел есть в whitelist.");
        builder.AppendLine("Уточнения «справа», «слева», «при кашле», «усиливающиеся при кашле» не запрещают вывод «Боль в грудной клетке», если в whitelist нет более точного подходящего узла.");
        builder.AppendLine();
        builder.AppendLine("Температура:");
        builder.AppendLine("Если в тексте есть положительное упоминание температуры, лихорадки, жара, t, T или t тела, нужно вывести температурный симптом, если подходящий температурный узел есть в whitelist.");
        builder.AppendLine();
        builder.AppendLine("Если температура указана числом, включая форматы «38,5», «38.5», «до 38,5», «t 38,5», «T до 38,5», «лихорадка до 38,5 С», считай это прямым указанием на максимальную температуру тела.");
        builder.AppendLine();
        builder.AppendLine("При числовой температуре выбери ровно одну категорию строго по диапазону:");
        builder.AppendLine("37,1-37,9 °C -> «Субфебрильная температура».");
        builder.AppendLine("38,0-39,5 °C -> «Фебрильная температура».");
        builder.AppendLine("39,6-40,9 °C -> «Пиретическая температура».");
        builder.AppendLine("41,0 °C и выше -> «Гиперпиретическая температура».");
        builder.AppendLine();
        builder.AppendLine("Выводить можно только ту температурную категорию, которая есть в whitelist.");
        builder.AppendLine();
        builder.AppendLine("При числовой температуре запрещено выводить «Повышение температуры», если в whitelist есть подходящая категориальная температура.");
        builder.AppendLine();
        builder.AppendLine("Если температура упомянута без числа, например «лихорадка», «температура повышена», «жар», и в whitelist есть «Повышение температуры», выведи «Повышение температуры».");
        builder.AppendLine();
        builder.AppendLine("Десятичная запятая и десятичная точка равнозначны: 38,5 = 38.5.");
        builder.AppendLine("Обозначения «С», «C», «°С», «°C» равнозначны.");
        builder.AppendLine();
        builder.AppendLine("Невывод температурного симптома при наличии положительного упоминания температуры считается ошибкой.");
        builder.AppendLine();
        builder.AppendLine("Специальные запреты:");
        builder.AppendLine("1. «ломота в теле» не выводится, если в whitelist нет точного узла «Ломота в теле».");
        builder.AppendLine("2. Не нормализуй «ломота в теле» в «Ломота в суставах», «Артралгия», «Миалгия» или «Оссалгия».");
        builder.AppendLine("3. «потливость» -> «Ночные поты» только если из текста прямо следует, что поты ночные.");
        builder.AppendLine("4. Не выводи «Затяжное течение болезни» только по длительности симптома, если в тексте прямо не сказано «затяжное течение», «болеет длительно» или если отдельное правило длительности не разрешает такое сопоставление.");
        builder.AppendLine("5. Не выводи «Длительный кашель» только по наличию кашля, если в тексте прямо не указано, что кашель длительный или продолжается значимый срок.");
        builder.AppendLine();
        builder.AppendLine("Правила выбора между симптомами:");
        builder.AppendLine("1. Если несколько симптомов являются взаимоисключающими вариантами одной группы, выбирай наиболее точный подтвержденный узел.");
        builder.AppendLine("2. Не применяй правило наиболее точного узла к совместимым симптомам.");
        builder.AppendLine("3. «Продуктивный кашель» и подтип мокроты совместимы.");
        builder.AppendLine("4. «Кровохарканье» и «Мокрота с примесью крови» не являются автоматическими заменами друг друга.");
        builder.AppendLine("5. После финальных проверок запрещено удалять симптомы, добавленные обязательными правилами.");
        builder.AppendLine("6. Удали полные дубли. Один и тот же симптом нельзя выводить дважды.");
        builder.AppendLine();
        builder.AppendLine("Финальная самопроверка перед ответом:");
        builder.AppendLine("Перед выводом проверь:");
        builder.AppendLine("1. ответ содержит только строки из whitelist;");
        builder.AppendLine("2. ответ является корректным JSON-объектом нужного формата;");
        builder.AppendLine("3. в ответе нет отрицательных симптомов;");
        builder.AppendLine("4. в ответе нет дублей;");
        builder.AppendLine("5. каждый симптом подтвержден переданным медицинским текстом;");
        builder.AppendLine("6. обязательные симптомы по специальным правилам не пропущены;");
        builder.AppendLine("7. при числовой температуре выбран ровно один температурный симптом, если подходящий узел есть в whitelist;");
        builder.AppendLine("8. при кашле с мокротой выведен «Продуктивный кашель», если такой узел есть в whitelist;");
        builder.AppendLine("9. подтип мокроты выведен, если он однозначно соответствует whitelist;");
        builder.AppendLine("10. обязательными считаются только те симптомы, которые в переданном медицинском тексте прямо или через очевидное языковое сопоставление однозначно соответствуют узлу whitelist;");
        builder.AppendLine("11. если обязательный по этим правилам симптом отсутствует, исправь ответ до вывода;");
        builder.AppendLine("12. не добавляй пояснения.");
        builder.AppendLine();
        builder.AppendLine("3. Оформление вывода");
        builder.AppendLine();
        builder.AppendLine("Верни только итоговый JSON с массивом симптомов.");
        builder.AppendLine();
        builder.AppendLine("Формат ответа:");
        builder.AppendLine("1. Верни только JSON-объект без Markdown-разметки.");
        builder.AppendLine("2. Используй формат: {\"symptoms\": [\"Симптом 1\", \"Симптом 2\"]}.");
        builder.AppendLine("3. Ключ верхнего уровня должен быть ровно «symptoms».");
        builder.AppendLine("4. Значение ключа «symptoms» должно быть массивом строк.");
        builder.AppendLine("5. Каждая строка массива должна быть ровно равна одному каноническому названию из whitelist.");
        builder.AppendLine("6. Не добавляй другие ключи.");
        builder.AppendLine("7. Не добавляй комментарии, пояснения, диагнозы, рекомендации или рассуждения.");
        builder.AppendLine();
        builder.AppendLine("Если ничего не найдено, верни:");
        builder.AppendLine("{\"symptoms\":[]}");
        builder.AppendLine();
        builder.AppendLine("Пример вывода:");
        builder.AppendLine();
        builder.AppendLine("Исходный текст жалобы:");
        builder.AppendLine("Жалобы на сухой кашель с небольшим количеством трудноотделяемой мокроты, общую слабость, одышку при физической нагрузке, повышение температуры тела до 38,5 С.");
        builder.AppendLine();
        builder.AppendLine("Правильный итоговый ответ:");
        builder.AppendLine("{\"symptoms\":[\"Сухой кашель\",\"Малопродуктивный кашель\",\"Одышка при физической активности\",\"Слабость\",\"Фебрильная температура\"]}");
        builder.AppendLine();
        builder.AppendLine("Почему именно так:");
        builder.AppendLine("1. «сухой кашель» подтверждает «Сухой кашель», если такой узел есть в whitelist.");
        builder.AppendLine("2. «с небольшим количеством трудноотделяемой мокроты» подтверждает «Малопродуктивный кашель», если такой узел есть в whitelist.");
        builder.AppendLine("3. «одышку при физической нагрузке» подтверждает «Одышка при физической активности», если такой узел есть в whitelist.");
        builder.AppendLine("4. «общую слабость» подтверждает «Слабость», если такой узел есть в whitelist.");
        builder.AppendLine("5. «температуры тела до 38,5 С» подтверждает «Фебрильная температура», если такой узел есть в whitelist.");
        builder.AppendLine();
        builder.AppendLine("Важно:");
        builder.AppendLine("Если текст содержит «кашель с небольшим количеством мокроты» и в whitelist есть «Малопродуктивный кашель», выводи «Малопродуктивный кашель» как более точный вариант.");
        builder.AppendLine("В этом случае не добавляй «Продуктивный кашель», если только текст отдельно не содержит более общее независимое указание на продуктивный кашель без уточнения малого количества мокроты.");
        builder.AppendLine();
        builder.AppendLine("Плохой пример ответа:");
        builder.AppendLine();
        builder.AppendLine("Исходный текст жалобы:");
        builder.AppendLine("Лихорадка: темперaтура 39,6 С. Кашель с мoкротой. Одышка при небольшой физическoй нагрузке. общая слабость, ломота в теле.");
        builder.AppendLine();
        builder.AppendLine("Неправильный ответ:");
        builder.AppendLine("Продуктивный кашель");
        builder.AppendLine("Одышка при физической активности");
        builder.AppendLine("Фебрильная температура");
        builder.AppendLine();
        builder.AppendLine("Почему это плохой пример:");
        builder.AppendLine("1. «температура 39,6 С» должна распознаваться как числовая температура 39,6 °C.");
        builder.AppendLine("2. Значение 39,6 °C соответствует «Пиретическая температура», а не «Фебрильная температура».");
        builder.AppendLine("3. «общая слабость» должна подтверждать «Слабость», если такой узел есть в whitelist.");
        builder.AppendLine("4. Ответ плохой, потому что в нем пропущен обязательный симптом «Слабость».");
        builder.AppendLine("5. «ломота в теле» нельзя автоматически преобразовывать в другой симптом, если в whitelist нет точного допустимого узла.");
        builder.AppendLine("6. Правильность «Продуктивный кашель» и «Одышка при физической активности» не отменяет того, что ответ остается неправильным из-за неверной температурной категории и пропуска обязательного симптома.");
        builder.AppendLine();
        builder.AppendLine("Текст жалобы пациента:");
        builder.AppendLine(complaintsText);
        builder.AppendLine();
        builder.AppendLine("Допустимые симптомы из базы знаний:");

        if (symptoms.Count == 0)
        {
            builder.AppendLine("Список пуст.");
        }
        else
        {
            foreach (var symptom in symptoms)
            {
                builder.AppendLine(string.IsNullOrWhiteSpace(symptom.Name) ? "не указан" : symptom.Name.Trim());
            }
        }

        return builder.ToString().Trim();
    }
    private async Task<ParsedSymptomsResult> ParseSymptomsAsync(
        IFormFile? symptomsFile,
        CancellationToken cancellationToken)
    {
        var source = await OpenSymptomsSourceAsync(symptomsFile, cancellationToken);
        if (source is null)
        {
            return new ParsedSymptomsResult(new List<SymptomPromptItemDto>(), string.Empty, string.Empty);
        }

        var sourceFileName = source.SourceFileName;
        await using var sourceStream = source.Stream;
        sourceStream.Position = 0;

        using var workbook = new XLWorkbook(sourceStream);
        var worksheet = ResolveKnowledgeBaseWorksheet(workbook);
        if (worksheet is null)
        {
            _logger.LogWarning(
                "[SERVER] Prompt builder could not find worksheet {Worksheet} in source file {SourceFile}",
                KnowledgeBaseWorksheetName,
                sourceFileName);
            return new ParsedSymptomsResult(new List<SymptomPromptItemDto>(), sourceFileName, string.Empty);
        }

        var rows = worksheet.RangeUsed()?.RowsUsed() ?? Enumerable.Empty<IXLRangeRow>();
        var symptoms = new List<SymptomPromptItemDto>();
        var seenSymptoms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var sectionValue = row.Cell(4).GetString().Trim();
            if (sectionValue is not ("2" or "3"))
            {
                continue;
            }

            var name = row.Cell(6).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name) || !seenSymptoms.Add(name))
            {
                continue;
            }

            symptoms.Add(new SymptomPromptItemDto
            {
                Name = name
            });
        }

        _logger.LogInformation(
            "[SERVER] Prompt builder selected symptoms from knowledge base Count={SymptomsCount} Worksheet={Worksheet} SourceFile={SourceFile}",
            symptoms.Count,
            worksheet.Name,
            sourceFileName);

        return new ParsedSymptomsResult(symptoms, sourceFileName, worksheet.Name);
    }

    private async Task<SymptomsSourceResult?> OpenSymptomsSourceAsync(IFormFile? symptomsFile, CancellationToken cancellationToken)
    {
        if (symptomsFile is not null && symptomsFile.Length > 0)
        {
            var uploadedStream = new MemoryStream();
            await symptomsFile.CopyToAsync(uploadedStream, cancellationToken);
            uploadedStream.Position = 0;
            return new SymptomsSourceResult(uploadedStream, symptomsFile.FileName);
        }

        var knowledgeBasePath = ResolveKnowledgeBasePath();
        if (knowledgeBasePath is null)
        {
            _logger.LogWarning(
                "[SERVER] Prompt builder did not find uploaded symptoms file or local knowledge base in {DataDirectory}",
                Path.Combine(_environment.ContentRootPath, "Data"));
            return null;
        }

        var fileBytes = await File.ReadAllBytesAsync(knowledgeBasePath, cancellationToken);
        return new SymptomsSourceResult(
            new MemoryStream(fileBytes),
            Path.GetFileName(knowledgeBasePath));
    }

    private string? ResolveKnowledgeBasePath()
    {
        var dataDirectory = Path.Combine(_environment.ContentRootPath, "Data");
        if (!Directory.Exists(dataDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(dataDirectory, "*.xlsx", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path =>
                Path.GetFileNameWithoutExtension(path).StartsWith(
                    KnowledgeBaseFilePrefix,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static IXLWorksheet? ResolveKnowledgeBaseWorksheet(XLWorkbook workbook)
    {
        return workbook.Worksheets.FirstOrDefault(worksheet =>
                   worksheet.Name.Equals(KnowledgeBaseWorksheetName, StringComparison.OrdinalIgnoreCase))
               ?? workbook.Worksheets.FirstOrDefault(worksheet =>
                   worksheet.Name.Equals("script nodes", StringComparison.OrdinalIgnoreCase))
               ?? workbook.Worksheets.FirstOrDefault(worksheet =>
                   worksheet.Name.Contains("nodes", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record ParsedSymptomsResult(
        List<SymptomPromptItemDto> Symptoms,
        string SourceFileName,
        string WorksheetName);

    private sealed record SymptomsSourceResult(
        MemoryStream Stream,
        string SourceFileName);
}
