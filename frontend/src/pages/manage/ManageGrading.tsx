import { useState, type FormEvent } from "react";
import { ArrowDown, ArrowUp, Eye, EyeOff, Plus, Ruler, Save, Trash2, X } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { gradeSystemTypeLabel, useCreateGradeSystem, useGradeSystems, useSetGradeValues, useUpdateGradeSystem, type GradeSystem, type GradeSystemType } from "@/features/grading/api";
import { atLeast } from "@/features/staff/roles";
import { inkOn } from "@/features/boulders/holdColors";
import { SelectField } from "@/components/Fields";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { Badge } from "@/components/Badge";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";

export function ManageGrading() {
  const { gym, role } = useManagedGym();
  const systems = useGradeSystems(gym.id);
  const canEdit = atLeast(role, "ADMIN");

  if (systems.isPending) return <LoadingState label="Loading grading" />;
  if (systems.isError) return <ErrorState error={systems.error} onRetry={() => systems.refetch()} />;

  return (
    <div className="stack">
      <p className="field__hint">Boulders get one official grade per active system. Colour grades describe difficulty only — hold colour is set separately on each boulder.</p>
      {canEdit && <AddSystem gymId={gym.id} existing={systems.data} />}
      {systems.data.length === 0
        ? <EmptyState icon={<Ruler />} title="No grading systems yet" body={canEdit ? "Add the grading your gym uses. You can run more than one." : "Ask a gym admin to set up grading."} />
        : systems.data.map((s) => <SystemCard key={s.id} gymId={gym.id} system={s} canEdit={canEdit} />)}
    </div>
  );
}

function AddSystem({ gymId, existing }: { gymId: string; existing: GradeSystem[] }) {
  const create = useCreateGradeSystem(gymId);
  const toast = useToast();
  const [type, setType] = useState<GradeSystemType>(existing.some((s) => s.type === "FONTAINEBLEAU") ? "V_SCALE" : "FONTAINEBLEAU");

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    const values = type === "CUSTOM" ? [{ label: "Easy" }, { label: "Medium" }, { label: "Hard" }] : undefined;
    create.mutate({ type, values }, {
      onSuccess: (s) => toast.success(`${s.name} added`),
      onError: (err) => toast.error(errorMessage(err)),
    });
  }

  return (
    <form className="inline-form card" onSubmit={onSubmit}>
      <SelectField label="Add a grading system" value={type} onChange={(e) => setType(e.target.value as GradeSystemType)}
        options={(Object.keys(gradeSystemTypeLabel) as GradeSystemType[]).map((t) => ({ value: t, label: gradeSystemTypeLabel[t] }))} />
      <Button type="submit" icon={<Plus aria-hidden />} loading={create.isPending}>Add</Button>
    </form>
  );
}

interface Row { id?: string; label: string; colorHex: string }

function SystemCard({ gymId, system, canEdit }: { gymId: string; system: GradeSystem; canEdit: boolean }) {
  const update = useUpdateGradeSystem(gymId);
  const setValues = useSetGradeValues(gymId);
  const toast = useToast();
  const [editing, setEditing] = useState(false);
  const active = system.values.filter((v) => v.isActive);
  const [rows, setRows] = useState<Row[]>([]);
  const isColor = system.type === "COLOR";

  function startEditing() {
    setRows(active.map((v) => ({ id: v.id, label: v.label, colorHex: v.colorHex ?? "#888888" })));
    setEditing(true);
  }
  const move = (i: number, d: -1 | 1) => setRows((r) => {
    const n = [...r]; const j = i + d;
    if (j < 0 || j >= n.length) return r;
    [n[i], n[j]] = [n[j]!, n[i]!];
    return n;
  });

  function save() {
    setValues.mutate({ id: system.id, values: rows.map((r) => ({ id: r.id, label: r.label.trim(), colorHex: isColor ? r.colorHex : null })) }, {
      onSuccess: () => { setEditing(false); toast.success("Grades saved"); },
      onError: (err) => toast.error(err instanceof ApiError && err.fieldError("values") ? err.fieldError("values")! : errorMessage(err)),
    });
  }

  return (
    <article className={`card grading ${system.isActive ? "" : "grading--inactive"}`}>
      <header className="candidate__head">
        <div>
          <h3 className="list__title">{system.name}</h3>
          <p className="list__sub">{gradeSystemTypeLabel[system.type]} · {active.length} grades · easiest first</p>
        </div>
        <div className="form__actions">
          {!system.isActive && <Badge>Hidden</Badge>}
          {canEdit && (
            <button type="button" className="icon-btn" disabled={update.isPending}
              aria-label={system.isActive ? `Hide ${system.name}` : `Show ${system.name}`}
              onClick={() => update.mutate({ id: system.id, isActive: !system.isActive }, { onError: (e) => toast.error(errorMessage(e)) })}>
              {system.isActive ? <EyeOff aria-hidden /> : <Eye aria-hidden />}
            </button>
          )}
        </div>
      </header>

      {!editing ? (
        <>
          <div className="grade-scale">
            {active.map((v) => isColor && v.colorHex
              ? <span key={v.id} className="grade grade--color grade--sm" style={{ background: v.colorHex, color: inkOn(v.colorHex) }}>{v.label}</span>
              : <span key={v.id} className="grade grade--text grade--sm">{v.label}</span>)}
          </div>
          {canEdit && <Button variant="secondary" onClick={startEditing}>Edit grades</Button>}
        </>
      ) : (
        <div className="stack">
          <ol className="grade-rows">
            {rows.map((r, i) => (
              <li key={r.id ?? `new-${i}`} className="grade-row">
                <div className="reorder">
                  <button type="button" className="icon-btn" onClick={() => move(i, -1)} disabled={i === 0} aria-label="Easier"><ArrowUp aria-hidden /></button>
                  <button type="button" className="icon-btn" onClick={() => move(i, 1)} disabled={i === rows.length - 1} aria-label="Harder"><ArrowDown aria-hidden /></button>
                </div>
                <TextField label={`Grade ${i + 1}`} value={r.label} maxLength={20}
                  onChange={(e) => setRows((rs) => rs.map((x, k) => (k === i ? { ...x, label: e.target.value } : x)))} />
                {isColor && (
                  <label className="color-input">
                    <span className="sr-only">Grade colour for {r.label}</span>
                    <input type="color" value={r.colorHex} onChange={(e) => setRows((rs) => rs.map((x, k) => (k === i ? { ...x, colorHex: e.target.value } : x)))} />
                  </label>
                )}
                <button type="button" className="icon-btn" onClick={() => setRows((rs) => rs.filter((_, k) => k !== i))} aria-label={`Retire ${r.label}`}><Trash2 aria-hidden /></button>
              </li>
            ))}
          </ol>
          <p className="field__hint">Retired grades stay on existing boulders but can't be used for new ones.</p>
          <div className="form__actions">
            <Button variant="secondary" icon={<Plus aria-hidden />} onClick={() => setRows((rs) => [...rs, { label: "", colorHex: "#888888" }])}>Add grade</Button>
            <Button icon={<Save aria-hidden />} loading={setValues.isPending} onClick={save}>Save</Button>
            <Button variant="ghost" icon={<X aria-hidden />} onClick={() => setEditing(false)}>Cancel</Button>
          </div>
        </div>
      )}
    </article>
  );
}
