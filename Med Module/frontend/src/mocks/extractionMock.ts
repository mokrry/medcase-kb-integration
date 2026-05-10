import type {
  ExtractionEntityResult,
  ExtractionInput,
  ExtractionLogEntry,
  ExtractionRun,
  ExtractionSummary,
  KnowledgeBaseMatch,
  ModelOption,
  ReliabilityEntityResult
} from '../types/extraction';

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

export const MOCK_MODELS: ModelOption[] = [
  {
    id: 'gigachat-mock',
    name: 'GigaChat Med Mock',
    provider: 'GigaChat API',
    description: 'Основной демонстрационный режим извлечения на локальной заглушке.',
    connectionStatus: 'available',
    source: 'mock'
  },
  {
    id: 'proxy-clinical-mock',
    name: 'Clinical Proxy Mock',
    provider: 'Прокси-сервис',
    description: 'Имитирует международную модель через агрегатор без внешнего вызова API.',
    connectionStatus: 'available',
    source: 'mock'
  },
  {
    id: 'audit-mock',
    name: 'Audit Review Mock',
    provider: 'Внутренний контур',
    description: 'Модель-помощник для сценариев ручной проверки.',
    connectionStatus: 'degraded',
    source: 'mock'
  }
];

const symptomPatterns = [
  /каш[её]ль[^.,;\n]*/gi,
  /одышк[аеи][^.,;\n]*/gi,
  /боль[^.,;\n]*/gi,
  /лихорадк[аеи][^.,;\n]*/gi,
  /слабост[ьи][^.,;\n]*/gi,
  /температур[аы][^.,;\n]*/gi
];

const diagnosisPatterns = [
  /(?:диагноз|заключение|ds)[:\s-]+([^.;\n<]+)/gi,
  /пневмони[яи][^.,;\n]*/gi,
  /бронхит[^.,;\n]*/gi,
  /астм[аы][^.,;\n]*/gi,
  /хобл[^.,;\n]*/gi
];

const medicationPatterns = [
  /парацетамол[^.,;\n]*/gi,
  /амоксициллин[^.,;\n]*/gi,
  /цефтриаксон[^.,;\n]*/gi,
  /ибупрофен[^.,;\n]*/gi,
  /ингал[яеи][^.,;\n]*/gi,
  /сальбутамол[^.,;\n]*/gi,
  /лечение[:\s-]+([^.;\n<]+)/gi,
  /назначен[аоы]?[^.;\n]*/gi
];

const indicatorPatterns = [
  /SpO2\s*\d{2,3}%?/gi,
  /сатурац[ияи]\s*\d{2,3}%?/gi,
  /АД\s*\d{2,3}\/\d{2,3}/gi,
  /ЧСС\s*\d{2,3}/gi,
  /пульс\s*\d{2,3}/gi,
  /температур[аы]\s*\d{2}(?:[.,]\d)?/gi,
  /Hb\s*\d{2,3}/gi
];

const knowledgeBaseReference = new Map<string, string[]>([
  ['симптомы', ['кашель', 'одышка', 'лихорадка']],
  ['диагнозы', ['пневмония', 'бронхит', 'астма']],
  ['препараты', ['амоксициллин', 'цефтриаксон', 'сальбутамол']],
  ['даты', ['01.01.2026', '21.04.2026']],
  ['показатели', ['SpO2 95%', 'АД 120/80', 'ЧСС 88']]
]);

function unique(values: string[]) {
  return Array.from(new Set(values.map((value) => value.trim()).filter(Boolean)));
}

