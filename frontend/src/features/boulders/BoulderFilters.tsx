import type { Sector } from "@/features/gyms/api";
import type { GradeSystem } from "@/features/grading/api";
import type { BoulderFilters as Filters, ProgressFilter } from "@/features/boulders/api";
import { useAuth } from "@/auth/AuthProvider";
import { holdLabel, HOLD_COLORS, type HoldColor } from "@/features/boulders/holdColors";
import { useEffect, useState } from "react";
import { ChevronDown, Search, SlidersHorizontal, X } from "lucide-react";
import { SearchField } from "@/components/SearchField";
import { SelectField } from "@/components/Fields";
import { plural, t } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";
import { INTENSITIES, intensityLabel, type Intensity } from "@/features/boulders/intensity";
import type { GymTag } from "@/features/boulders/tags";

interface Props {
  filters: Filters;
  onChange: (f: Filters) => void;
  sectors: Sector[];
  systems: GradeSystem[];
  /** The gym's tags; the tag filter only shows when there are some. */
  tags?: GymTag[];
}

/** Adds or takes away one value of a multi-choice filter; an empty choice is no filter at all. */
function toggled<T>(list: T[] | undefined, value: T): T[] | undefined {
  const next = list?.includes(value) ? list.filter((x) => x !== value) : [...(list ?? []), value];
  return next.length ? next : undefined;
}

/** Sector, grade and the climber's own progress up front; everything else behind "Advanced search". */
export function BoulderFiltersBar({ filters, onChange, sectors, systems, tags = [] }: Props) {
  const { session } = useAuth();
  const set = (patch: Partial<Filters>) => onChange({ ...filters, ...patch });
  const gradeOptions = [{ value: "", label: t("Any grade") }, ...systems.flatMap((s) =>
    s.values.filter((v) => v.isActive).map((v) => ({ value: v.id, label: systems.length > 1 ? `${dataLabel(s.name)}: ${dataLabel(v.label)}` : dataLabel(v.label) })))];

  return (
    <div className="boulder-filters">
      <SelectField label={t("Sector")} value={filters.sectorId ?? ""} onChange={(e) => set({ sectorId: e.target.value || undefined })}
        options={[{ value: "", label: t("All sectors") }, ...sectors.filter((s) => s.isActive).map((s) => ({ value: s.id, label: s.name }))]} />
      <SelectField label={t("Grade")} value={filters.gradeValueId ?? ""} onChange={(e) => set({ gradeValueId: e.target.value || undefined })} options={gradeOptions} />
      {session && (
        <SelectField label={t("Your progress")} value={filters.progress ?? ""} onChange={(e) => set({ progress: (e.target.value || undefined) as ProgressFilter | undefined })}
          options={[{ value: "", label: t("All") }, { value: "UNTRIED", label: t("Not tried") }, { value: "PROJECTS", label: t("Projects") }, { value: "COMPLETED", label: t("Completed") }]} />
      )}
      <AdvancedSearch filters={filters} set={set} tags={tags} />
    </div>
  );
}

/**
 * Name, hold colour, rating, intensity and tags, behind an "Advanced search" button: most people filter by sector and
 * grade, so these stay out of the way until asked for. Already in use, they open by themselves.
 */
function AdvancedSearch({ filters, set, tags }: { filters: Filters; set: (patch: Partial<Filters>) => void; tags: GymTag[] }) {
  const [open, setOpen] = useState(() => !!(filters.name || filters.holdColor || filters.minRating || filters.intensity?.length || filters.tagIds?.length));
  const [name, setName] = useState(filters.name ?? "");
  // The list is asked for once typing pauses, not at every letter.
  useEffect(() => {
    const next = name.trim() || undefined;
    if (next === filters.name) return;
    const id = window.setTimeout(() => set({ name: next }), 350);
    return () => window.clearTimeout(id);
  }, [name]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => { if (!filters.name) setName(""); }, [filters.name]);

  return (
    <div className="advanced-search">
      <button type="button" className="text-btn advanced-search__toggle" aria-expanded={open} aria-controls="advanced-search" onClick={() => setOpen((v) => !v)}>
        <Search aria-hidden /> {t("Advanced search")} <ChevronDown aria-hidden className={open ? "is-open" : ""} />
      </button>
      {open && (
        <div id="advanced-search" className="advanced-search__body">
          <SearchField label={t("Boulder name")} placeholder={t("e.g. La Fessura")} value={name} onChange={setName} />
          <div className="boulder-filters">
            <SelectField label={t("Hold colour")} value={filters.holdColor ?? ""} onChange={(e) => set({ holdColor: (e.target.value || undefined) as HoldColor | undefined })}
              options={[{ value: "", label: t("Any holds") }, ...HOLD_COLORS.map((c) => ({ value: c.value, label: holdLabel(c.value) }))]} />
            <SelectField label={t("Rating")} value={filters.minRating ?? ""} onChange={(e) => set({ minRating: e.target.value || undefined })}
              options={[{ value: "", label: t("Any rating") }, { value: "3", label: t("3+ stars") }, { value: "4", label: t("4+ stars") }]} />
            {/* One level at a time: the server still takes several, for links made before this was a drop-down. */}
            <SelectField label={t("Intensity")} value={filters.intensity?.length === 1 ? filters.intensity[0] : ""}
              onChange={(e) => set({ intensity: e.target.value ? [e.target.value as Intensity] : undefined })}
              options={[{ value: "", label: t("Any intensity") }, ...INTENSITIES.map((i) => ({ value: i, label: intensityLabel(i) }))]} />
          </div>
          {tags.length > 0 && (
            <fieldset className="filter-group">
              <legend className="field__label">{t("Tags")}</legend>
              <div className="chips chips--wrap">
                {tags.map((tag) => (
                  <button key={tag.id} type="button" className="chip" aria-pressed={!!filters.tagIds?.includes(tag.id)} onClick={() => set({ tagIds: toggled(filters.tagIds, tag.id) })}>
                    {tag.name}
                  </button>
                ))}
              </div>
            </fieldset>
          )}
        </div>
      )}
    </div>
  );
}

