import { useEffect, useState } from "react";
import type { BoulderFilters } from "@/features/boulders/api";

const read = (key: string): BoulderFilters | null => {
  try { return JSON.parse(sessionStorage.getItem(key) ?? "null") as BoulderFilters | null; } catch { return null; }
};

/**
 * Boulder filters that outlive the page: open a boulder, come back, and the list is still narrowed the same way.
 * Kept for the browser tab only, per list; `accept` says whether what was kept still fits how the page was opened.
 */
export function useRememberedFilters(key: string, initial: () => BoulderFilters, accept: (kept: BoulderFilters) => boolean = () => true) {
  const [filters, setFilters] = useState<BoulderFilters>(() => {
    const kept = read(key);
    return kept && accept(kept) ? kept : initial();
  });
  useEffect(() => {
    try { sessionStorage.setItem(key, JSON.stringify(filters)); } catch { /* storage full or disabled: filters just reset */ }
  }, [key, filters]);
  return [filters, setFilters] as const;
}
