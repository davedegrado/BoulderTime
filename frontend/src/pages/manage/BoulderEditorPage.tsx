import { useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useGoBack } from "@/lib/navigation";
import { ArrowLeft, Camera, Check, Crop, Expand, ImagePlus, Mountain, Plus, RotateCcw, Save, Trash2, Wand2 } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useSectors } from "@/features/gyms/api";
import { fetchSectorPhoto } from "@/features/gyms/sectorPhoto";
import { useGradeSystems } from "@/features/grading/api";
import { useStaff } from "@/features/staff/api";
import { fetchBoulderPhoto, fetchHighlight, uploadBoulderPhoto, uploadBoulderThumbnail, uploadHighlightMask, uploadOriginalPhoto, useBoulder, useCreateBoulder, useRemoveBoulders, useUpdateBoulder, wholeThumbnail, type BoulderHighlight, type SaveBoulderInput } from "@/features/boulders/api";
import { HOLD_COLORS, holdColorName, type HoldColor } from "@/features/boulders/holdColors";
import { SelectField } from "@/components/Fields";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";
import { MediaInput, type MediaInputHandle } from "@/components/MediaInput";
import { BetaEditor } from "@/features/community/BetaEditor";
import { ThumbnailCropper } from "@/features/boulders/ThumbnailCropper";
import { HoldHighlighter, readHighlightSettings, type HighlightResult, type HighlightResume, type HighlightSource } from "@/features/boulders/HoldHighlighter";
import { CARD_PICTURE, cropImage, type CropRect } from "@/features/boulders/imageResize";
import { INTENSITIES, IntensityBars, intensityShort, type Intensity } from "@/features/boulders/intensity";
import { useCreateTag, useGymTags } from "@/features/boulders/tags";

/** A boulder carries at most this many tags; the server holds the same limit. */
const MAX_TAGS = 6;

export function BoulderEditorPage() {
  const { boulderId } = useParams();
  const existing = useBoulder(boulderId);
  if (boulderId && existing.isPending) return <LoadingState label={t("Loading boulder")} />;
  if (boulderId && existing.isError) return <ErrorState error={existing.error} onRetry={() => existing.refetch()} />;
  return <BoulderEditor key={boulderId ?? "new"} initial={existing.data} />;
}

type Stage = "idle" | "processing" | "uploading" | "saving";

/**
 * A highlight made in this editor, to be saved with the boulder: what it was made from (a wall photo, the photo
 * just picked, or the original the boulder already keeps) and the mask and choices handed back.
 */
type HighlightOrigin = { kind: "wall"; sectorId: string; path: string } | { kind: "picked"; file: File } | { kind: "kept"; path: string };
interface PendingHighlight { origin: HighlightOrigin; color: HoldColor; result: HighlightResult }

