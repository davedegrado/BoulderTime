import { t } from "@/i18n/i18n";

/** How hard a boulder works the body, apart from its grade. Three levels, never more. */
export type Intensity = "LOW" | "MEDIUM" | "HIGH";
export const INTENSITIES: Intensity[] = ["LOW", "MEDIUM", "HIGH"];

const LEVEL: Record<Intensity, number> = { LOW: 1, MEDIUM: 2, HIGH: 3 };

export function intensityLabel(i: Intensity): string {
  return i === "LOW" ? t("Low intensity") : i === "MEDIUM" ? t("Medium intensity") : t("High intensity");
}

/** For buttons and filters: the same words, so the three levels always read the same everywhere. */
export const intensityShort = intensityLabel;

/**
 * Three rising bars, one to three filled, in the text colour: never a grade or hold colour, so nobody reads it as
 * one. The label says it in words for anyone who can't tell bars apart.
 */
export function IntensityBars({ intensity, className = "" }: { intensity: Intensity; className?: string }) {
  const level = LEVEL[intensity];
  return (
    <svg className={`intensity-bars ${className}`} viewBox="0 0 14 12" aria-hidden>
      {[0, 1, 2].map((i) => (
        <rect key={i} x={i * 5} y={8 - i * 4} width="3.5" height={4 + i * 4} rx="1" className={i < level ? "is-on" : ""} />
      ))}
    </svg>
  );
}

/** Bars and words, as the boulder page and the editor show it. Compact: bars only, the words for screen readers. */
export function IntensityBadge({ intensity, compact }: { intensity: Intensity; compact?: boolean }) {
  return (
    <span className="intensity" title={intensityLabel(intensity)}>
      <IntensityBars intensity={intensity} />
      {compact ? <span className="sr-only">{intensityLabel(intensity)}</span> : <span>{intensityLabel(intensity)}</span>}
    </span>
  );
}
