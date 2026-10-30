import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ArrowDownUp, ArrowUpDown, Check, ChevronDown, LayoutGrid, List, MountainSnow } from "lucide-react";
import type { BoulderSummary } from "@/features/boulders/api";
import { GradeBadge, HoldIcon } from "@/features/boulders/BoulderBits";
import { holdColorName } from "@/features/boulders/holdColors";
import { useLongPress } from "@/features/boulders/useLongPress";
import { dataLabel } from "@/i18n/data";
import { plural, t } from "@/i18n/i18n";

/** How a gym's boulder list is ordered. The server does the ordering, because the list comes a page at a time. */
export type BoulderSortKey = "SECTOR" | "POINTS" | "GRADE" | "SET" | "REMOVED";
export type SortDirection = "ASC" | "DESC";
export interface BoulderSort { sort: BoulderSortKey; order?: SortDirection }

export const DEFAULT_SORT: BoulderSort = { sort: "SECTOR" };

/** Each sort's natural direction: sectors in the gym's order, most points first, easiest first, newest first. */
const NATURAL: Record<BoulderSortKey, SortDirection> = { SECTOR: "ASC", POINTS: "DESC", GRADE: "ASC", SET: "DESC", REMOVED: "DESC" };
const SORTS: BoulderSortKey[] = ["SECTOR", "POINTS", "GRADE", "SET", "REMOVED"];
const SORT_LABEL: Record<BoulderSortKey, string> = { SECTOR: "Sector", POINTS: "Points", GRADE: "Grade", SET: "Date set", REMOVED: "Date taken down" };

/** "Taken down" only means something on the list of boulders that came off the wall. */
export const sortFor = (sort: BoulderSort, removed: boolean): BoulderSort => (sort.sort === "REMOVED" && !removed ? DEFAULT_SORT : sort);

const readJson = <T,>(storage: () => Storage, key: string): T | null => {
  try { return JSON.parse(storage().getItem(key) ?? "null") as T | null; } catch { return null; }
};
const writeJson = (storage: () => Storage, key: string, value: unknown) => {
  try { storage().setItem(key, JSON.stringify(value)); } catch { /* storage full or disabled: the choice just isn't kept */ }
};

/** The sort of one list, kept for the browser tab like its filters: open a boulder, come back, same order. */
export function useRememberedSort(key: string) {
  const [sort, setSort] = useState<BoulderSort>(() => {
    const kept = readJson<BoulderSort>(() => sessionStorage, key);
    return kept && SORTS.includes(kept.sort) ? kept : DEFAULT_SORT;
  });
  useEffect(() => writeJson(() => sessionStorage, key, sort), [key, sort]);
  return [sort, setSort] as const;
}

export type BoulderView = "grid" | "list";
const VIEW_KEY = "bt:boulder-view";

/** Cards or rows: a preference of the person, kept on this device for every gym. */
export function useBoulderView() {
  const [view, setView] = useState<BoulderView>(() => (readJson<BoulderView>(() => localStorage, VIEW_KEY) === "list" ? "list" : "grid"));
  useEffect(() => writeJson(() => localStorage, VIEW_KEY, view), [view]);
  return [view, setView] as const;
}

/** The sort as a compact drop-down, and a button that turns the order round. */
export function SortControl({ value, onChange, removed }: { value: BoulderSort; onChange: (s: BoulderSort) => void; removed: boolean }) {
  const current = sortFor(value, removed);
  const reversed = (current.order ?? NATURAL[current.sort]) !== NATURAL[current.sort];
  return (
    <div className="sort-control">
      <label className="chip-select">
        <span className="sr-only">{t("Sort by")}</span>
        <select value={current.sort} onChange={(e) => onChange({ sort: e.target.value as BoulderSortKey })}>
          {SORTS.filter((s) => s !== "REMOVED" || removed).map((s) => <option key={s} value={s}>{t(SORT_LABEL[s])}</option>)}
        </select>
        <ChevronDown aria-hidden />
      </label>
      <button type="button" className={`icon-btn icon-btn--outlined icon-btn--sm ${reversed ? "is-on" : ""}`} aria-pressed={reversed}
        aria-label={t("Reverse the order")} title={t("Reverse the order")}
        onClick={() => onChange({ sort: current.sort, order: reversed ? undefined : (NATURAL[current.sort] === "ASC" ? "DESC" : "ASC") })}>
        <ArrowDownUp aria-hidden />
      </button>
    </div>
  );
}

/**
 * The sort as one compact button, for a toolbar that has to fit one row on a phone: it opens the same choice as
 * {@link SortControl} (what to sort by, and the reversed order). The button names the current order for screen readers.
 */
