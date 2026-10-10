import { useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, Camera, ImagePlus, Save, Trash2 } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useSectors } from "@/features/gyms/api";
import { useGradeSystems } from "@/features/grading/api";
import { useStaff } from "@/features/staff/api";
import { uploadBoulderPhoto, useBoulder, useCreateBoulder, useRemoveBoulders, useUpdateBoulder, type SaveBoulderInput } from "@/features/boulders/api";
import { HOLD_COLORS, holdColorName, type HoldColor } from "@/features/boulders/holdColors";
import { SelectField } from "@/components/Fields";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";
import { MediaInput, type MediaInputHandle } from "@/components/MediaInput";
import { BetaEditor } from "@/features/community/BetaEditor";

export function BoulderEditorPage() {
  const { boulderId } = useParams();
  const existing = useBoulder(boulderId);
  if (boulderId && existing.isPending) return <LoadingState label={t("Loading boulder")} />;
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
  const remove = useRemoveBoulders(gym.id);
  const fileInput = useRef<MediaInputHandle>(null);

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
    if (!file && !initial) local.photoPath = t("Add a photo of the boulder.");
    if (!sectorId) local.sectorId = t("Choose a sector.");
    if (!holdColor) local.holdColor = t("Choose the hold colour.");
    setErrors(local);
    if (Object.keys(local).length) return;

    try {
      const uploaded = file ? await uploadBoulderPhoto(gym.id, file, setStage) : { path: initial!.photoPath, thumbnailPath: null };
      setStage("saving");
      const input: SaveBoulderInput = {
        sectorId, photoPath: uploaded.path, thumbnailPath: uploaded.thumbnailPath, holdColor: holdColor as HoldColor, setterUserId: setterUserId || null,
        grades: Object.entries(grades).filter(([, v]) => v).map(([gradeSystemId, gradeValueId]) => ({ gradeSystemId, gradeValueId })),
      };
      if (initial) {
        await update.mutateAsync(input);
        toast.success(t("Boulder updated"));
      } else {
        await create.mutateAsync(input);
        toast.success(t("Boulder added"));
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
  const busyLabel = stage === "processing" ? t("Preparing photo…") : stage === "uploading" ? t("Uploading photo…") : stage === "saving" ? t("Saving…") : "";

  return (
    <form className="editor" onSubmit={onSubmit} noValidate>
      <div className="editor__head">
        <Link to={`/manage/${gym.slug}/boulders`} className="manage-head__back" aria-label={t("Back to boulders")}><ArrowLeft aria-hidden /></Link>
        <h2 className="section__title">{initial ? t("Edit boulder") : t("New boulder")}</h2>
      </div>
      {initial && <p className="field__hint">{t("Fix mistakes here. If the boulder was retraced, remove it and add a new one instead, so climbers' history stays accurate.")}</p>}

      {/* Photo */}
      <section className="editor__section" aria-labelledby="photo-label">
        <p id="photo-label" className="field__label">{t("Photo *")}</p>
        <button type="button" className={`photo-picker ${preview ? "has-photo" : ""} ${errors.photoPath ? "is-invalid" : ""}`} onClick={() => fileInput.current?.open()}>
          {preview ? <img src={preview} alt={t("Selected boulder")} /> : (
            <span className="photo-picker__empty"><Camera aria-hidden /><span>{t("Take or choose a photo")}</span></span>
          )}
          {preview && <span className="photo-picker__change"><ImagePlus aria-hidden /> {t("Change")}</span>}
        </button>
        <MediaInput ref={fileInput} kind="image" accept="image/jpeg,image/png,image/webp,image/*" onFile={pickFile} />
        {errors.photoPath && <p className="field__error">{errors.photoPath}</p>}
      </section>

      <SelectField label={t("Sector *")} value={sectorId} onChange={(e) => setSectorId(e.target.value)} error={errors.sectorId}
        options={[{ value: "", label: t("Choose a sector") }, ...activeSectors.map((s) => ({ value: s.id, label: s.name }))]} />

      {/* Official grades — one control per active system */}
      <section className="editor__section" aria-labelledby="grades-label">
        <p id="grades-label" className="field__label">{t("Official grade")}</p>
        <p className="field__hint">{t("You can leave it ungraded and add the grade later: climbers can still log it and suggest one.")}</p>
        <div className="form__grid">
          {activeSystems.map((system) => (
            <SelectField key={system.id} label={dataLabel(system.name)} value={grades[system.id] ?? ""}
              onChange={(e) => setGrades((g) => ({ ...g, [system.id]: e.target.value }))}
              options={[{ value: "", label: t("Not graded") }, ...system.values.filter((v) => v.isActive || v.id === grades[system.id]).map((v) => ({ value: v.id, label: dataLabel(v.label) }))]} />
          ))}
        </div>
        {errors.grades && <p className="field__error">{errors.grades}</p>}
      </section>

      {/* Hold colour — deliberately a different control type from grades */}
      <fieldset className="editor__section hold-picker" aria-describedby={errors.holdColor ? "hold-error" : undefined}>
        <legend className="field__label">{t("Hold colour *")}</legend>
        <p className="field__hint">{t("The colour of the physical holds, not the grade.")}</p>
        <div className="hold-picker__grid">
          {HOLD_COLORS.map((c) => (
            <label key={c.value} className={`hold-option ${holdColor === c.value ? "is-selected" : ""}`}>
              <input type="radio" name="holdColor" value={c.value} checked={holdColor === c.value} onChange={() => setHoldColor(c.value)} />
              <span className="holds__swatch" style={{ background: c.hex }} aria-hidden />
              <span className="hold-option__name">{holdColorName(c.value)}</span>
            </label>
          ))}
        </div>
        {errors.holdColor && <p id="hold-error" className="field__error">{errors.holdColor}</p>}
      </fieldset>

      <SelectField label={t("Setter")} value={setterUserId} onChange={(e) => setSetterUserId(e.target.value)} error={errors.setterUserId}
        options={[{ value: "", label: t("Not specified") }, ...(staff.data ?? []).map((m) => ({ value: m.userId, label: m.displayName }))]} />

      {/* The beta belongs to whoever sets the boulder, so it is published here and not from the climber's page. */}
      {initial && (
        <section className="editor__section" aria-labelledby="beta-editor-label">
          <p id="beta-editor-label" className="field__label">{t("Official beta")}</p>
          <BetaEditor boulderId={initial.id} canAdd={initial.canAddOfficialBeta} />
        </section>
      )}

      <div className="editor__submit">
        <Button type="submit" block icon={<Save aria-hidden />} loading={busy}>{busy ? busyLabel : initial ? t("Save changes") : t("Add boulder")}</Button>
      </div>

      {/* Removing one boulder is a correction, so it is silent. A whole sector coming down is a retrace: that is
          done from the list, where followers can be told once per sector instead of once per boulder. */}
      {initial && initial.status !== "REMOVED" && (
        <section className="editor__section editor__danger" aria-labelledby="remove-label">
          <p id="remove-label" className="field__label">{t("Remove this boulder")}</p>
          <p className="field__hint">{t("It leaves the wall but stays in climbers' history. Nobody is notified — for a whole retrace, select the boulders from the list instead.")}</p>
          <ConfirmButton variant="danger" icon={<Trash2 aria-hidden />} confirmLabel={t("Tap again to remove")} loading={remove.isPending}
            onConfirm={() => remove.mutate({ boulderIds: [initial.id], notifyFollowers: false }, {
              onSuccess: () => { toast.success(t("Boulder removed")); navigate(`/manage/${gym.slug}/boulders`); },
              onError: (e) => toast.error(errorMessage(e)),
            })}>
            {t("Remove")}
          </ConfirmButton>
        </section>
      )}
    </form>
  );
}