function stripXml(xmlText: string) {
  return xmlText
    .replace(/<[^>]+>/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function findMatches(text: string, patterns: RegExp[]) {
  const values: string[] = [];

  patterns.forEach((pattern) => {
    const matches = text.match(pattern);
    if (matches) {
      values.push(...matches);
    }
  });

  return unique(values);
}

function findEvidence(text: string, values: string[]) {
  return values.slice(0, 3).map((value) => {
    const index = text.toLocaleLowerCase('ru-RU').indexOf(value.toLocaleLowerCase('ru-RU'));
    if (index < 0) {
      return value;
    }

    const start = Math.max(0, index - 36);
    const end = Math.min(text.length, index + value.length + 48);
    return text.slice(start, end).trim();
  });
}

function buildEntityResult(entity: string, plainText: string): ExtractionEntityResult {
  const normalized = entity.toLocaleLowerCase('ru-RU');
  let values: string[] = [];
  let comment = '';
  let status: ExtractionEntityResult['status'] = 'NotFound';

  if (normalized.includes('симптом') || normalized.includes('жалоб')) {
    values = findMatches(plainText, symptomPatterns);
    status = values.length === 0 ? 'NotFound' : values.length === 1 ? 'PartiallyFound' : 'Found';
    comment =
      status === 'PartiallyFound'
        ? 'Найдено ограниченное число симптомов, часть значений стоит проверить вручную.'
        : status === 'Found'
          ? 'Симптомы подтверждены текстом истории болезни.'
          : 'В тексте не найдено явных упоминаний симптомов.';
  } else if (normalized.includes('диагноз')) {
    values = findMatches(plainText, diagnosisPatterns);
    status = values.length === 0 ? 'NotFound' : values.length === 1 ? 'PartiallyFound' : 'Found';
    comment =
      status === 'NotFound'
        ? 'Диагностические формулировки не обнаружены.'
        : 'Диагностические формулировки найдены в тексте.';
  } else if (normalized.includes('препарат') || normalized.includes('лечение') || normalized.includes('терап')) {
    values = findMatches(plainText, medicationPatterns);
    status = values.length === 0 ? 'NotFound' : values.length === 1 ? 'PartiallyFound' : 'Found';
    comment =
      status === 'NotFound'
        ? 'Назначения препаратов не обнаружены.'
        : 'Извлечены строки, похожие на назначения и медикаменты.';
  } else if (normalized.includes('дат')) {
    values = unique(plainText.match(/\b\d{2}[./]\d{2}[./]\d{4}\b/g) ?? []);
    status = values.length === 0 ? 'NotFound' : 'Found';
    comment = status === 'Found' ? 'Из текста извлечены календарные даты.' : 'Даты в явном виде не найдены.';
  } else if (
    normalized.includes('показател') ||
    normalized.includes('анализ') ||
    normalized.includes('параметр') ||
    normalized.includes('спо2')
  ) {
    values = findMatches(plainText, indicatorPatterns);
    status = values.length === 0 ? 'NotFound' : values.length === 1 ? 'PartiallyFound' : 'Found';
    comment =
      status === 'NotFound'
        ? 'Числовые показатели не выделены.'
        : 'Выделены измеримые показатели и жизненные параметры.';
  } else {
    const words = normalized.split(/\s+/).filter((word) => word.length > 3);
    const matchedWords = words.filter((word) => plainText.toLocaleLowerCase('ru-RU').includes(word));

    if (plainText.toLocaleLowerCase('ru-RU').includes(normalized)) {
      values = [entity];
      status = 'Found';
      comment = 'Сущность упоминается в тексте в явном виде.';
    } else if (matchedWords.length > 0) {
      values = matchedWords;
      status = 'NeedsReview';
      comment = 'Найдены частичные совпадения, требуется ручная проверка формулировки.';
    } else {
      comment = 'Совпадения по введённой сущности не обнаружены.';
    }
  }

  const evidence = findEvidence(plainText, values);

  if (status === 'Found' && evidence.some((snippet) => snippet.includes('?'))) {
    status = 'NeedsReview';
    comment = 'Найденное упоминание находится в неоднозначном фрагменте текста, нужна ручная проверка.';
  }

  return {
    entity,
    status,
    valueCount: values.length,
    values,
    comment,
    evidence
  };
}

function buildSummary(results: ExtractionEntityResult[]): ExtractionSummary {
  return {
    totalRequested: results.length,
    foundCount: results.filter((item) => item.status === 'Found').length,
    notFoundCount: results.filter((item) => item.status === 'NotFound').length,
    partialCount: results.filter((item) => item.status === 'PartiallyFound').length,
    needsReviewCount: results.filter((item) => item.status === 'NeedsReview').length
  };
}

function buildReliability(results: ExtractionEntityResult[]): ReliabilityEntityResult[] {
  return results.map((result) => ({
    entity: result.entity,
    status:
      result.status === 'Found'
        ? 'Confirmed'
        : result.status === 'NotFound'
          ? 'Unconfirmed'
          : 'NeedsReview',
    rationale:
      result.status === 'Found'
        ? 'Подтверждено фрагментами исходного текста.'
        : result.status === 'NotFound'
          ? 'Подтверждение в тексте не найдено.'
          : 'Есть частичные совпадения или неоднозначная формулировка.'
  }));
}

function buildKnowledgeBase(results: ExtractionEntityResult[]): KnowledgeBaseMatch[] {
  return results.map((result) => {
    const referenceValues = knowledgeBaseReference.get(result.entity.toLocaleLowerCase('ru-RU')) ?? [];
    const matchedValues = result.values.filter((value) =>
      referenceValues.some((reference) => value.toLocaleLowerCase('ru-RU').includes(reference.toLocaleLowerCase('ru-RU')))
    );
    const missingValues = referenceValues.filter(
      (reference) =>
        !matchedValues.some((value) => value.toLocaleLowerCase('ru-RU').includes(reference.toLocaleLowerCase('ru-RU')))
    );

    return {
      entity: result.entity,
      matched: matchedValues.length > 0,
      matchedValues,
      missingValues,
      comment:
        matchedValues.length > 0
          ? 'Есть пересечение с эталонным набором базы знаний.'
          : 'Сопоставление с эталонным набором не найдено.'
    };
  });
}

export async function simulateExtraction(input: ExtractionInput): Promise<ExtractionRun> {
  await wait(950);

  if (input.xmlText.toLocaleLowerCase('ru-RU').includes('simulate_timeout')) {
    await wait(900);
    throw new Error('TIMEOUT');
  }

  const model = MOCK_MODELS.find((item) => item.id === input.modelId) ?? MOCK_MODELS[0];
  const plainText = stripXml(input.xmlText);
  const results = input.entities.map((entity) => buildEntityResult(entity, plainText));
  const summary = buildSummary(results);
  const warnings: string[] = [];

  if (summary.needsReviewCount > 0) {
    warnings.push('Некоторые сущности требуют ручной проверки');
  }

  if (summary.partialCount > 0) {
    warnings.push('Часть сущностей найдена не полностью');
  }

  return {
    requestId: `REQ-${Date.now().toString(36).toUpperCase()}`,
    createdAt: new Date().toISOString(),
    sourceFileName: input.fileName,
    xmlText: input.xmlText,
    plainText,
    modelId: model.id,
    modelName: model.name,
    entities: input.entities,
    results,
    summary,
    reliability: buildReliability(results),
    knowledgeBase: buildKnowledgeBase(results),
    warnings,
    errors: [],
    processingState:
      summary.partialCount > 0 || summary.needsReviewCount > 0 ? 'partial' : 'success',
    progress: 100
  };
}

export function toLogEntry(run: ExtractionRun): ExtractionLogEntry {
  return {
    requestId: run.requestId,
    createdAt: run.createdAt,
    modelName: run.modelName,
    status: run.processingState,
    sourceFileName: run.sourceFileName,
    summary: run.summary,
    warnings: run.warnings,
    errors: run.errors,
    reliabilityReviewCount: run.reliability.filter((item) => item.status === 'NeedsReview').length
  };
}
