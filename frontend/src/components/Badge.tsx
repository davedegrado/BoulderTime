import type { ReactNode } from "react";

type Tone = "neutral" | "orange" | "success" | "danger" | "dark";

export function Badge({ tone = "neutral", children }: { tone?: Tone; children: ReactNode }) {
  return <span className={`tag tag--${tone}`}>{children}</span>;
}
