import { useRef, useState } from "react";
import { AlertTriangle, ImagePlus, Trash2 } from "lucide-react";
import type { Sector } from "@/features/gyms/api";
import { fetchSectorPhotoUsage, uploadSectorPhoto, useSetSectorPhoto, type SectorPhotoUsage } from "@/features/gyms/sectorPhoto";
import { MediaInput, type MediaInputHandle } from "@/components/MediaInput";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { plural, t } from "@/i18n/i18n";

/** What staff asked for, held while they read the warning. */
type Change = { kind: "replace"; file: File } | { kind: "remove" };

/**
 * The sector's wall photo: uploaded once, used by every boulder set on that wall. Replacing or removing it deletes the
 * old one, so when boulders are still set on it staff are told first how many will lose their photo.
 */
export function SectorPhotoPanel({ sector, gymId }: { sector: Sector; gymId: string }) {
  const setPhoto = useSetSectorPhoto(gymId);
  const toast = useToast();
  const fileInput = useRef<MediaInputHandle>(null);
  const [busy, setBusy] = useState(false);
  const [pending, setPending] = useState<{ change: Change; usage: SectorPhotoUsage } | null>(null);

  async function apply(change: Change) {
    setBusy(true);
    try {
      const path = change.kind === "replace" ? await uploadSectorPhoto(sector.id, change.file) : null;
      await setPhoto.mutateAsync({ sectorId: sector.id, path });
      toast.success(change.kind === "remove" ? t("Sector photo removed") : sector.photoPath ? t("Sector photo replaced") : t("Sector photo added"));
      setPending(null);
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  /** Straight through when no boulder uses the photo; otherwise the warning first. */
  async function request(change: Change) {
    if (!sector.photoPath) return apply(change);
    setBusy(true);
    try {
      const usage = await fetchSectorPhotoUsage(sector.id);
      if (usage.onTheWall + usage.removed === 0) return await apply(change);
      setPending({ change, usage });
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="sector-photo">
      <div className="thumb-choice">
        <div className="thumb-choice__preview">
          {sector.imageUrl && <img src={sector.imageUrl} alt={t("Photo of {name}", { name: sector.name })} />}
        </div>
        <div className="thumb-choice__text">
          <p className="field__hint">{sector.imageUrl
            ? t("New boulders of this sector can use this photo, each with its own card picture.")
            : t("Upload a photo of the wall once: every boulder of this sector can use it, each with its own card picture.")}</p>
          {!pending && (
            <div className="sector-photo__actions">
              <Button type="button" variant="secondary" icon={<ImagePlus aria-hidden />} loading={busy} onClick={() => fileInput.current?.open()}>
                {sector.imageUrl ? t("Replace photo") : t("Upload photo")}
              </Button>
              {sector.imageUrl && (
                <Button type="button" variant="ghost" icon={<Trash2 aria-hidden />} disabled={busy} onClick={() => request({ kind: "remove" })}>{t("Remove")}</Button>
              )}
            </div>
          )}
        </div>
      </div>
      <MediaInput ref={fileInput} kind="image" accept="image/jpeg,image/png,image/webp,image/*" onFile={(f) => f && request({ kind: "replace", file: f })} />

      {pending && (
        <div className="notice" role="alert">
          <AlertTriangle aria-hidden />
          <div className="stack stack--tight">
            <p><strong>{pending.usage.onTheWall > 0
              ? plural(pending.usage.onTheWall, "1 boulder on the wall is set on this photo.", "{count} boulders on the wall are set on this photo.")
              : plural(pending.usage.removed, "1 boulder already taken down is set on this photo.", "{count} boulders already taken down are set on this photo.")}</strong></p>
            <p className="list__sub">{t("The old photo will be deleted: those boulders keep their card picture but lose the photo, which shows as a placeholder. Choose a new photo for them from each boulder if you need to.")}</p>
            <div className="sector-photo__actions">
              <Button type="button" variant="danger" loading={busy} onClick={() => apply(pending.change)}>
                {pending.change.kind === "remove" ? t("Remove anyway") : t("Replace anyway")}
              </Button>
              <Button type="button" variant="ghost" disabled={busy} onClick={() => setPending(null)}>{t("Cancel")}</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