function BoulderEditor({ initial }: { initial?: ReturnType<typeof useBoulder>["data"] }) {
  const { gym } = useManagedGym();
  const navigate = useNavigate();
  const goBack = useGoBack();
  const listPath = `/manage/${gym.slug}/boulders`;
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
  const [intensity, setIntensity] = useState<Intensity | null>(initial?.intensity ?? null);
  const [tagIds, setTagIds] = useState<string[]>(() => (initial?.tags ?? []).map((x) => x.id));
  const [name, setName] = useState(initial?.name ?? "");
  // Points set by hand for this one boulder; null follows its grade.
  const [points, setPoints] = useState<number | null>(initial?.pointsOverride ?? null);
  const [pointsText, setPointsText] = useState(initial?.pointsOverride != null ? String(initial.pointsOverride) : "");
  const [stage, setStage] = useState<Stage>("idle");
  const [errors, setErrors] = useState<Record<string, string>>({});
  // The part of the photo the cards show, when staff chose one; otherwise the cards show the whole photo.
  // `whole`: the whole photo, chosen again after a part had been cut out.
  const [thumb, setThumb] = useState<{ blob: Blob; url: string; whole?: boolean } | null>(null);
  const [cropping, setCropping] = useState(false);
  // The sector's wall photo, when staff set the boulder on it instead of uploading a photo of its own.
  const [wall, setWall] = useState<{ sectorId: string; path: string } | null>(null);
  // Open, and whether it goes on from the highlight there is (true) or starts from a photo (false).
  const [highlighting, setHighlighting] = useState<false | "resume" | "fresh">(false);
  const [pendingHighlight, setPendingHighlight] = useState<PendingHighlight | null>(null);
  // The photo staff picked themselves, kept apart from a highlighted result made from it.
  const [picked, setPicked] = useState<File | null>(null);
  // The boulder's own photo, once loaded to highlight the holds on it.
  const currentBlob = useRef<Blob | null>(null);

  useEffect(() => () => { if (preview?.startsWith("blob:")) URL.revokeObjectURL(preview); }, [preview]);
  useEffect(() => () => { if (thumb) URL.revokeObjectURL(thumb.url); }, [thumb]);

  const activeSystems = useMemo(() => (systems.data ?? []).filter((s) => s.isActive), [systems.data]);
  const activeSectors = (sectors.data ?? []).filter((s) => s.isActive || s.id === initial?.sectorId);
  const sector = activeSectors.find((s) => s.id === sectorId);
  // The photo the boulder will be saved with: one just picked, the wall just chosen, or the one it has.
  const photoPath = file ? null : wall?.path ?? initial?.photoPath ?? null;
  const wallOffer = sector?.photoPath && sector.imageUrl && photoPath !== sector.photoPath ? sector : null;
  // Photos the holds can be highlighted on, all of this boulder: the photo just picked, the photo it has (unless it is
  // already a highlighted one, which filtering again would wear, or is its sector's wall photo, offered next anyway)
  // and the wall photo of its own sector. Never another sector's wall: that is a different wall.
  const ownSector = (sectors.data ?? []).find((s) => s.id === sectorId && s.photoPath && s.imageUrl);
  const currentPhoto = initial && !file && !wall && !initial.highlight && initial.photoPath && initial.photoPath !== ownSector?.photoPath ? initial : null;
  const highlightSources: HighlightSource[] = [
    ...(picked ? [{ id: "picked", label: t("The photo you just picked"), load: async () => picked as Blob }] : []),
    ...(currentPhoto ? [{ id: "current", label: t("This boulder's photo"), load: async () => (currentBlob.current = await fetchBoulderPhoto(currentPhoto.id)) }] : []),
    ...(ownSector ? [{ id: `sector:${ownSector.id}`, label: t("Wall of {name}", { name: ownSector.name }), load: () => fetchSectorPhoto(ownSector.id) }] : []),
  ];

  // Going on from where the holds were left: the highlight made in this editor, or the one saved with the boulder
  // while its photo is still that one. Settings it can't read mean starting afresh.
  const savedHighlight = !pendingHighlight && initial?.highlight && !file && !wall ? initial.highlight : null;
  const savedChoices = readHighlightSettings(pendingHighlight?.result.settings ?? savedHighlight?.settings);
  const resume: HighlightResume | null = pendingHighlight && savedChoices
    ? {
        color: pendingHighlight.color, outline: savedChoices.outline,
        load: async () => ({ source: await originalOf(pendingHighlight.origin), mask: pendingHighlight.result.mask }),
      }
    : savedHighlight && savedChoices
      ? { color: savedChoices.color, outline: savedChoices.outline, load: () => fetchHighlight(initial!.id) }
      : null;

  async function originalOf(origin: HighlightOrigin): Promise<Blob> {
    if (origin.kind === "picked") return origin.file;
    if (origin.kind === "wall") return fetchSectorPhoto(origin.sectorId);
    return (await fetchHighlight(initial!.id)).source;
  }

  /** The highlighted photo becomes the boulder's own photo, as if staff had picked it; the wall photo stays as it is. */
  function applyHighlighted(photo: Blob, color: HoldColor, result: HighlightResult) {
    // What it was made from: the photo chosen in the editor, or (going on from before) the same as last time.
    const wallSector = result.sourceId?.startsWith("sector:") ? (sectors.data ?? []).find((x) => `sector:${x.id}` === result.sourceId) : undefined;
    const origin: HighlightOrigin | null = wallSector?.photoPath ? { kind: "wall", sectorId: wallSector.id, path: wallSector.photoPath }
      : result.sourceId === "picked" && picked ? { kind: "picked", file: picked }
      // The boulder's own photo: kept as the original of the highlight, as if staff had picked it again.
      : result.sourceId === "current" && currentBlob.current ? { kind: "picked", file: new File([currentBlob.current], "original.jpg", { type: currentBlob.current.type || "image/jpeg" }) }
      : pendingHighlight ? pendingHighlight.origin
      : initial?.highlight ? { kind: "kept", path: initial.highlight.sourcePath }
      : null;
    setHighlighting(false);
    pickFile(new File([photo], "holds.jpg", { type: "image/jpeg" }), { highlighted: true });
    setPendingHighlight(origin ? { origin, color, result } : null);
    if (!holdColor) setHoldColor(color);
  }

  // What the chosen grade is worth: the first of the gym's scales it is graded in, as the leaderboard counts it.
  const gradePoints = useMemo(() => {
    for (const system of activeSystems) {
      const value = system.values.find((v) => v.id === grades[system.id]);
      if (value) return value.points ?? null;
    }
    return null;
  }, [activeSystems, grades]);

  function pickFile(f: File | undefined, { highlighted = false } = {}) {
    if (!f) return;
    // A photo picked by hand replaces a highlighted one: there is no highlight to go back to any more.
    if (!highlighted) { setPicked(f); setPendingHighlight(null); }
    setFile(f);
    setWall(null);
    setPreview(URL.createObjectURL(f));
    setThumb(null); // a new photo starts again from the whole picture
    setErrors((e) => ({ ...e, photoPath: "" }));
  }

  /** Sets the boulder on its sector's wall photo, and goes straight to choosing its part for the cards. */
  function chooseWall() {
    if (!wallOffer) return;
    setFile(null);
    setWall({ sectorId: wallOffer.id, path: wallOffer.photoPath! });
    setPreview(wallOffer.imageUrl);
    setThumb(null);
    setPendingHighlight(null);
    setErrors((e) => ({ ...e, photoPath: "" }));
  }

  /** The cards show the whole photo again, after a part had been chosen. */
  async function chooseWholePhoto() {
    try {
      const source = file ?? (wall ? await fetchSectorPhoto(wall.sectorId) : await fetchBoulderPhoto(initial!.id));
      const blob = await wholeThumbnail(source);
      setThumb({ blob, url: URL.createObjectURL(blob), whole: true });
    } catch (err) {
      const reason = err instanceof Error && err.message ? ` (${err.message})` : "";
      toast.error(t("Couldn't prepare the card picture. Try again.") + reason);
    }
  }

  async function applyCrop(rect: CropRect) {
    setCropping(false);
    try {
      // The photo just picked, the wall chosen, or the saved one, fetched again to cut the part out on this device.
      const source = file ?? (wall ? await fetchSectorPhoto(wall.sectorId) : await fetchBoulderPhoto(initial!.id));
      const blob = await cropImage(source, rect, CARD_PICTURE);
      setThumb({ blob, url: URL.createObjectURL(blob) });
    } catch (err) {
      // The reason travels along: this is a staff tool, and "why" is what they'll be asked when they report it.
      const reason = err instanceof Error && err.message ? ` (${err.message})` : "";
      toast.error(t("Couldn't prepare the card picture. Try again.") + reason);
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    const local: Record<string, string> = {};
    if (!file && !wall && !initial) local.photoPath = t("Add a photo of the boulder.");
    if (!sectorId) local.sectorId = t("Choose a sector.");
    if (!holdColor) local.holdColor = t("Choose the hold colour.");
    if (points !== null && !(Number.isInteger(points) && points >= 1 && points <= 1000)) local.points = t("Points go from 1 to {max}.", { max: 1000 });
    setErrors(local);
    if (Object.keys(local).length) return;

    try {
      // The card picture: the part chosen, or else the whole photo — made small for the lists, so a wall photo of
      // 1600 px isn't what every card loads.
      const uploaded = file
        ? await uploadBoulderPhoto(gym.id, file, setStage, thumb?.blob)
        : {
            path: wall?.path ?? initial!.photoPath,
            thumbnailPath: thumb ? (setStage("uploading"), await uploadBoulderThumbnail(gym.id, thumb.blob))
              : wall ? (setStage("uploading"), await uploadBoulderThumbnail(gym.id, await wholeThumbnail(await fetchSectorPhoto(wall.sectorId))))
              : null,
          };
      // A highlight made here is saved with the photo, so the next edit goes on from it.
      let highlight: BoulderHighlight | undefined;
      if (file && pendingHighlight) {
        setStage("uploading");
        const { origin, result } = pendingHighlight;
        const sourcePath = origin.kind === "picked" ? await uploadOriginalPhoto(gym.id, origin.file) : origin.path;
        highlight = { sourcePath, maskPath: await uploadHighlightMask(gym.id, result.mask), settings: result.settings };
      }
      setStage("saving");
      const input: SaveBoulderInput = {
        sectorId, photoPath: uploaded.path, thumbnailPath: uploaded.thumbnailPath, holdColor: holdColor as HoldColor, setterUserId: setterUserId || null,
        grades: Object.entries(grades).filter(([, v]) => v).map(([gradeSystemId, gradeValueId]) => ({ gradeSystemId, gradeValueId })),
        intensity, tagIds, name: name.trim(), points: points ?? 0,
        ...(highlight ? { highlight } : {}),
      };
      if (initial) {
        await update.mutateAsync(input);
        toast.success(t("Boulder updated"));
      } else {
        await create.mutateAsync(input);
        toast.success(t("Boulder added"));
      }
      // Back where the editor was opened from (the list as it was left, or the boulder's page).
      goBack(listPath);
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
        <button type="button" className="manage-head__back" aria-label={t("Back")} onClick={() => goBack(listPath)}><ArrowLeft aria-hidden /></button>
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
        <MediaInput ref={fileInput} kind="image" accept="image/jpeg,image/png,image/webp,image/*" onFile={(f) => pickFile(f)} />
        {wallOffer && (
          <div className="photo-source">
            <Button type="button" variant="secondary" icon={<Mountain aria-hidden />} onClick={chooseWall}>{t("Use the photo of {name}", { name: wallOffer.name })}</Button>
          </div>
        )}
        {wall && !thumb && <p className="field__hint">{t("Every boulder of this sector shares this photo: if you like, choose the part that shows this one for its cards.")}</p>}
        {errors.photoPath && <p className="field__error">{errors.photoPath}</p>}
      </section>

      {/* Highlight the holds: the holds of this boulder in colour, the rest of the wall in black and white. */}
      <section className="editor__section" aria-labelledby="highlight-label">
        <p id="highlight-label" className="field__label">{t("Highlight the holds")}</p>
        {resume ? (
          <>
            <p className="field__hint">{t("Go on from the holds as you left them, or start again from a photo of the wall.")}</p>
            <div className="photo-source">
              <Button type="button" variant="secondary" icon={<Wand2 aria-hidden />} onClick={() => setHighlighting("resume")}>{t("Edit the highlighted holds")}</Button>
              {highlightSources.length > 0 && (
                <Button type="button" variant="ghost" icon={<RotateCcw aria-hidden />} onClick={() => setHighlighting("fresh")}>{t("Start again from a photo")}</Button>
              )}
            </div>
          </>
        ) : highlightSources.length > 0 ? (
          <>
            <p className="field__hint">{t("Optional. From a photo of the wall, keep this boulder's holds in colour and turn the rest black and white; the result becomes this boulder's photo when you save.")}</p>
            <Button type="button" variant="secondary" icon={<Wand2 aria-hidden />} onClick={() => setHighlighting("fresh")}>{t("Highlight the holds")}</Button>
          </>
        ) : (
          <>
            <p className="field__hint">{t("No photo of the wall yet. Add one to the sector in Sectors, or take or choose a photo here first.")}</p>
            <Button type="button" variant="secondary" icon={<Camera aria-hidden />} onClick={() => fileInput.current?.open()}>{t("Take or choose a photo")}</Button>
          </>
        )}
      </section>
      {highlighting && (
        <HoldHighlighter sources={highlightSources} initialColor={holdColor} resume={highlighting === "resume" && resume ? resume : undefined}
          onCancel={() => setHighlighting(false)} onDone={applyHighlighted} />
      )}

      {/* Card picture: which part of the photo the boulder's cards show */}
      {preview && (
        <section className="editor__section" aria-labelledby="thumb-label">
          <p id="thumb-label" className="field__label">{t("Card picture")}</p>
          <div className="thumb-choice">
            <div className="thumb-choice__preview">
              <img src={thumb?.url ?? (file || wall ? preview : initial?.thumbnailUrl ?? preview)} alt={t("What the cards show")} />
            </div>
            <div className="thumb-choice__text">
              <p className="field__hint">{thumb?.whole ? t("The whole photo. It is saved with the boulder.")
                : thumb ? t("The part you chose. It is saved with the boulder.")
                : t("Optional: without it the cards show the whole photo. Choose a part if the boulder doesn't stand out.")}</p>
              <Button type="button" variant="secondary" icon={<Crop aria-hidden />} onClick={() => setCropping(true)}>{t("Choose the part")}</Button>
              {/* Back to the whole photo after a part was chosen (now, or when the boulder was saved). */}
              {((thumb && !thumb.whole) || (!thumb && initial && !file && !wall)) && (
                <Button type="button" variant="ghost" icon={<Expand aria-hidden />} onClick={chooseWholePhoto}>{t("Use the whole photo")}</Button>
              )}
            </div>
          </div>
        </section>
      )}
      {cropping && preview && <ThumbnailCropper src={preview} onCancel={() => setCropping(false)} onDone={applyCrop} />}

      <TextField label={t("Name")} value={name} maxLength={60} onChange={(e) => setName(e.target.value)} error={errors.name}
        hint={t("Optional. The name the setters gave it, shown on its card and page.")} />

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

      {/* Points: the grade's, unless staff set this one boulder's by hand (a classic, an event problem). */}
      <section className="editor__section" aria-labelledby="points-label">
        <p id="points-label" className="field__label">{t("Points")}</p>
        {points === null ? (
          <>
            <p className="field__hint">{gradePoints != null
              ? t("Worth {points} points on the leaderboard, like every boulder of its grade.", { points: gradePoints })
              : t("Ungraded boulders score no points until they have a grade.")}</p>
            <Button type="button" variant="secondary" onClick={() => { const start = gradePoints ?? 50; setPoints(start); setPointsText(String(start)); }}>{t("Set points for this boulder")}</Button>
          </>
        ) : (
          <>
            <div className="points-input">
              <input className="field__input" type="number" inputMode="numeric" min={1} max={1000} step={1} value={pointsText} aria-label={t("Points for this boulder")}
                aria-invalid={!!errors.points || undefined}
                onChange={(e) => { setPointsText(e.target.value); setPoints(e.target.value === "" ? Number.NaN : Number(e.target.value)); }} />
              <Button type="button" variant="ghost" onClick={() => { setPoints(null); setPointsText(""); }}>
                {gradePoints != null ? t("Back to the grade's {points}", { points: gradePoints }) : t("Back to the grade's points")}
              </Button>
            </div>
            <p className="field__hint">{t("Every send of this boulder scores this, past ones included.")}</p>
          </>
        )}
        {errors.points && <p className="field__error">{errors.points}</p>}
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

      {/* Intensity: how hard it works the body, apart from the grade. Optional, so older boulders need nothing. */}
      <fieldset className="editor__section">
        <legend className="field__label">{t("Intensity")}</legend>
        <p className="field__hint">{t("How hard it is on the body, whatever the grade: a green can be gentle or punishing.")}</p>
        <div className="intensity-picker" role="radiogroup" aria-label={t("Intensity")}>
          <button type="button" role="radio" aria-checked={intensity === null} className="intensity-picker__option" onClick={() => setIntensity(null)}>{t("Not given")}</button>
          {INTENSITIES.map((i) => (
            <button key={i} type="button" role="radio" aria-checked={intensity === i} className="intensity-picker__option" onClick={() => setIntensity(i)}>
              <IntensityBars intensity={i} /> {intensityShort(i)}
            </button>
          ))}
        </div>
        {errors.intensity && <p className="field__error">{errors.intensity}</p>}
      </fieldset>

      <TagPicker gymId={gym.id} gymSlug={gym.slug} selected={tagIds} onChange={setTagIds} error={errors.tagids} />

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
              // Replacing the editor, so going back from the list doesn't reopen a boulder that is gone from the wall.
              onSuccess: () => { toast.success(t("Boulder removed")); navigate(listPath, { replace: true }); },
              onError: (e) => toast.error(errorMessage(e)),
            })}>
            {t("Remove")}
          </ConfirmButton>
        </section>
      )}
    </form>
  );
}

