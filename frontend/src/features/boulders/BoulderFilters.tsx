import type { Sector } from "@/features/gyms/api";
import type { GradeSystem } from "@/features/grading/api";
import type { BoulderFilters as Filters, ProgressFilter } from "@/features/boulders/api";
import { useAuth } from "@/auth/AuthProvider";
import { holdLabel, HOLD_COLORS, type HoldColor } from "@/features/boulders/holdColors";
import { SelectField } from "@/components/Fields";
import { t } from "@/i18n/i18n";
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