/** One filter in use: its label, and how to take just that one away (a tag among several goes alone). */
export interface ActiveFilter { id: string; label: string; clear: (f: Filters) => Filters }

/** The filters in use, each with its own label, so they can be read and removed one by one. */
export function activeFilters(f: Filters, sectors: Sector[], systems: GradeSystem[], tags: GymTag[] = []): ActiveFilter[] {
  const out: ActiveFilter[] = [];
  const drop = (key: keyof Filters) => (x: Filters) => ({ ...x, [key]: undefined });
  if (f.sectorId) out.push({ id: "sectorId", label: sectors.find((s) => s.id === f.sectorId)?.name ?? t("Sector"), clear: drop("sectorId") });
  if (f.gradeValueId) {
    const value = systems.flatMap((s) => s.values).find((v) => v.id === f.gradeValueId);
    out.push({ id: "gradeValueId", label: value ? dataLabel(value.label) : t("Grade"), clear: drop("gradeValueId") });
  }
  if (f.holdColor) out.push({ id: "holdColor", label: holdLabel(f.holdColor), clear: drop("holdColor") });
  if (f.progress) out.push({ id: "progress", label: f.progress === "UNTRIED" ? t("Not tried") : f.progress === "PROJECTS" ? t("Projects") : t("Completed"), clear: drop("progress") });
  if (f.minRating) out.push({ id: "minRating", label: f.minRating === "4" ? t("4+ stars") : t("3+ stars"), clear: drop("minRating") });
  for (const i of f.intensity ?? [])
    out.push({ id: `intensity-${i}`, label: intensityLabel(i as Intensity), clear: (x) => ({ ...x, intensity: toggled(x.intensity, i) }) });
  if (f.name) out.push({ id: "name", label: t("Name: {name}", { name: f.name }), clear: drop("name") });
  for (const id of f.tagIds ?? [])
    out.push({ id: `tag-${id}`, label: tags.find((tag) => tag.id === id)?.name ?? t("Tag"), clear: (x) => ({ ...x, tagIds: toggled(x.tagIds, id) }) });
  return out;
}

/** The "Filters" button: the filters wait behind it, and it counts the ones in use. */
export function FiltersToggle({ open, count, onToggle }: { open: boolean; count: number; onToggle: () => void }) {
  return (
    <button type="button" className={`chip filter-toggle ${count ? "is-active" : ""}`} aria-expanded={open} aria-controls="boulder-filters" onClick={onToggle}>
      <SlidersHorizontal aria-hidden /> {t("Filters")}{count > 0 && <span className="filter-toggle__count">{count}</span>}
    </button>
  );
}

/**
 * The filters when opened, then the ones in use as chips that remove them (shown open or closed). With `withStatus`
 * the panel also chooses between the boulders on the wall and those taken down, at its top (a view more than a
 * filter, so "clear" keeps it), and offers to clear the filters.
 */
export function FiltersPanel({ open, filters, onChange, sectors, systems, tags = [], withStatus = false }: Props & { open: boolean; withStatus?: boolean }) {
  const active = activeFilters(filters, sectors, systems, tags);
  const removed = filters.status === "REMOVED";
  return (
    <>
      {open && (
        <div id="boulder-filters" className="filter-panel">
          {withStatus && (
            <div className="segmented" role="radiogroup" aria-label={t("Boulder status")}>
              <button type="button" role="radio" aria-checked={!removed} className="segmented__option" onClick={() => onChange({ ...filters, status: undefined })}>{t("On the wall")}</button>
              <button type="button" role="radio" aria-checked={removed} className="segmented__option" onClick={() => onChange({ ...filters, status: "REMOVED" })}>{t("Taken down")}</button>
            </div>
          )}
          <BoulderFiltersBar filters={filters} onChange={(f) => onChange({ ...f, status: filters.status })} sectors={sectors} systems={systems} tags={tags} />
          {withStatus && active.length > 0 && (
            <button type="button" className="text-btn filter-panel__reset" onClick={() => onChange({ status: filters.status })}>{t("Clear filters")}</button>
          )}
        </div>
      )}
      {active.length > 0 && (
        <ul className="active-filters" aria-label={t("Filters in use")}>
          {active.map((a) => (
            <li key={a.id}>
              <button type="button" className="active-filter" onClick={() => onChange(a.clear(filters))}
                aria-label={t("Remove filter {name}", { name: a.label })}>
                {a.label} <X aria-hidden />
              </button>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}

/**
 * On the wall or taken down: a compact drop-down beside the filters rather than two buttons of its own. It is a view,
 * not a filter — an empty "taken down" list means nothing has come off the wall yet.
 */
export function BoulderStatusSelect({ removed, onChange }: { removed: boolean; onChange: (removed: boolean) => void }) {
  return (
    <label className="chip-select">
      <span className="sr-only">{t("Boulder status")}</span>
      <select value={removed ? "REMOVED" : "ACTIVE"} onChange={(e) => onChange(e.target.value === "REMOVED")}>
        <option value="ACTIVE">{t("On the wall")}</option>
        <option value="REMOVED">{t("Taken down")}</option>
      </select>
      <ChevronDown aria-hidden />
    </label>
  );
}

/** "18 on the wall", "1 taken down". */
export const boulderCount = (total: number, removed: boolean) =>
  removed ? plural(total, "1 taken down", "{count} taken down") : plural(total, "1 on the wall", "{count} on the wall");
