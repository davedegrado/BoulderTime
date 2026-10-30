import { useLayoutEffect, useRef, useState, type CSSProperties } from "react";
import { Link } from "react-router-dom";
import { Check, ChevronRight, MountainSnow, Plus, Sparkles, X } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { BACKDROP_REACH, featherMask, outlineArea, outlineWidth } from "@/features/boulders/holdHighlight";
import { OUTLINE_COLORS, readHighlightSettings } from "@/features/boulders/HoldHighlighter";
import { useBoulders, type BoulderSummary } from "@/features/boulders/api";
import { GradeBadge, HoldIcon } from "@/features/boulders/BoulderBits";
import { holdColorName } from "@/features/boulders/holdColors";
import type { Sector } from "@/features/gyms/api";
import { LoadingState, ErrorState } from "@/components/States";
import { dataLabel } from "@/i18n/data";
import { plural, t } from "@/i18n/i18n";

/** What lights a boulder's holds up on the sector's photo: where its holds are, and its outline, as pictures to draw. */
interface HoldsLook { holds: string; outline: string | null; backdrop: number }

/** Turns a picture's pixels into a PNG address the page can use. */
async function pngUrl(data: ImageData): Promise<string> {
  const canvas = document.createElement("canvas");
  canvas.width = data.width;
  canvas.height = data.height;
  canvas.getContext("2d")!.putImageData(data, 0, 0);
  const blob = await new Promise<Blob>((resolve, reject) => canvas.toBlob((b) => (b ? resolve(b) : reject(new Error("no picture"))), "image/png"));
  return URL.createObjectURL(blob);
}

/**
 * A boulder's holds as staff left them in "Highlight the holds": its own mask (read through the API, whose pixels can
 * be read), softened at the edge as the saved photo is, and the outline they chose drawn from that same mask by the
 * editor's own code — so the sector's photo shows the holds the way the boulder's photo does. Kept for the session.
 */
const looks = new Map<string, Promise<HoldsLook>>();
function holdsLook(boulderId: string, settings: string | null | undefined): Promise<HoldsLook> {
  const key = `${boulderId}|${settings ?? ""}`;
  let found = looks.get(key);
  if (!found) {
    found = (async () => {
      const blob = await api.blob(`/api/boulders/${boulderId}/holds-on-sector-photo`);
      const bitmap = await createImageBitmap(blob);
      const { width, height } = bitmap;
      const canvas = document.createElement("canvas");
      canvas.width = width;
      canvas.height = height;
      const ctx = canvas.getContext("2d");
      if (!ctx) throw new Error("no canvas");
      ctx.drawImage(bitmap, 0, 0);
      bitmap.close?.();
      const pixels = ctx.getImageData(0, 0, width, height).data;
      const mask = new Uint8Array(width * height);
      for (let p = 0; p < mask.length; p++) mask[p] = pixels[p * 4]! >= 128 ? 255 : 0;

      // The holds: white where they keep their colour, the edge softened by a pixel like the saved photo's.
      const soft = featherMask(mask, width, height, 1);
      const holds = new ImageData(width, height);
      for (let p = 0, i = 0; p < soft.length; p++, i += 4) { holds.data[i] = holds.data[i + 1] = holds.data[i + 2] = 255; holds.data[i + 3] = soft[p]!; }

      // The outline, if staff kept it on: drawn by the editor over black and over white, which tells, pixel by pixel,
      // how much of the line covers it and in what colour.
      const chosen = readHighlightSettings(settings);
      let outline: string | null = null;
      if (chosen?.outline.on) {
        const c = OUTLINE_COLORS.find((x) => x.key === chosen.outline.color) ?? OUTLINE_COLORS[0]!;
        const style = { rgb: [...c.rgb] as [number, number, number], width: outlineWidth(chosen.outline.level, width, height) };
        const onBlack = new Uint8ClampedArray(width * height * 4), onWhite = new Uint8ClampedArray(width * height * 4).fill(255);
        outlineArea(onBlack, width, height, mask, 0, 0, width, height, style);
        outlineArea(onWhite, width, height, mask, 0, 0, width, height, style);
        const line = new ImageData(width, height);
        for (let i = 0; i < onBlack.length; i += 4) {
          const cover = 1 - (onWhite[i]! - onBlack[i]!) / 255;
          if (cover <= 0) continue;
          line.data[i] = onBlack[i]! / cover;
          line.data[i + 1] = onBlack[i + 1]! / cover;
          line.data[i + 2] = onBlack[i + 2]! / cover;
          line.data[i + 3] = cover * 255;
        }
        outline = await pngUrl(line);
      }
      return { holds: await pngUrl(holds), outline, backdrop: (chosen?.backdrop ?? 0) / 100 };
    })();
    found.catch(() => looks.delete(key));
    looks.set(key, found);
  }
  return found;
}

function useHoldsLook(boulder: BoulderSummary | null) {
  return useQuery({
    queryKey: ["holds-on-sector-photo", boulder?.id, boulder?.holdsOnSectorPhotoLook ?? null],
    queryFn: () => holdsLook(boulder!.id, boulder!.holdsOnSectorPhotoLook),
    enabled: !!boulder, staleTime: Infinity, retry: false,
  });
}

