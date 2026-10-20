export const defaultLoginPrimaryTitle = "Todo Gaia, en un solo lugar";

const maximumLoginPrimaryTitleLength = 80;

export function normalizeLoginLineBreaks(value?: string | null) {
  return value?.replace(/\s*<br\s*\/?\s*>\s*/gi, "\n").replace(/\r\n?/g, "\n").trim() ?? "";
}

export function loginDisplayText(configuredValue: string | null | undefined, fallback: string) {
  return normalizeLoginLineBreaks(configuredValue) || fallback;
}

export function loginPrimaryTitle(configuredValue?: string | null) {
  const normalized = normalizeLoginLineBreaks(configuredValue);
  return normalized && normalized.length <= maximumLoginPrimaryTitleLength
    ? normalized
    : defaultLoginPrimaryTitle;
}
