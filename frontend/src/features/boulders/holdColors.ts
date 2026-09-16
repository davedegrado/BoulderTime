/**
 * PHYSICAL hold colours. This palette is only ever used for holds — never for grades,
 * even when a gym grades by colour. UI copy always says "… holds".
 */
export type HoldColor = "RED" | "ORANGE" | "YELLOW" | "GREEN" | "BLUE" | "PURPLE" | "PINK" | "WHITE" | "BLACK" | "GREY" | "BROWN" | "MIXED";

export const HOLD_COLORS: { value: HoldColor; label: string; hex: string }[] = [
  { value: "RED", label: "Red", hex: "#D93A2B" },
  { value: "ORANGE", label: "Orange", hex: "#F28C28" },
  { value: "YELLOW", label: "Yellow", hex: "#F5C400" },
  { value: "GREEN", label: "Green", hex: "#2E9E4F" },
  { value: "BLUE", label: "Blue", hex: "#2F6FDB" },
  { value: "PURPLE", label: "Purple", hex: "#8046BE" },
  { value: "PINK", label: "Pink", hex: "#E85EA0" },
  { value: "WHITE", label: "White", hex: "#F4F4F4" },
  { value: "BLACK", label: "Black", hex: "#1A1A1A" },
  { value: "GREY", label: "Grey", hex: "#8A8A8A" },
  { value: "BROWN", label: "Brown", hex: "#8B5A2B" },
  { value: "MIXED", label: "Mixed", hex: "conic-gradient(#D93A2B 0 25%, #F5C400 0 50%, #2E9E4F 0 75%, #2F6FDB 0)" },
];

export const holdColorInfo = (value: HoldColor) => HOLD_COLORS.find((c) => c.value === value) ?? HOLD_COLORS[0]!;

/** "Blue holds", "Mixed holds". */
export const holdLabel = (value: HoldColor) => `${holdColorInfo(value).label} holds`;

/** Readable text colour on top of a hex background (for colour GRADE badges). */
export function inkOn(hex: string): string {
  const m = /^#([0-9a-f]{6})$/i.exec(hex);
  if (!m) return "#1A1A1A";
  const n = parseInt(m[1]!, 16);
  const [r, g, b] = [(n >> 16) & 255, (n >> 8) & 255, n & 255];
  return 0.299 * r + 0.587 * g + 0.114 * b > 150 ? "#1A1A1A" : "#FFFFFF";
}
