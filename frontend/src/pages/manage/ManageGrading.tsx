import { useState, type FormEvent } from "react";
import { ArrowDown, ArrowUp, Eye, EyeOff, Plus, Ruler, Save, Trash2, X } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { automaticPoints, gradeSystemTypeLabel, useCreateGradeSystem, useGradeSystems, useSetGradeValues, useUpdateGradeSystem, type GradeSystem, type GradeSystemType } from "@/features/grading/api";
import { atLeast } from "@/features/staff/roles";
import { ColorGrade } from "@/features/boulders/BoulderBits";
import { SelectField } from "@/components/Fields";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { Badge } from "@/components/Badge";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { plural, t } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";

export function ManageGrading() {
  const { gym, role } = useManagedGym();
  const systems = useGradeSystems(gym.id);
  const canEdit = atLeast(role, "ADMIN");

  if (systems.isPending) return <LoadingState label={t("Loading grading")} />;
  if (systems.isError) return <ErrorState error={systems.error} onRetry={() => systems.refetch()} />;

  return (
    <div className="stack">
      <p className="field__hint">{t("Boulders get one official grade per active system. Colour grades describe difficulty only — hold colour is set separately on each boulder.")}</p>
      {canEdit && <AddSystem gymId={gym.id} existing={systems.data} />}
      {systems.data.length === 0
        ? <EmptyState icon={<Ruler />} title={t("No grading systems yet")} body={canEdit ? t("Add the grading your gym uses. You can run more than one.") : t("Ask a gym admin to set up grading.")} />
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
    const values = type === "CUSTOM" ? [{ label: t("Easy") }, { label: t("Medium") }, { label: t("Hard") }] : undefined;
    create.mutate({ type, values }, {
      onSuccess: (s) => toast.success(t("{name} added", { name: s.name })),
      onError: (err) => toast.error(errorMessage(err)),
    });
  }

  return (
    <form className="inline-form card" onSubmit={onSubmit}>
      <SelectField label={t("Add a grading system")} value={type} onChange={(e) => setType(e.target.value as GradeSystemType)}
        options={(Object.keys(gradeSystemTypeLabel) as GradeSystemType[]).map((t) => ({ value: t, label: gradeSystemTypeLabel[t] }))} />
      <Button type="submit" icon={<Plus aria-hidden />} loading={create.isPending}>{t("Add")}</Button>
    </form>
  );
}

/** `colorHex2` null: one colour. `points` as typed; empty means automatic. */
interface Row { id?: string; label: string; colorHex: string; colorHex2: string | null; points: string }

const validPoints = (text: string) => text.trim() === "" || (/^\d+$/.test(text.trim()) && Number(text) >= 1 && Number(text) <= 1000);

function SystemCard({ gymId, system, canEdit }: { gymId: string; system: GradeSystem; canEdit: boolean }) {
  const update = useUpdateGradeSystem(gymId);
  const setValues = useSetGradeValues(gymId);
  const toast = useToast();
  const [editing, setEditing] = useState(false);
  const active = system.values.filter((v) => v.isActive);
  const [rows, setRows] = useState<Row[]>([]);
  const isColor = system.type === "COLOR";

  function startEditing() {
    setRows(active.map((v) => ({ id: v.id, label: v.label, colorHex: v.colorHex ?? "#888888", colorHex2: v.colorHex2 ?? null,
      points: v.pointsOverride != null ? String(v.pointsOverride) : "" })));
    setEditing(true);
  }
  const move = (i: number, d: -1 | 1) => setRows((r) => {
    const n = [...r]; const j = i + d;
    if (j < 0 || j >= n.length) return r;
    [n[i], n[j]] = [n[j]!, n[i]!];
    return n;
  });

  function save() {
    if (!rows.every((r) => validPoints(r.points))) { toast.error(t("Points go from 1 to {max}.", { max: 1000 })); return; }
    setValues.mutate({ id: system.id, values: rows.map((r) => ({
      id: r.id, label: r.label.trim(), colorHex: isColor ? r.colorHex : null,
      ...(isColor ? { colorHex2: r.colorHex2 ?? "" } : {}),
      points: r.points.trim() === "" ? 0 : Number(r.points),
    })) }, {
      onSuccess: () => { setEditing(false); toast.success(t("Grades saved")); },
      onError: (err) => toast.error(err instanceof ApiError && err.fieldError("values") ? err.fieldError("values")! : errorMessage(err)),
    });
  }

  return (
    <article className={`card grading ${system.isActive ? "" : "grading--inactive"}`}>
      <header className="candidate__head">
        <div>
          <h3 className="list__title">{dataLabel(system.name)}</h3>
          <p className="list__sub">{gradeSystemTypeLabel[system.type]} · {plural(active.length, "{count} grade", "{count} grades")} · {t("easiest first")}</p>
        </div>
        <div className="form__actions">
          {!system.isActive && <Badge>{t("Hidden")}</Badge>}
          {canEdit && (
            <button type="button" className="icon-btn" disabled={update.isPending}
              aria-label={system.isActive ? t("Hide {name}", { name: dataLabel(system.name) }) : t("Show {name}", { name: dataLabel(system.name) })}
              onClick={() => update.mutate({ id: system.id, isActive: !system.isActive }, { onError: (e) => toast.error(errorMessage(e)) })}>
              {system.isActive ? <EyeOff aria-hidden /> : <Eye aria-hidden />}
            </button>
          )}
        </div>
      </header>

      {!editing ? (
        <>
          {/* Each grade with what a send of it scores; a number set by hand says so. */}
          <ul className="grade-scale" aria-label={t("Grades and their points")}>
            {active.map((v) => (
              <li key={v.id} className="grade-scale__item">
                {isColor && v.colorHex
                  ? <ColorGrade label={v.label} colorHex={v.colorHex} colorHex2={v.colorHex2} size="sm" />
                  : <span className="grade grade--text grade--sm">{dataLabel(v.label)}</span>}
                {v.points != null && (
                  <span className={`grade-scale__points ${v.pointsOverride != null ? "is-set" : ""}`}
                    title={v.pointsOverride != null ? t("Set by hand (automatic: {points})", { points: v.automaticPoints ?? v.points }) : t("Automatic")}>
                    {t("{points} pt", { points: v.points })}{v.pointsOverride != null && <span className="sr-only"> {t("Set by hand (automatic: {points})", { points: v.automaticPoints ?? v.points })}</span>}
                  </span>
                )}
              </li>
            ))}
          </ul>
          {canEdit && <Button variant="secondary" onClick={startEditing}>{t("Edit grades")}</Button>}
        </>
      ) : (
        <div className="stack">
          <ol className="grade-rows">
            {rows.map((r, i) => (
              <li key={r.id ?? `new-${i}`} className="grade-row">
                <div className="reorder">
                  <button type="button" className="icon-btn" onClick={() => move(i, -1)} disabled={i === 0} aria-label={t("Easier")}><ArrowUp aria-hidden /></button>
                  <button type="button" className="icon-btn" onClick={() => move(i, 1)} disabled={i === rows.length - 1} aria-label={t("Harder")}><ArrowDown aria-hidden /></button>
                </div>
                <TextField label={t("Grade {number}", { number: i + 1 })} value={r.label} maxLength={20}
                  onChange={(e) => setRows((rs) => rs.map((x, k) => (k === i ? { ...x, label: e.target.value } : x)))} />
                {isColor && (
                  <span className="grade-row__colors">
                    <label className="color-input">
                      <span className="sr-only">{t("Grade colour for {grade}", { grade: r.label })}</span>
                      <input type="color" value={r.colorHex} onChange={(e) => setRows((rs) => rs.map((x, k) => (k === i ? { ...x, colorHex: e.target.value } : x)))} />
                    </label>
                    {r.colorHex2 !== null ? (
                      <>
                        <label className="color-input">
                          <span className="sr-only">{t("Second colour for {grade}", { grade: r.label })}</span>
                          <input type="color" value={r.colorHex2} onChange={(e) => setRows((rs) => rs.map((x, k) => (k === i ? { ...x, colorHex2: e.target.value } : x)))} />
                        </label>
                        <button type="button" className="icon-btn" onClick={() => setRows((rs) => rs.map((x, k) => (k === i ? { ...x, colorHex2: null } : x)))}
                          aria-label={t("One colour for {grade}", { grade: r.label })}><X aria-hidden /></button>
                      </>
                    ) : (
                      <button type="button" className="btn btn--ghost grade-row__second" onClick={() => setRows((rs) => rs.map((x, k) => (k === i ? { ...x, colorHex2: "#2F6FDB" } : x)))}
                        aria-label={t("Add a second colour to {grade}", { grade: r.label })}>{t("+ colour")}</button>
                    )}
                  </span>
                )}
                <label className="grade-row__points">
                  <span className="field__label">{t("Points")}</span>
                  <input className="field__input" inputMode="numeric" value={r.points} placeholder={String(automaticPoints(i, rows.length))}
                    aria-invalid={!validPoints(r.points) || undefined}
                    onChange={(e) => setRows((rs) => rs.map((x, k) => (k === i ? { ...x, points: e.target.value } : x)))} />
                </label>
                <button type="button" className="icon-btn" onClick={() => setRows((rs) => rs.filter((_, k) => k !== i))} aria-label={t("Retire {grade}", { grade: dataLabel(r.label) })}><Trash2 aria-hidden /></button>
              </li>
            ))}
          </ol>
          <p className="field__hint">{t("Retired grades stay on existing boulders but can't be used for new ones.")}</p>
          <p className="field__hint">{t("Leave the points empty to use the automatic ones (shown greyed). Points apply to every send, past ones included: leaderboards change as soon as you save.")}</p>
          <div className="form__actions">
            <Button variant="secondary" icon={<Plus aria-hidden />} onClick={() => setRows((rs) => [...rs, { label: "", colorHex: "#888888", colorHex2: null, points: "" }])}>{t("Add grade")}</Button>
            <Button icon={<Save aria-hidden />} loading={setValues.isPending} onClick={save}>{t("Save")}</Button>
            <Button variant="ghost" icon={<X aria-hidden />} onClick={() => setEditing(false)}>{t("Cancel")}</Button>
          </div>
        </div>
      )}
    </article>
  );
}
