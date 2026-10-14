import { useRef, useState } from "react";
import { ImagePlus, Trash2 } from "lucide-react";
import type { Sector } from "@/features/gyms/api";
import { uploadSectorPhoto, useSetSectorPhoto } from "@/features/gyms/sectorPhoto";
import { MediaInput, type MediaInputHandle } from "@/components/MediaInput";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/**
 * The sector's wall photo: uploaded once, used by every boulder set on that wall. Replacing or removing it only
 * changes what new boulders use; the boulders already set on the old photo keep it, in climbers' history too.
 */
export function SectorPhotoPanel({ sector, gymId }: { sector: Sector; gymId: string }) {
  const setPhoto = useSetSectorPhoto(gymId);
  const toast = useToast();
  const fileInput = useRef<MediaInputHandle>(null);
  const [busy, setBusy] = useState(false);

  async function change(file: File | null) {
    setBusy(true);
    try {
      const path = file ? await uploadSectorPhoto(sector.id, file) : null;
      await setPhoto.mutateAsync({ sectorId: sector.id, path });
      toast.success(!file ? t("Sector photo removed") : sector.photoPath ? t("Sector photo replaced") : t("Sector photo added"));
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
            ? t("New boulders of this sector can use this photo, each with its own card picture. If you replace it, the boulders already set keep the photo they have.")
            : t("Upload a photo of the wall once: every boulder of this sector can use it, each with its own card picture.")}</p>
          <div className="sector-photo__actions">
            <Button type="button" variant="secondary" icon={<ImagePlus aria-hidden />} loading={busy} onClick={() => fileInput.current?.open()}>
              {sector.imageUrl ? t("Replace photo") : t("Upload photo")}
            </Button>
            {sector.imageUrl && (
              <Button type="button" variant="ghost" icon={<Trash2 aria-hidden />} disabled={busy} onClick={() => change(null)}>{t("Remove")}</Button>
            )}
          </div>
        </div>
      </div>
      <MediaInput ref={fileInput} kind="image" accept="image/jpeg,image/png,image/webp,image/*" onFile={(f) => f && change(f)} />
    </div>
  );
}
