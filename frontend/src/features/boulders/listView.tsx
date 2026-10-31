import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ArrowUpDown, Check, LayoutGrid, List, MountainSnow } from "lucide-react";
import { BottomSheet } from "@/features/boulders/BoulderFilters";
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

/** The "Sort" button: it names the order in use, and opens the sheet to change it. */
export function SortButton({ value, removed, onClick }: { value: BoulderSort; removed: boolean; onClick: () => void }) {
  const current = sortFor(value, removed);
  const label = t("Sort: {by}", { by: t(SORT_LABEL[current.sort]) });
  return (
    <button type="button" className="chip tool-btn" aria-haspopup="dialog" aria-label={label} title={label} onClick={onClick}>
      <ArrowUpDown aria-hidden /> <span className="tool-btn__label">{t("Sort")}</span>
    </button>
  );
}

/**
 * What to sort by and in which direction, in a sheet, applied together. Choosing what to sort by starts from its
 * natural direction (most points first, newest first…); the direction can then be turned round. "Date taken down" is
 * only offered for the boulders taken down.
 */
export function SortSheet({ value, removed, onApply, onClose }: { value: BoulderSort; removed: boolean; onApply: (s: BoulderSort) => void; onClose: () => void }) {
  const start = sortFor(value, removed);
  const [by, setBy] = useState<BoulderSortKey>(start.sort);
  const [dir, setDir] = useState<SortDirection>(start.order ?? NATURAL[start.sort]);
  const choose = (key: BoulderSortKey) => { setBy(key); setDir(NATURAL[key]); };
  return (
    <BottomSheet title={t("Sort by")} onClose={onClose} footer={
      <button type="button" className="btn btn--primary btn--block" onClick={() => { onApply({ sort: by, order: dir === NATURAL[by] ? undefined : dir }); onClose(); }}>
        {t("Apply")}
      </button>
    }>
      <div className="radio-list" role="radiogroup" aria-label={t("Sort by")}>
        {SORTS.filter((s) => s !== "REMOVED" || removed).map((s) => (
          <button key={s} type="button" role="radio" aria-checked={by === s} className="radio-row" onClick={() => choose(s)}>
            <span className="radio-row__dot" aria-hidden /> {t(SORT_LABEL[s])}
          </button>
        ))}
      </div>
      <div className="radio-list" role="radiogroup" aria-label={t("Direction")}>
        <p className="field__label">{t("Direction")}</p>
        {(["ASC", "DESC"] as const).map((d) => (
          <button key={d} type="button" role="radio" aria-checked={dir === d} className="radio-row" onClick={() => setDir(d)}>
            <span className="radio-row__dot" aria-hidden /> {d === "ASC" ? t("Ascending") : t("Descending")}
            <span className="radio-row__hint">{directionHint(by, d)}</span>
          </button>
        ))}
      </div>
    </BottomSheet>
  );
}

/** What each direction means for each order, said plainly beside it. */
function directionHint(by: BoulderSortKey, dir: SortDirection): string {
  switch (by) {
    case "SECTOR": return dir === "ASC" ? t("in the gym's order") : t("in reverse order");
    case "POINTS": return dir === "ASC" ? t("fewest points first") : t("most points first");
    case "GRADE": return dir === "ASC" ? t("easiest first") : t("hardest first");
    default: return dir === "ASC" ? t("oldest first") : t("newest first");
  }
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