/**
 * The gym's tags as chips to switch on and off, and a quick way to add one that is missing: staff writing a boulder
 * shouldn't have to leave the form to create "Placca".
 */
function TagPicker({ gymId, gymSlug, selected, onChange, error }: { gymId: string; gymSlug: string; selected: string[]; onChange: (ids: string[]) => void; error?: string }) {
  const tags = useGymTags(gymId);
  const create = useCreateTag(gymId);
  const toast = useToast();
  const [name, setName] = useState("");
  const full = selected.length >= MAX_TAGS;

  const toggle = (id: string) => onChange(selected.includes(id) ? selected.filter((x) => x !== id) : full ? selected : [...selected, id]);

  function add() {
    const trimmed = name.trim();
    if (!trimmed) return;
    create.mutate(trimmed, {
      onSuccess: (tag) => { setName(""); if (!full) onChange([...selected, tag.id]); },
      onError: (e) => toast.error(e instanceof ApiError ? (e.fieldError("name") ?? errorMessage(e)) : errorMessage(e)),
    });
  }

  return (
    <fieldset className="editor__section">
      <legend className="field__label">{t("Tags")}</legend>
      <p className="field__hint">{t("Up to {count}. They help climbers find the boulders they like.", { count: MAX_TAGS })}{" "}
        <Link to={`/manage/${gymSlug}/tags`}>{t("Manage tags")}</Link></p>
      {(tags.data ?? []).length > 0 && (
        <div className="chips chips--wrap" role="group" aria-label={t("Tags")}>
          {(tags.data ?? []).map((tag) => {
            const on = selected.includes(tag.id);
            return (
              <button key={tag.id} type="button" className="chip" aria-pressed={on} disabled={!on && full} onClick={() => toggle(tag.id)}>
                {on && <Check aria-hidden className="chip__check" />}{tag.name}
              </button>
            );
          })}
        </div>
      )}
      <div className="tag-add">
        <input className="field__input" value={name} maxLength={24} placeholder={t("New tag, e.g. Slab")} aria-label={t("New tag")}
          onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); add(); } }} />
        <Button type="button" variant="secondary" icon={<Plus aria-hidden />} loading={create.isPending} onClick={add} disabled={!name.trim()}>{t("Add")}</Button>
      </div>
      {error && <p className="field__error">{error}</p>}
    </fieldset>
  );
}
