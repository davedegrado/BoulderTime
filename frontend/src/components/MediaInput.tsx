import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from "react";
import { Camera, Images, Video } from "lucide-react";
import { isNativeApp } from "@/lib/native";
import { t } from "@/i18n/i18n";

export interface MediaInputHandle {
  /** Opens the camera-or-gallery choice; call it from a tap. */
  open(): void;
}

interface MediaInputProps {
  kind: "image" | "video";
  accept: string;
  onFile(file: File | undefined): void;
}

/**
 * The one way the app asks for a photo or a video.
 *
 * In a browser a file field already offers "take photo" and "photo library", so it is used as it is. Inside the store
 * app it isn't: Android's in-app WebView opens the gallery only, or the camera only when the field asks for it. So in
 * the app we ask first — camera or gallery — and open the matching field.
 */
export const MediaInput = forwardRef<MediaInputHandle, MediaInputProps>(function MediaInput({ kind, accept, onFile }, ref) {
  const library = useRef<HTMLInputElement>(null);
  const camera = useRef<HTMLInputElement>(null);
  const [choosing, setChoosing] = useState(false);

  useImperativeHandle(ref, () => ({
    open() {
      if (isNativeApp()) setChoosing(true);
      else library.current?.click();
    },
  }), []);

  useEffect(() => {
    if (!choosing) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setChoosing(false); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [choosing]);

  const pick = (input: HTMLInputElement | null) => {
    setChoosing(false);
    input?.click();
  };
  const changed = (e: React.ChangeEvent<HTMLInputElement>) => {
    onFile(e.target.files?.[0]);
    e.target.value = ""; // the same file chosen twice in a row must still count as a choice
  };

  return (
    <>
      <input ref={library} type="file" accept={accept} hidden data-testid={`${kind}-library`} onChange={changed} />
      <input ref={camera} type="file" accept={kind === "image" ? "image/*" : "video/*"} capture="environment" hidden
        data-testid={`${kind}-camera`} onChange={changed} />

      {choosing && (
        <div className="sheet-backdrop" onClick={() => setChoosing(false)}>
          <div className="sheet" role="dialog" aria-modal="true" aria-label={kind === "image" ? t("Add a photo") : t("Add a video")}
            onClick={(e) => e.stopPropagation()}>
            <button type="button" className="sheet__option" onClick={() => pick(camera.current)}>
              {kind === "image" ? <Camera aria-hidden /> : <Video aria-hidden />}
              <span>{kind === "image" ? t("Take a photo") : t("Record a video")}</span>
            </button>
            <button type="button" className="sheet__option" onClick={() => pick(library.current)}>
              <Images aria-hidden />
              <span>{t("Choose from the gallery")}</span>
            </button>
            <button type="button" className="sheet__cancel" onClick={() => setChoosing(false)}>{t("Cancel")}</button>
          </div>
        </div>
      )}
    </>
  );
});
