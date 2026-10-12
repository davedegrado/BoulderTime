import type { CSSProperties } from "react";
import type { BoulderGrade } from "@/features/boulders/api";
import { holdColorInfo, holdLabel, inkOn, type HoldColor } from "@/features/boulders/holdColors";
import { dataLabel } from "@/i18n/data";
import { t } from "@/i18n/i18n";

/**
 * GRADE display. Colour grades render as a solid grade badge with the grade name written in it;
 * numeric systems render as text. Hold colour is never shown here.
 */
/**
 * The look of a colour grade: its colour, or for an in-between grade ("green/blue") both colours split on the
 * diagonal. The text takes the ink that reads on the colour; when the two colours want different inks it is white with
 * a shadow (`data-mixed-ink`).
 */
export function colorGradeLook(colorHex: string, colorHex2?: string | null): { style: CSSProperties; mixedInk: boolean } {
  if (!colorHex2) return { style: { background: colorHex, color: inkOn(colorHex) }, mixedInk: false };
  const first = inkOn(colorHex), second = inkOn(colorHex2);
  return {
    style: { background: `linear-gradient(135deg, ${colorHex} 0 50%, ${colorHex2} 50% 100%)`, color: first === second ? first : "#fff" },
    mixedInk: first !== second,
  };
}

/** A colour grade badge from its parts, for the places that have a grade's label and colours but not a whole grade. */
export function ColorGrade({ label, colorHex, colorHex2, size = "md", title }: { label: string; colorHex: string; colorHex2?: string | null; size?: "sm" | "md" | "lg"; title?: string }) {
  const look = colorGradeLook(colorHex, colorHex2);
  return (
    <span className={`grade grade--color grade--${size}${colorHex2 ? " grade--duo" : ""}`} style={look.style} data-mixed-ink={look.mixedInk ? "" : undefined} title={title}>
      {dataLabel(label)}
    </span>
  );
}

export function GradeBadge({ grade, size = "md" }: { grade: BoulderGrade; size?: "sm" | "md" | "lg" }) {
  if (grade.systemType === "COLOR" && grade.colorHex) {
    return <ColorGrade label={grade.label} colorHex={grade.colorHex} colorHex2={grade.colorHex2} size={size}
      title={t("{system} grade: {grade}", { system: dataLabel(grade.systemName), grade: dataLabel(grade.label) })} />;
  }
  return <span className={`grade grade--text grade--${size}`} title={t("{system} grade", { system: dataLabel(grade.systemName) })}>{dataLabel(grade.label)}</span>;
}

/** Primary grade large, other systems small after it: "YELLOW · 6A · V3". */
export function GradeLine({ grades, size = "md" }: { grades: BoulderGrade[]; size?: "md" | "lg" }) {
  const [primary, ...rest] = grades;
  if (!primary) return <span className={`grade grade--text grade--ungraded grade--${size}`}>{t("Ungraded")}</span>;
  return (
    <span className="grade-line" aria-label={t("Grade {grades}", { grades: grades.map((g) => `${dataLabel(g.label)} (${dataLabel(g.systemName)})`).join(", ") })}>
      <GradeBadge grade={primary} size={size} />
      {rest.map((g) => <GradeBadge key={g.gradeSystemId} grade={g} size="sm" />)}
    </span>
  );
}

/** HOLD colour display: a hold-shaped swatch plus the words "… holds". */
export function HoldBadge({ color, compact = false }: { color: HoldColor; compact?: boolean }) {
  const info = holdColorInfo(color);
  return (
    <span className={`holds ${compact ? "holds--compact" : ""}`}>
      <span className="holds__swatch" style={{ background: info.hex }} aria-hidden />
      <span className="holds__label">{holdLabel(color)}</span>
    </span>
  );
}
