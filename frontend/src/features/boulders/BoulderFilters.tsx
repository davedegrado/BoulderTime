import type { Sector } from "@/features/gyms/api";
import type { GradeSystem } from "@/features/grading/api";
import type { BoulderFilters as Filters, ProgressFilter } from "@/features/boulders/api";
import { useAuth } from "@/auth/AuthProvider";
import { HOLD_COLORS, type HoldColor } from "@/features/boulders/holdColors";
import { SelectField } from "@/components/Fields";

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
  const gradeOptions = [{ value: "", label: "Any grade" }, ...systems.flatMap((s) =>
    s.values.filter((v) => v.isActive).map((v) => ({ value: v.id, label: systems.length > 1 ? `${s.name}: ${v.label}` : v.label })))];

  return (
    <div className="boulder-filters">
      <SelectField label="Sector" value={filters.sectorId ?? ""} onChange={(e) => set({ sectorId: e.target.value || undefined })}
        options={[{ value: "", label: "All sectors" }, ...sectors.filter((s) => s.isActive).map((s) => ({ value: s.id, label: s.name }))]} />
      <SelectField label="Grade" value={filters.gradeValueId ?? ""} onChange={(e) => set({ gradeValueId: e.target.value || undefined })} options={gradeOptions} />
      <SelectField label="Hold colour" value={filters.holdColor ?? ""} onChange={(e) => set({ holdColor: (e.target.value || undefined) as HoldColor | undefined })}
        options={[{ value: "", label: "Any holds" }, ...HOLD_COLORS.map((c) => ({ value: c.value, label: `${c.label} holds` }))]} />
      {session && (
        <SelectField label="Your progress" value={filters.progress ?? ""} onChange={(e) => set({ progress: (e.target.value || undefined) as ProgressFilter | undefined })}
          options={[{ value: "", label: "All" }, { value: "UNTRIED", label: "Not tried" }, { value: "PROJECTS", label: "Projects" }, { value: "COMPLETED", label: "Completed" }]} />
      )}
      <SelectField label="Rating" value={filters.minRating ?? ""} onChange={(e) => set({ minRating: e.target.value || undefined })}
        options={[{ value: "", label: "Any rating" }, { value: "3", label: "3+ stars" }, { value: "4", label: "4+ stars" }]} />
    </div>
  );
}
