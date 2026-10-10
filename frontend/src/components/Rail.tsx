import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";

/**
 * A row of cards that scrolls sideways. Each card stops in line with the page's side margin (not against the edge of
 * the phone), and the side with more to see fades out: the right while there are cards beyond it, the left once
 * some have scrolled away. At the end of the row the right edge is sharp again, so the last card is whole.
 */
export function Rail({ children, label }: { children: ReactNode; label?: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const [edges, setEdges] = useState({ start: true, end: true });

  const measure = useCallback(() => {
    const el = ref.current;
    if (!el) return;
    const start = el.scrollLeft <= 2;
    const end = el.scrollLeft + el.clientWidth >= el.scrollWidth - 2;
    setEdges((e) => (e.start === start && e.end === end ? e : { start, end }));
  }, []);

  useEffect(() => {
    measure();
    const el = ref.current;
    if (!el || typeof ResizeObserver === "undefined") return;
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, [measure]);

  return (
    <div ref={ref} className={`rail ${edges.start ? "" : "rail--fade-start"} ${edges.end ? "" : "rail--fade-end"}`}
      onScroll={measure} role={label ? "region" : undefined} aria-label={label}>
      {children}
    </div>
  );
}
