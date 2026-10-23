import { useEffect, useRef, useState } from "react";
import { X, ZoomIn, ZoomOut } from "lucide-react";
import { TAP_ZOOM, usePinchZoom } from "@/features/boulders/usePinchZoom";
import type { Size } from "@/features/boulders/zoomView";
import { t } from "@/i18n/i18n";

/** Until the picture and the screen are measured, it is simply fitted and centred — where the zoom then takes it. */
const FITTED = { position: "absolute", inset: 0, margin: "auto", maxWidth: "100%", maxHeight: "100%" } as const;

/**
 * A picture full screen — a boulder's photo, a person's, an announcement's — to see what the page shows too small.
 * Two fingers zoom it (the page itself can't be pinched, so the viewer does it), one finger moves it once enlarged, a
 * tap goes in on the spot tapped and back out, and so does the button; on a computer the wheel zooms and + / − too.
 */
export function PhotoViewer({ src, alt, onClose }: { src: string; alt: string; onClose: () => void }) {
  const [picture, setPicture] = useState<Size | null>(null);
  const zoom = usePinchZoom(picture, { tapToZoom: true });
  const close = useRef<HTMLButtonElement>(null);
  const zoomTo = zoom.zoomTo;
  const scale = zoom.scale;

  useEffect(() => {
    close.current?.focus();
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { document.body.style.overflow = overflow; };
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
      else if (e.key === "+" || e.key === "=") zoomTo(Math.max(TAP_ZOOM, scale * 1.5));
      else if (e.key === "-") zoomTo(scale / 1.5);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose, zoomTo, scale]);

  const loaded = (img: HTMLImageElement | null) => {
    if (img?.complete && img.naturalWidth > 0 && !picture) setPicture({ w: img.naturalWidth, h: img.naturalHeight });
  };

  return (
    <div className="photo-viewer" role="dialog" aria-modal="true" aria-label={alt}>
      <div className="viewer__top">
        <button type="button" className="viewer__close" onClick={() => zoomTo(zoom.zoomed ? 1 : TAP_ZOOM)} aria-pressed={zoom.zoomed}
          aria-label={zoom.zoomed ? t("Zoom out") : t("Zoom in")}>
          {zoom.zoomed ? <ZoomOut aria-hidden /> : <ZoomIn aria-hidden />}
        </button>
        <button ref={close} type="button" className="viewer__close" onClick={onClose} aria-label={t("Close")}><X aria-hidden /></button>
      </div>
      <div ref={zoom.box} className={`photo-viewer__stage zoom-stage${zoom.zoomed ? " is-zoomed" : ""}`} {...zoom.boxProps}>
        <img ref={loaded} src={src} alt={alt} draggable={false} className="zoom-stage__content"
          style={zoom.contentStyle ?? FITTED}
          onLoad={(e) => setPicture({ w: e.currentTarget.naturalWidth, h: e.currentTarget.naturalHeight })} />
      </div>
    </div>
  );
}