/**
 * The sector's photo, and one boulder's holds lit up on it as on the boulder's own photo: the wall in black and white,
 * as dark or light as staff set it, the holds in colour, the outline they chose. The photo itself is never read, only
 * shown in layers — so it works whatever the storage says about reading it.
 */
function SectorPhoto({ sector, lit }: { sector: Sector; lit: BoulderSummary | null }) {
  const look = useHoldsLook(lit);
  const on = !!lit && !!look.data;
  const backdrop = look.data?.backdrop ?? 0;
  // The frame takes the photo's own proportions, so the holds drawn over it land exactly on the wall's.
  const [ratio, setRatio] = useState<number | null>(null);
  return (
    // Stays in view while the list scrolls under it, so the holds of any boulder tapped show up where they are.
    <div className={`sector-photo ${on ? "is-lit" : ""}`}>
      <div className="sector-photo__frame" style={ratio ? ({ "--ratio": ratio } as CSSProperties) : undefined}>
        <img src={sector.imageUrl!} alt={t("Photo of {sector}", { sector: sector.name })} className="sector-photo__wall"
          style={on && backdrop < 0 ? { filter: `grayscale(1) brightness(${1 + backdrop * BACKDROP_REACH})` } : undefined}
          onLoad={(e) => { const i = e.currentTarget; if (i.naturalWidth && i.naturalHeight) setRatio(i.naturalWidth / i.naturalHeight); }} />
        {/* Lighter: the grey wall mixed with white, as the editor does. */}
        {on && backdrop > 0 && <span className="sector-photo__lighten" aria-hidden style={{ opacity: backdrop * BACKDROP_REACH }} />}
        {on && (
          <img src={sector.imageUrl!} alt="" aria-hidden className="sector-photo__holds"
            style={{ WebkitMaskImage: `url(${look.data!.holds})`, maskImage: `url(${look.data!.holds})` }} />
        )}
        {on && look.data!.outline && <img src={look.data!.outline} alt="" aria-hidden className="sector-photo__outline" />}
        {lit && look.isPending && <span className="sector-photo__note" role="status">{t("Finding the holds…")}</span>}
        {lit && look.isError && <span className="sector-photo__note" role="status">{t("The holds of this boulder can't be shown on the photo.")}</span>}
      </div>
    </div>
  );
}

/**
 * A sector opened from the gym's map, right under it: its name, following it, its photo and its boulders. A boulder
 * highlighted on the sector's photo lights its holds up there when tapped, and offers to open it; any other opens
 * straight away.
 */
