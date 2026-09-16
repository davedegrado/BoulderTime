import type { BoulderGrade } from "@/features/boulders/api";
import { holdColorInfo, holdLabel, inkOn, type HoldColor } from "@/features/boulders/holdColors";

/**
 * GRADE display. Colour grades render as a solid grade badge with the grade name written in it;
 * numeric systems render as text. Hold colour is never shown here.
 */
export function GradeBadge({ grade, size = "md" }: { grade: BoulderGrade; size?: "sm" | "md" | "lg" }) {
  if (grade.systemType === "COLOR" && grade.colorHex) {
    return (
      <span className={`grade grade--color grade--${size}`} style={{ background: grade.colorHex, color: inkOn(grade.colorHex) }}
        title={`${grade.systemName} grade: ${grade.label}`}>
        {grade.label}
      </span>
    );
  }
  return <span className={`grade grade--text grade--${size}`} title={`${grade.systemName} grade`}>{grade.label}</span>;
}

/** Primary grade large, other systems small after it: "YELLOW · 6A · V3". */
export function GradeLine({ grades, size = "md" }: { grades: BoulderGrade[]; size?: "md" | "lg" }) {
  const [primary, ...rest] = grades;
  if (!primary) return null;
  return (
    <span className="grade-line" aria-label={`Grade ${grades.map((g) => `${g.label} (${g.systemName})`).join(", ")}`}>
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
