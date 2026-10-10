import type { Sector } from "@/features/gyms/api";
import type { GradeSystem } from "@/features/grading/api";
import type { BoulderFilters as Filters, ProgressFilter } from "@/features/boulders/api";
import { useAuth } from "@/auth/AuthProvider";
import { holdLabel, HOLD_COLORS, type HoldColor } from "@/features/boulders/holdColors";
import { ChevronDown, SlidersHorizontal, X } from "lucide-react";
import { SelectField } from "@/components/Fields";
import { plural, t } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";

interface Props {
  filters: Filters;
  onChange: (f: Filters) => void;
  sectors: Sector[];
  systems: GradeSystem[];
}

/** Sector, grade and hold-colour filters. Grade and hold colour are separate controls with separate labels. */
export function BoulderFiltersBar({ filters, onChange, sectors, systems }: Props) {
  const { session } = useAuth();
  const set = (patch: Partial<Filters>) => onChange({ ...filters, ...patch });
  const gradeOptions = [{ value: "", label: t("Any grade") }, ...systems.flatMap((s) =>
    s.values.filter((v) => v.isActive).map((v) => ({ value: v.id, label: systems.length > 1 ? `${dataLabel(s.name)}: ${dataLabel(v.label)}` : dataLabel(v.label) })))];

  return (
    <div className="boulder-filters">
      <SelectField label={t("Sector")} value={filters.sectorId ?? ""} onChange={(e) => set({ sectorId: e.target.value || undefined })}
        options={[{ value: "", label: t("All sectors") }, ...sectors.filter((s) => s.isActive).map((s) => ({ value: s.id, label: s.name }))]} />
      <SelectField label={t("Grade")} value={filters.gradeValueId ?? ""} onChange={(e) => set({ gradeValueId: e.target.value || undefined })} options={gradeOptions} />
      <SelectField label={t("Hold colour")} value={filters.holdColor ?? ""} onChange={(e) => set({ holdColor: (e.target.value || undefined) as HoldColor | undefined })}
        options={[{ value: "", label: t("Any holds") }, ...HOLD_COLORS.map((c) => ({ value: c.value, label: holdLabel(c.value) }))]} />
      {session && (
        <SelectField label={t("Your progress")} value={filters.progress ?? ""} onChange={(e) => set({ progress: (e.target.value || undefined) as ProgressFilter | undefined })}
          options={[{ value: "", label: t("All") }, { value: "UNTRIED", label: t("Not tried") }, { value: "PROJECTS", label: t("Projects") }, { value: "COMPLETED", label: t("Completed") }]} />
      )}
      <SelectField label={t("Rating")} value={filters.minRating ?? ""} onChange={(e) => set({ minRating: e.target.value || undefined })}
        options={[{ value: "", label: t("Any rating") }, { value: "3", label: t("3+ stars") }, { value: "4", label: t("4+ stars") }]} />
    </div>
  );
}

type Narrowing = Exclude<keyof Filters, "status">;

/** The filters in use, each with its own label, so they can be read and removed one by one. */
export function activeFilters(f: Filters, sectors: Sector[], systems: GradeSystem[]): { key: Narrowing; label: string }[] {
  const out: { key: Narrowing; label: string }[] = [];
  if (f.sectorId) out.push({ key: "sectorId", label: sectors.find((s) => s.id === f.sectorId)?.name ?? t("Sector") });
  if (f.gradeValueId) {
    const value = systems.flatMap((s) => s.values).find((v) => v.id === f.gradeValueId);
    out.push({ key: "gradeValueId", label: value ? dataLabel(value.label) : t("Grade") });
  }
  if (f.holdColor) out.push({ key: "holdColor", label: holdLabel(f.holdColor) });
  if (f.progress) out.push({ key: "progress", label: f.progress === "UNTRIED" ? t("Not tried") : f.progress === "PROJECTS" ? t("Projects") : t("Completed") });
  if (f.minRating) out.push({ key: "minRating", label: f.minRating === "4" ? t("4+ stars") : t("3+ stars") });
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

/** The filters when opened, then the ones in use as chips that remove them (shown open or closed). */
export function FiltersPanel({ open, filters, onChange, sectors, systems }: Props & { open: boolean }) {
  const active = activeFilters(filters, sectors, systems);
  return (
    <>
      {open && (
        <div id="boulder-filters" className="filter-panel">
          <BoulderFiltersBar filters={filters} onChange={(f) => onChange({ ...f, status: filters.status })} sectors={sectors} systems={systems} />
        </div>
      )}
      {active.length > 0 && (
        <ul className="active-filters" aria-label={t("Filters in use")}>
          {active.map((a) => (
            <li key={a.key}>
              <button type="button" className="active-filter" onClick={() => onChange({ ...filters, [a.key]: undefined })}
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