export function SortMenu({ value, onChange, removed }: { value: BoulderSort; onChange: (s: BoulderSort) => void; removed: boolean }) {
  const [open, setOpen] = useState(false);
  const current = sortFor(value, removed);
  const label = t("Sort: {by}", { by: t(SORT_LABEL[current.sort]) });
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open]);
  return (
    <div className="sort-menu">
      <button type="button" className={`icon-btn icon-btn--outlined icon-btn--sm ${open ? "is-on" : ""}`} aria-label={label} title={label}
        aria-expanded={open} aria-controls="boulder-sort" onClick={() => setOpen((v) => !v)}>
        <ArrowUpDown aria-hidden />
      </button>
      {open && (
        <div className="sort-menu__panel" id="boulder-sort">
          <SortControl value={value} onChange={onChange} removed={removed} />
        </div>
      )}
    </div>
  );
}

export function ViewToggle({ value, onChange }: { value: BoulderView; onChange: (v: BoulderView) => void }) {
  return (
    <div className="view-toggle" role="radiogroup" aria-label={t("Show as")}>
      <button type="button" role="radio" aria-checked={value === "grid"} className="view-toggle__option" onClick={() => onChange("grid")}
        aria-label={t("Cards")} title={t("Cards")}><LayoutGrid aria-hidden /></button>
      <button type="button" role="radio" aria-checked={value === "list"} className="view-toggle__option" onClick={() => onChange("list")}
        aria-label={t("List")} title={t("List")}><List aria-hidden /></button>
    </div>
  );
}

/** The climber's own state of a boulder: completed, an open project, tried (not a project), or never tried. */
type OwnState = "done" | "project" | "tried" | "untried";
export const ownState = (b: BoulderSummary): OwnState =>
  b.viewer?.completed ? "done" : b.isProject ? "project" : (b.viewer?.attempts ?? 0) > 0 ? "tried" : "untried";
export const STATE_LABEL: Record<OwnState, string | null> = { done: "You completed this", project: "Your project", tried: "Tried, not completed", untried: null };

/**
 * One boulder as a row of a compact list: the same places in every row, whatever the boulder has — picture, grade,
 * sector, holds, points. With `showOwnState` a bar on the row's left edge says the climber's own state: green when
 * completed, orange for an open project, yellow when tried, none when never tried (and the label says it for screen readers). A missing value leaves its place empty rather
 * than moving the others, and long text is cut short, never wrapped. The name is left out: this list is for scanning,
 * the card and the page have it. With `onLongPress`, holding it starts selecting, as on the cards.
 */
export function BoulderRow({ boulder, to, selectable, selected, onToggle, onLongPress, showOwnState = false }: {
  boulder: BoulderSummary; to?: string; selectable?: boolean; selected?: boolean; onToggle?: () => void; onLongPress?: () => void;
  showOwnState?: boolean;
}) {
  const { pressHandlers, toggleHandlers } = useLongPress(onLongPress, onToggle);
  const primary = boulder.grades[0];
  const state = showOwnState && !selectable ? ownState(boulder) : null;
  const label = [primary ? dataLabel(primary.label) : t("Ungraded"), boulder.sectorName, t("{colour} holds", { colour: holdColorName(boulder.holdColor) }),
    boulder.points != null ? plural(boulder.points, "Worth {count} point", "Worth {count} points") : null,
    state && STATE_LABEL[state] ? t(STATE_LABEL[state]!) : null, boulder.isNew ? t("New") : null].filter(Boolean).join(", ");
  const cells = (
    <>
      <span className="boulder-row__thumb">
        {boulder.photoUrl ? <img src={boulder.photoUrl} alt="" loading="lazy" decoding="async" draggable={false} /> : <MountainSnow aria-hidden />}
        {boulder.isNew && !selectable && <span className="new-tag new-tag--row">{t("New")}</span>}
        {selectable && <span className={`boulder-row__check ${selected ? "is-on" : ""}`} aria-hidden><Check /></span>}
      </span>
      <span className="boulder-row__grade">
        {primary ? <GradeBadge grade={primary} size="sm" /> : <span className="boulder-row__none" aria-hidden>–</span>}
      </span>
      <span className="boulder-row__sector">{boulder.sectorName}</span>
      {/* A hold in the holds' colour says it without words, and leaves the sector the room. */}
      <span className="boulder-row__holds" title={t("{colour} holds", { colour: holdColorName(boulder.holdColor) })} aria-hidden>
        <HoldIcon color={boulder.holdColor} />
      </span>
      <span className="boulder-row__points" aria-hidden>{boulder.points != null ? t("+{points} pt", { points: boulder.points }) : "–"}</span>
    </>
  );
  if (selectable) {
    return (
      <button type="button" className={`boulder-row boulder-row--pressable${state ? ` boulder-row--${state}` : ""} ${selected ? "boulder-row--selected" : ""}`} aria-pressed={selected} aria-label={label}
        {...toggleHandlers}>{cells}</button>
    );
  }
  return <Link to={to ?? `/boulders/${boulder.id}`} className={`boulder-row${state ? ` boulder-row--${state}` : ""} ${onLongPress ? "boulder-row--pressable" : ""}`} aria-label={label} {...pressHandlers}>{cells}</Link>;
}
