import type { ReactNode } from "react";
import { EmptyState } from "@/components/States";

/**
 * Temporary route target for navigation destinations whose feature lands in a later build phase.
 * Tracked in docs/roadmap.md — every usage must be removed by the end of Phase 8.
 */
export function UpcomingPage({ title, body, icon }: { title: string; body: string; icon: ReactNode }) {
  return (
    <div className="page">
      <header className="page__header"><h1 className="page__title">{title}</h1></header>
      <EmptyState icon={icon} title="Not built yet" body={body} />
    </div>
  );
}
