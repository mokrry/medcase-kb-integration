import * as XLSX from 'xlsx';

export type SupportedSourceFormat = 'xml' | 'xlsx';

export interface ParsedSourceFile {
  format: SupportedSourceFormat;
  text: string;
}

function workbookToText(buffer: ArrayBuffer) {
  const workbook = XLSX.read(buffer, { type: 'array' });

  return workbook.SheetNames.map((sheetName) => {
    const worksheet = workbook.Sheets[sheetName];
    const rows = XLSX.utils.sheet_to_json<(string | number | boolean | null)[]>(worksheet, {
      header: 1,
      blankrows: false,
      defval: ''
    });

    const content = rows
      .map((row) => row.map((cell) => String(cell).trim()).filter(Boolean).join(' | '))
      .filter(Boolean)
      .join('\n');

    return `Лист: ${sheetName}\n${content}`;
  })
    .filter(Boolean)
    .join('\n\n');
}

export function getSourceFormat(fileName: string): SupportedSourceFormat | null {
  const normalizedName = fileName.toLowerCase();

  if (normalizedName.endsWith('.xml')) {
    return 'xml';
  }

  if (normalizedName.endsWith('.xlsx')) {
    return 'xlsx';
  }

  return null;
}

export async function parseSupportedFile(file: File): Promise<ParsedSourceFile> {
  const format = getSourceFormat(file.name);

  if (!format) {
    throw new Error('Поддерживаются только форматы XML и XLSX');
  }

  if (format === 'xml') {
    return {
      format,
      text: await file.text()
    };
  }

  return {
    format,
    text: workbookToText(await file.arrayBuffer())
  };
}