export function SectorPanel({ sector, signedIn, onToggleFollow, onClose, onShowBoulders, litId, onLight }: {
  sector: Sector; signedIn: boolean; onToggleFollow: () => void; onClose: () => void; onShowBoulders: () => void;
  /** The boulder whose holds are lit up, kept by the page so it is still lit on coming back to it. */
  litId: string | null; onLight: (boulderId: string | null) => void;
}) {
  const boulders = useBoulders(sector.gymId, { sectorId: sector.id }, { sort: "GRADE" });
  const items = boulders.data?.pages.flatMap((p) => p.items) ?? [];
  const total = boulders.data?.pages[0]?.total ?? 0;
  const lit = items.find((b) => b.id === litId) ?? null;
  const ref = useRef<HTMLElement>(null);
  const hasPhoto = !!sector.imageUrl;
  const onPhoto = hasPhoto ? items.filter((b) => b.holdsOnSectorPhoto).length : 0;

  // Opened from the map, or come back to: always the same place — its name right under the gym's tabs, the photo
  // under it. The photo and the boulders arrive a moment later and push things about, so the panel is aligned again
  // as it grows, until the person scrolls by themselves; and the page keeps enough room under it to get there even
  // when the sector is short (no photo, no boulders).
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const page = el.closest<HTMLElement>(".page");
    let moved = false;
    const stop = () => { moved = true; };
    const below = () => {
      const tabs = document.querySelector<HTMLElement>(".tabs");
      return tabs ? tabs.offsetHeight + (parseFloat(getComputedStyle(tabs).top) || 0) + 8 : 8;
    };
    // Room at the foot of the page, enough for the panel's top to reach its place however little follows it.
    const fit = () => {
      if (!page) return;
      page.style.paddingBottom = "";
      const base = parseFloat(getComputedStyle(page).paddingBottom) || 0;
      const nav = document.querySelector<HTMLElement>(".app-shell__nav")?.offsetHeight ?? 0;
      const after = document.documentElement.scrollHeight - (el.getBoundingClientRect().top + window.scrollY);
      const need = Math.ceil(window.innerHeight - below() - nav - after);
      if (need > 0) page.style.paddingBottom = `${base + need}px`;
    };
    const align = () => {
      if (moved) return;
      fit();
      window.requestAnimationFrame(() => {
        if (!moved && typeof window.scrollTo === "function")
          window.scrollTo({ top: el.getBoundingClientRect().top + window.scrollY - below(), behavior: "smooth" });
      });
    };
    align();
    const observer = typeof ResizeObserver === "function" ? new ResizeObserver(align) : null;
    observer?.observe(el);
    // Aligned while it settles, not for ever: after that a change in size (a boulder lit) leaves the page where it is.
    const settled = window.setTimeout(() => observer?.disconnect(), 2500);
    window.addEventListener("touchstart", stop, { passive: true });
    window.addEventListener("wheel", stop, { passive: true });
    return () => {
      observer?.disconnect();
      window.clearTimeout(settled);
      if (page) page.style.paddingBottom = "";
      window.removeEventListener("touchstart", stop);
      window.removeEventListener("wheel", stop);
    };
  }, [sector.id]);

  return (
    <section ref={ref} className="sector-panel" aria-labelledby="sector-panel-title">
      <div className="sector-panel__head">
        <div className="sector-panel__text">
          <h2 id="sector-panel-title" className="section__title">{sector.name}</h2>
          <p className="list__sub">{total > 0 ? plural(total, "1 on the wall", "{count} on the wall") : t("No boulders on the wall")}</p>
        </div>
        {signedIn && (
          <button type="button" className={`btn follow-btn ${sector.isFollowing ? "btn--secondary is-following" : "btn--primary"}`} aria-pressed={sector.isFollowing} onClick={onToggleFollow}>
            {sector.isFollowing ? <Check aria-hidden /> : <Plus aria-hidden />} {sector.isFollowing ? t("Following") : t("Follow")}
          </button>
        )}
        <button type="button" className="icon-btn" onClick={onClose} aria-label={t("Close")}><X aria-hidden /></button>
      </div>
      {sector.description && <p className="sector-sheet__text">{sector.description}</p>}

      {hasPhoto && <SectorPhoto sector={sector} lit={lit} />}
      {hasPhoto && onPhoto > 0 && (
        <p className="sector-panel__hint">{lit ? t("Tap it again to see the whole wall.") : t("Tap a boulder to light its holds up on the photo.")}</p>
      )}

      {boulders.isPending ? <LoadingState label={t("Loading boulders")} />
        : boulders.isError ? <ErrorState error={boulders.error} onRetry={() => boulders.refetch()} />
        : items.length > 0 && (
          <ul className="sector-panel__list">
            {items.map((b) => (
              <li key={b.id}>
                <PanelRow boulder={b} canLight={hasPhoto && !!b.holdsOnSectorPhoto} lit={b.id === litId}
                  onLight={() => onLight(litId === b.id ? null : b.id)} />
              </li>
            ))}
          </ul>
        )}
      {total > items.length && (
        <button type="button" className="text-btn sector-panel__more" onClick={onShowBoulders}>{t("See all the boulders")} <ChevronRight aria-hidden /></button>
      )}
    </section>
  );
}

/** One boulder of the panel: a tap lights its holds up (when it can), and the arrow opens it. */
function PanelRow({ boulder, canLight, lit, onLight }: { boulder: BoulderSummary; canLight: boolean; lit: boolean; onLight: () => void }) {
  const primary = boulder.grades[0];
  const holds = t("{colour} holds", { colour: holdColorName(boulder.holdColor) });
  const name = boulder.name?.trim() || null;
  const cells = (
    <>
      <span className="boulder-row__thumb">
        {boulder.photoUrl ? <img src={boulder.photoUrl} alt="" loading="lazy" decoding="async" draggable={false} /> : <MountainSnow aria-hidden />}
        {boulder.isNew && <span className="new-tag new-tag--row">{t("New")}</span>}
      </span>
      <span className="boulder-row__grade">{primary ? <GradeBadge grade={primary} size="sm" /> : <span className="boulder-row__none" aria-hidden>–</span>}</span>
      <span className="sector-panel__name">{name ?? holds}</span>
      <span className="boulder-row__holds" aria-hidden><HoldIcon color={boulder.holdColor} /></span>
      {canLight && <span className={`sector-panel__lamp ${lit ? "is-on" : ""}`} aria-hidden><Sparkles /></span>}
    </>
  );
  const label = [primary ? dataLabel(primary.label) : t("Ungraded"), name, holds].filter(Boolean).join(", ");
  if (!canLight) {
    return <Link to={`/boulders/${boulder.id}`} className="boulder-row sector-panel__row" aria-label={label}>{cells}<ChevronRight aria-hidden className="sector-panel__go" /></Link>;
  }
  return (
    <div className={`boulder-row sector-panel__row ${lit ? "is-lit" : ""}`}>
      <button type="button" className="sector-panel__pick" aria-pressed={lit} onClick={onLight}
        aria-label={lit ? t("{boulder}: holds lit up on the photo", { boulder: label }) : t("Light up the holds of {boulder}", { boulder: label })}>
        {cells}
      </button>
      <Link to={`/boulders/${boulder.id}`} className="sector-panel__open" aria-label={t("Open {boulder}", { boulder: label })}><ChevronRight aria-hidden /></Link>
    </div>
  );
}
