import { useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, Camera, ImagePlus, Save } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useSectors } from "@/features/gyms/api";
import { useGradeSystems } from "@/features/grading/api";
import { useStaff } from "@/features/staff/api";
import { uploadBoulderPhoto, useBoulder, useCreateBoulder, useUpdateBoulder, type SaveBoulderInput } from "@/features/boulders/api";
import { HOLD_COLORS, type HoldColor } from "@/features/boulders/holdColors";
import { SelectField } from "@/components/Fields";
import { Button } from "@/components/Button";
import { ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";

export function BoulderEditorPage() {
  const { boulderId } = useParams();
  const existing = useBoulder(boulderId);
  if (boulderId && existing.isPending) return <LoadingState label="Loading boulder" />;
  if (boulderId && existing.isError) return <ErrorState error={existing.error} onRetry={() => existing.refetch()} />;
  return <BoulderEditor key={boulderId ?? "new"} initial={existing.data} />;
}

type Stage = "idle" | "processing" | "uploading" | "saving";

function BoulderEditor({ initial }: { initial?: ReturnType<typeof useBoulder>["data"] }) {
  const { gym } = useManagedGym();
  const navigate = useNavigate();
  const toast = useToast();
  const sectors = useSectors(gym.id);
  const systems = useGradeSystems(gym.id);
  const staff = useStaff(gym.id);
  const create = useCreateBoulder(gym.id);
  const update = useUpdateBoulder(initial?.id ?? "");
  const fileInput = useRef<HTMLInputElement>(null);

  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(initial?.photoUrl ?? null);
  const [sectorId, setSectorId] = useState(initial?.sectorId ?? "");
  const [holdColor, setHoldColor] = useState<HoldColor | "">(initial?.holdColor ?? "");
  const [grades, setGrades] = useState<Record<string, string>>(() =>
    Object.fromEntries((initial?.grades ?? []).map((g) => [g.gradeSystemId, g.gradeValueId])));
  const [setterUserId, setSetterUserId] = useState(initial?.setter?.userId ?? "");
  const [stage, setStage] = useState<Stage>("idle");
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => () => { if (preview?.startsWith("blob:")) URL.revokeObjectURL(preview); }, [preview]);

  const activeSystems = useMemo(() => (systems.data ?? []).filter((s) => s.isActive), [systems.data]);
  const activeSectors = (sectors.data ?? []).filter((s) => s.isActive || s.id === initial?.sectorId);

  function pickFile(f: File | undefined) {
    if (!f) return;
    setFile(f);
    setPreview(URL.createObjectURL(f));
    setErrors((e) => ({ ...e, photoPath: "" }));
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    const local: Record<string, string> = {};
    if (!file && !initial) local.photoPath = "Add a photo of the boulder.";
    if (!sectorId) local.sectorId = "Choose a sector.";
    if (!holdColor) local.holdColor = "Choose the hold colour.";
    if (!Object.values(grades).some(Boolean)) local.grades = "Give the boulder at least one official grade.";
    setErrors(local);
    if (Object.keys(local).length) return;

    try {
      const photoPath = file ? await uploadBoulderPhoto(gym.id, file, setStage) : initial!.photoPath;
      setStage("saving");
      const input: SaveBoulderInput = {
        sectorId, photoPath, holdColor: holdColor as HoldColor, setterUserId: setterUserId || null,
        grades: Object.entries(grades).filter(([, v]) => v).map(([gradeSystemId, gradeValueId]) => ({ gradeSystemId, gradeValueId })),
      };
      if (initial) {
        await update.mutateAsync(input);
        toast.success("Boulder updated");
      } else {
        await create.mutateAsync(input);
        toast.success("Boulder added");
      }
      navigate(`/manage/${gym.slug}/boulders`);
    } catch (err) {
      if (err instanceof ApiError && err.isValidation) {
        setErrors(Object.fromEntries(Object.keys(err.fieldErrors).map((k) => [k === "photopath" ? "photoPath" : k === "sectorid" ? "sectorId" : k === "holdcolor" ? "holdColor" : k === "setteruserid" ? "setterUserId" : k, err.fieldErrors[k]![0]!])));
      } else {
        toast.error(errorMessage(err));
      }
    } finally {
      setStage("idle");
    }
  }

  if (sectors.isPending || systems.isPending) return <LoadingState />;

  const busy = stage !== "idle";
  const busyLabel = stage === "processing" ? "Preparing photo…" : stage === "uploading" ? "Uploading photo…" : stage === "saving" ? "Saving…" : "";

  return (
    <form className="editor" onSubmit={onSubmit} noValidate>
      <div className="editor__head">
        <Link to={`/manage/${gym.slug}/boulders`} className="manage-head__back" aria-label="Back to boulders"><ArrowLeft aria-hidden /></Link>
        <h2 className="section__title">{initial ? "Edit boulder" : "New boulder"}</h2>
      </div>
      {initial && <p className="field__hint">Fix mistakes here. If the boulder was retraced, remove it and add a new one instead, so climbers' history stays accurate.</p>}

      {/* Photo */}
      <section className="editor__section" aria-labelledby="photo-label">
        <p id="photo-label" className="field__label">Photo *</p>
        <button type="button" className={`photo-picker ${preview ? "has-photo" : ""} ${errors.photoPath ? "is-invalid" : ""}`} onClick={() => fileInput.current?.click()}>
          {preview ? <img src={preview} alt="Selected boulder" /> : (
            <span className="photo-picker__empty"><Camera aria-hidden /><span>Take or choose a photo</span></span>
          )}
          {preview && <span className="photo-picker__change"><ImagePlus aria-hidden /> Change</span>}
        </button>
        <input ref={fileInput} type="file" accept="image/jpeg,image/png,image/webp,image/*" capture="environment" hidden onChange={(e) => pickFile(e.target.files?.[0])} />
        {errors.photoPath && <p className="field__error">{errors.photoPath}</p>}
      </section>

      <SelectField label="Sector *" value={sectorId} onChange={(e) => setSectorId(e.target.value)} error={errors.sectorId}
        options={[{ value: "", label: "Choose a sector" }, ...activeSectors.map((s) => ({ value: s.id, label: s.name }))]} />

      {/* Official grades — one control per active system */}
      <section className="editor__section" aria-labelledby="grades-label">
        <p id="grades-label" className="field__label">Official grade *</p>
        <div className="form__grid">
          {activeSystems.map((system) => (
            <SelectField key={system.id} label={system.name} value={grades[system.id] ?? ""}
              onChange={(e) => setGrades((g) => ({ ...g, [system.id]: e.target.value }))}
              options={[{ value: "", label: "Not graded" }, ...system.values.filter((v) => v.isActive || v.id === grades[system.id]).map((v) => ({ value: v.id, label: v.label }))]} />
          ))}
        </div>
        {errors.grades && <p className="field__error">{errors.grades}</p>}
      </section>

      {/* Hold colour — deliberately a different control type from grades */}
      <fieldset className="editor__section hold-picker" aria-describedby={errors.holdColor ? "hold-error" : undefined}>
        <legend className="field__label">Hold colour *</legend>
        <p className="field__hint">The colour of the physical holds, not the grade.</p>
        <div className="hold-picker__grid">
          {HOLD_COLORS.map((c) => (
            <label key={c.value} className={`hold-option ${holdColor === c.value ? "is-selected" : ""}`}>
              <input type="radio" name="holdColor" value={c.value} checked={holdColor === c.value} onChange={() => setHoldColor(c.value)} />
              <span className="holds__swatch" style={{ background: c.hex }} aria-hidden />
              <span>{c.label}</span>
            </label>
          ))}
        </div>
        {errors.holdColor && <p id="hold-error" className="field__error">{errors.holdColor}</p>}
      </fieldset>

      <SelectField label="Setter" value={setterUserId} onChange={(e) => setSetterUserId(e.target.value)} error={errors.setterUserId}
        options={[{ value: "", label: "Not specified" }, ...(staff.data ?? []).map((m) => ({ value: m.userId, label: m.displayName }))]} />

      <div className="editor__submit">
        <Button type="submit" block icon={<Save aria-hidden />} loading={busy}>{busy ? busyLabel : initial ? "Save changes" : "Add boulder"}</Button>
      </div>
    </form>
  );
}
