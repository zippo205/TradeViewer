/** The part of the Web Crypto API the id needs; `randomUUID` is missing outside a secure context. */
export interface RandomSource {
  randomUUID?: () => string;
  getRandomValues(array: Uint8Array<ArrayBuffer>): Uint8Array<ArrayBuffer>;
}

/**
 * A new random id (UUID version 4) for a drawing. Browsers offer `crypto.randomUUID` only in a secure context
 * (HTTPS or localhost); TradeViewer is opened over plain HTTP in the home network (NF8), where only
 * `getRandomValues` exists, so the id is built from that when needed.
 */
export function newId(source: RandomSource = crypto): string {
  if (typeof source.randomUUID === "function") return source.randomUUID();
  const bytes = source.getRandomValues(new Uint8Array(16));
  bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x40; // version 4
  bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80; // variant 10xx
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
