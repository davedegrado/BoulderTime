import { useEffect, useRef, useState, type MouseEvent } from "react";
import { X, ZoomIn, ZoomOut } from "lucide-react";
import { t } from "@/i18n/i18n";

const ZOOM = 2.5;

/**
 * The boulder's photo full screen, to pick out holds the page shows too small. The app can't rely on pinch-zoom
 * (the page disables it so the layout never zooms by accident), so a tap zooms in on the spot tapped and the picture
 * can then be dragged around; another tap zooms back out.
 */
export function PhotoViewer({ src, alt, onClose }: { src: string; alt: string; onClose: () => void }) {
  const [zoomed, setZoomed] = useState(false);
  const stage = useRef<HTMLDivElement>(null);
  const close = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    close.current?.focus();
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", onKey);
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { window.removeEventListener("keydown", onKey); document.body.style.overflow = overflow; };
  }, [onClose]);

  function toggle(e?: MouseEvent<HTMLImageElement>) {
    const box = stage.current;
    if (!box) return;
    if (zoomed) { setZoomed(false); return; }
    // Keep the tapped point under the finger once the picture is bigger.
    const rect = box.getBoundingClientRect();
    const fx = e ? (e.clientX - rect.left) / rect.width : 0.5;
    const fy = e ? (e.clientY - rect.top) / rect.height : 0.5;
    setZoomed(true);
    requestAnimationFrame(() => {
      box.scrollLeft = fx * box.scrollWidth - rect.width / 2;
      box.scrollTop = fy * box.scrollHeight - rect.height / 2;
    });
  }

  return (
    <div className="photo-viewer" role="dialog" aria-modal="true" aria-label={alt}>
      <div className="viewer__top">
        <button type="button" className="viewer__close" onClick={() => toggle()} aria-pressed={zoomed}
          aria-label={zoomed ? t("Zoom out") : t("Zoom in")}>
          {zoomed ? <ZoomOut aria-hidden /> : <ZoomIn aria-hidden />}
        </button>
        <button ref={close} type="button" className="viewer__close" onClick={onClose} aria-label={t("Close")}><X aria-hidden /></button>
      </div>
      <div ref={stage} className={`photo-viewer__stage ${zoomed ? "is-zoomed" : ""}`}>
        <img src={src} alt={alt} style={zoomed ? { width: `${ZOOM * 100}%` } : undefined} onClick={toggle} />
      </div>
    </div>
  );
}
