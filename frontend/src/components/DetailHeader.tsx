import { useEffect, useRef, useState, type ReactNode, type RefObject } from "react";
import { ArrowLeft } from "lucide-react";
import { useGoBack } from "@/lib/navigation";
import { setStatusBarOverCover } from "@/lib/theme";
import { t } from "@/i18n/i18n";

/**
 * The header of a gym's or a person's page, in place of the logo bar: back on the left, the page's "…" menu on the
 * right, no logo. At the top of the page it is transparent over the cover (`coverRef`); once the cover has gone under
 * it, it turns solid, and once the page's own name (`titleRef`) has gone under it too, that name appears in the middle.
 * Both moments are where those elements actually meet the header's bottom edge, measured on the page, so they hold on
 * any screen and safe area. The header's height never changes: nothing below it moves.
 * Without a cover (while loading, on an error) it is solid from the start. Back returns where the page was opened
 * from and goes Home only when it was opened directly (useGoBack).
 */
export function DetailHeader({ menu, title, coverRef, titleRef }: {
  menu?: ReactNode;
  title?: string;
  coverRef?: RefObject<HTMLElement | null>;
  titleRef?: RefObject<HTMLElement | null>;
}) {
  const goBack = useGoBack();
  const header = useRef<HTMLDivElement>(null);
  const [solid, setSolid] = useState(!coverRef);
  const [titled, setTitled] = useState(false);

  useEffect(() => {
    const bar = header.current;
    if (!bar || typeof IntersectionObserver === "undefined") return;
    let observer: IntersectionObserver | null = null;
    const watch = () => {
      observer?.disconnect();
      // The line that matters is the header's bottom edge: the viewport, less the header, is what is still in view.
      const height = bar.getBoundingClientRect().height;
      observer = new IntersectionObserver((entries) => {
        for (const e of entries) {
          const top = e.rootBounds?.top ?? height;
          const gone = !e.isIntersecting && e.boundingClientRect.bottom <= top + 1;
          if (e.target === coverRef?.current) setSolid(gone);
          if (e.target === titleRef?.current) setTitled(gone);
        }
      }, { rootMargin: `-${Math.round(height)}px 0px 0px 0px`, threshold: 0 });
      if (coverRef?.current) observer.observe(coverRef.current);
      if (titleRef?.current) observer.observe(titleRef.current);
    };
    watch();
    // A rotation or a different safe area changes the header's height, and with it the line.
    window.addEventListener("resize", watch);
    return () => { window.removeEventListener("resize", watch); observer?.disconnect(); };
  }, [coverRef, titleRef]);

  // On iPhone the page runs under the status bar: its text turns light while the header is over the cover.
  const overCover = Boolean(coverRef) && !solid;
  useEffect(() => {
    setStatusBarOverCover(overCover);
    return () => setStatusBarOverCover(false);
  }, [overCover]);

  return (
    <div ref={header} className={`detail-header${coverRef ? " detail-header--over" : ""}${solid ? " is-solid" : ""}${titled ? " has-title" : ""}`}>
      <button type="button" className="icon-btn detail-header__back" aria-label={t("Back")} onClick={() => goBack("/")}>
        <ArrowLeft aria-hidden />
      </button>
      {/* The page's heading already names it for screen readers: this is its echo for the eyes. */}
      <span className="detail-header__title" aria-hidden>{title}</span>
      <div className="detail-header__end">{menu}</div>
    </div>
  );
}
