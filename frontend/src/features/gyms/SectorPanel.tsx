import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Link, useNavigationType } from "react-router-dom";
import { Check, ChevronRight, Maximize2, MountainSnow, Plus, X } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { BACKDROP_REACH, featherMask, outlineArea, outlineWidth } from "@/features/boulders/holdHighlight";
import { OUTLINE_COLORS, readHighlightSettings } from "@/features/boulders/HoldHighlighter";
import { useBoulder, useBoulders, type BoulderSummary } from "@/features/boulders/api";
import { GradeBadge, HoldIcon } from "@/features/boulders/BoulderBits";
import { ownState, STATE_LABEL } from "@/features/boulders/listView";
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
 * A size for a picture of these proportions in a box: as wide as the box, no taller than `maxHeight()`, in pixels —
 * so every layer drawn over the photo (the holds, their outline) lands exactly on the wall's. Left to the browser, a
 * flex box could stretch it to other proportions (Safari did), cropping the photo under a mask that is not.
 */
function useFitted(box: React.RefObject<HTMLElement>, ratio: number | null, maxHeight: () => number) {
  const [size, setSize] = useState<{ w: number; h: number } | null>(null);
  useLayoutEffect(() => {
    const el = box.current;
    if (!el || !ratio) return;
    const measure = () => {
      const room = el.clientWidth;
      if (!room) return;
      const h = Math.min(room / ratio, maxHeight());
      setSize({ w: Math.round(h * ratio), h: Math.round(h) });
    };
    measure();
    const observer = typeof ResizeObserver === "function" ? new ResizeObserver(measure) : null;
    observer?.observe(el);
    window.addEventListener("resize", measure);
    return () => { observer?.disconnect(); window.removeEventListener("resize", measure); };
  }, [box, ratio, maxHeight]);
  return size;
}

/** A photo, and over it the lit boulder's holds as staff left them (on the sector's photo only), at a size in pixels. */
function WallLayers({ src, alt, look, size, onRatio, children }: {
  src: string; alt: string; look: HoldsLook | null; size: { w: number; h: number } | null; onRatio: (ratio: number) => void; children?: React.ReactNode;
}) {
  const backdrop = look?.backdrop ?? 0;
  return (
    <div className="sector-wall__frame" style={size ? { width: size.w, height: size.h } : undefined}>
      <img src={src} alt={alt} className="sector-wall__wall"
        style={look && backdrop < 0 ? { filter: `grayscale(1) brightness(${1 + backdrop * BACKDROP_REACH})` } : undefined}
        onLoad={(e) => { const i = e.currentTarget; if (i.naturalWidth && i.naturalHeight) onRatio(i.naturalWidth / i.naturalHeight); }} />
      {/* Lighter: the grey wall mixed with white, as the editor does. */}
      {look && backdrop > 0 && <span className="sector-wall__lighten" aria-hidden style={{ opacity: backdrop * BACKDROP_REACH }} />}
      {look && (
        <img src={src} alt="" aria-hidden className="sector-wall__holds"
          style={{ WebkitMaskImage: `url(${look.holds})`, maskImage: `url(${look.holds})` }} />
      )}
      {look?.outline && <img src={look.outline} alt="" aria-hidden className="sector-wall__outline" />}
      {children}
    </div>
  );
}

/** Room kept under an open panel beyond what reaching its place needs (see the panel's room). */
const SLACK = 160;

/** The inline photo is kept small, so the boulders under it stay in sight; a tap shows it full screen. */
const inlineHeight = () => (window.innerWidth >= WIDE ? Math.max(320, window.innerHeight - 220) : Math.max(140, window.innerHeight * 0.26));

/** From this width (a tablet, a computer) the sector's photo sits large on the left and its boulders on the right. */
const WIDE = 720;

/**
 * The picture at the panel's top: the sector's photo, with the chosen boulder's holds lit up on it when it was
 * highlighted there; or, for a boulder whose photo is its own, that photo in its place, said so. The photo itself is
 * never read, only shown in layers — so it works whatever the storage says about reading it.
 */
function TopPhoto({ sector, chosen, onWall }: { sector: Sector; chosen: BoulderSummary | null; onWall: boolean }) {
  const look = useHoldsLook(onWall ? chosen : null);
  // A boulder's own photo: its full one, from its page's data (the list only has its card picture, shown meanwhile).
  const own = useBoulder(chosen && !onWall ? chosen.id : undefined);
  const ownPhoto = chosen && !onWall ? own.data?.photoUrl ?? chosen.photoUrl : null;
  const src = ownPhoto ?? sector.imageUrl;
  if (!src) return null;
  const alt = ownPhoto ? t("Photo of the boulder") : t("Photo of {sector}", { sector: sector.name });
  const shown = onWall && chosen && look.data ? look.data : null;
  return (
    <FramedPhoto key={src} src={src} alt={alt} look={shown} grey={onWall && !!chosen}>
      {ownPhoto && <span className="sector-wall__tag">{t("Photo of the boulder")}</span>}
      {onWall && chosen && look.isPending && <span className="sector-wall__note" role="status">{t("Finding the holds…")}</span>}
      {onWall && chosen && look.isError && <span className="sector-wall__note" role="status">{t("The holds of this boulder can't be shown on the photo.")}</span>}
    </FramedPhoto>
  );
}

/** One photo at the panel's top, sized to its own proportions; a tap shows it full screen. */
function FramedPhoto({ src, alt, look, grey, children }: { src: string; alt: string; look: HoldsLook | null; grey: boolean; children?: React.ReactNode }) {
  const [ratio, setRatio] = useState<number | null>(null);
  const [big, setBig] = useState(false);
  const box = useRef<HTMLButtonElement>(null);
  const size = useFitted(box, ratio, inlineHeight);
  return (
    // Stays in view while the list scrolls under it, so whatever boulder is tapped shows up there.
    <>
      <button ref={box} type="button" className={`sector-wall ${grey ? "is-lit" : ""}`} onClick={() => setBig(true)}
        aria-label={t("Show the photo full screen")}>
        <WallLayers src={src} alt={alt} look={look} size={size} onRatio={setRatio}>
          <span className="sector-wall__enlarge" aria-hidden><Maximize2 /></span>
          {children}
        </WallLayers>
      </button>
      {/* Over everything, the tab bar included: opened outside the panel, whose sticky top would keep it under. */}
      {big && createPortal(<WallViewer src={src} alt={alt} look={look} grey={grey} ratio={ratio} onClose={() => setBig(false)} />, document.body)}
    </>
  );
}

/** A photo full screen, with the same holds lit up, fitted to the screen whole. */
function WallViewer({ src, alt, look, grey, ratio, onClose }: {
  src: string; alt: string; look: HoldsLook | null; grey: boolean; ratio: number | null; onClose: () => void;
}) {
  const stage = useRef<HTMLDivElement>(null);
  const [own, setOwn] = useState<number | null>(ratio);
  const fullHeight = useCallback(() => stage.current?.clientHeight ?? window.innerHeight, []);
  const size = useFitted(stage, own, fullHeight);
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", onKey);
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { window.removeEventListener("keydown", onKey); document.body.style.overflow = overflow; };
  }, [onClose]);
  return (
    <div className={`photo-viewer wall-viewer ${grey ? "is-lit" : ""}`} role="dialog" aria-modal="true" aria-label={alt}>
      <div className="viewer__top">
        <button type="button" className="viewer__close" onClick={onClose} aria-label={t("Close")}><X aria-hidden /></button>
      </div>
      <div ref={stage} className="wall-viewer__stage" onClick={onClose}>
        <WallLayers src={src} alt={alt} look={look} size={size} onRatio={setOwn} />
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
  // Known from the sector before its boulders arrive: never "no boulders" while they load.
  const count = boulders.data ? total : sector.activeBoulders ?? 0;
  const lit = items.find((b) => b.id === litId) ?? null;
  const ref = useRef<HTMLElement>(null);
  // Back from a boulder opened here, the page comes back already in place rather than moving to it.
  const comingBack = useNavigationType() === "POP";
  const hasPhoto = !!sector.imageUrl;
  // A tap shows the boulder at the top: its holds lit up on the sector's photo when it was highlighted there, its own
  // photo otherwise. Only a boulder with neither (no photo at all) just opens.
  const onWall = (b: BoulderSummary) => hasPhoto && !!b.holdsOnSectorPhoto;
  const canPick = (b: BoulderSummary) => onWall(b) || !!b.photoUrl;
  const pickable = items.some(canPick);

  // Opened from the map, or come back to: always the same place — its name right under the gym's tabs, the photo
  // under it. The photo and the boulders arrive a moment later and push things about, so the panel is aligned again
  // as it grows, until the person scrolls by themselves; and the page keeps enough room under it to get there even
  // when the sector is short (no photo, no boulders).
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const page = el.closest<HTMLElement>(".page");
    const below = () => {
      const tabs = document.querySelector<HTMLElement>(".tabs");
      return tabs ? tabs.offsetHeight + (parseFloat(getComputedStyle(tabs).top) || 0) + 8 : 8;
    };
    // Room at the foot of the page, enough for the panel's top to reach its place however short the panel gets: an
    // empty sector shrinks when "Loading boulders" goes, and a page that gets shorter than the move needs stops it
    // halfway (Safari does). While it settles the room counts only the panel's top (name and photo); once settled it
    // shrinks to what the page really needs.
    // The room is only ever set to its new value, never taken away to be measured again: a page that got shorter for
    // an instant would stop the move under way.
    const top = el.querySelector<HTMLElement>(".sector-panel__top");
    const base = page ? parseFloat(getComputedStyle(page).paddingBottom) || 0 : 0;
    let extra = 0;
    let settling = true;
    // The one moment the room may shrink after settling: right then, to what the page really needs.
    let resized = false;
    const fit = () => {
      if (!page) return;
      const nav = document.querySelector<HTMLElement>(".app-shell__nav")?.offsetHeight ?? 0;
      const panelTop = el.getBoundingClientRect().top + window.scrollY;
      const content = document.documentElement.scrollHeight - extra;
      const after = content - panelTop - (settling ? el.offsetHeight - (top?.offsetHeight ?? 0) : 0);
      // Plus a margin, so that the panel getting a little shorter (a hint going from two lines to one, a photo
      // briefly smaller) never makes the page shorter than where the person is, which would pull it up.
      const need = Math.max(0, Math.ceil(window.innerHeight - below() - nav - after)) + SLACK;
      // Once settled the room only grows: a photo at the top briefly shorter (one boulder's replacing another's) must
      // not make the page shorter under the person, which would pull it up.
      if (need === extra || (!settling && !resized && need < extra)) return;
      resized = false;
      extra = need;
      page.style.paddingBottom = need > 0 ? `${base + need}px` : "";
    };
    const target = () => el.getBoundingClientRect().top + window.scrollY - below();
    let moved = false;
    const stop = () => { moved = true; };
    fit();
    // One move, straight to its place: smooth when the sector is chosen, at once when coming back to it (back from a
    // boulder), so the page doesn't show its top first.
    if (typeof window.scrollTo === "function") window.scrollTo({ top: target(), behavior: comingBack ? "auto" : "smooth" });
    const observer = typeof ResizeObserver === "function" ? new ResizeObserver(fit) : null;
    observer?.observe(el);
    window.addEventListener("touchstart", stop, { passive: true });
    window.addEventListener("wheel", stop, { passive: true });
    // Settled: if something still stopped the move short and the person hasn't taken over, put it in place.
    const settle = window.setTimeout(() => {
      settling = false;
      if (!moved && typeof window.scrollTo === "function" && Math.abs(window.scrollY - target()) > 2) window.scrollTo({ top: target(), behavior: "auto" });
      resized = true;
      fit();
    }, 900);
    return () => {
      observer?.disconnect();
      window.clearTimeout(settle);
      window.removeEventListener("touchstart", stop);
      window.removeEventListener("wheel", stop);
      if (page) page.style.paddingBottom = "";
    };
  }, [sector.id]);

  return (
    <section ref={ref} className="sector-panel" aria-labelledby="sector-panel-title">
      {/* Name, follow, close and the photo stay together at the top while the boulders scroll under them. */}
      <div className="sector-panel__top">
        <div className="sector-panel__head">
          <div className="sector-panel__text">
            <h2 id="sector-panel-title" className="section__title">{sector.name}</h2>
            <p className="list__sub">{count > 0 ? plural(count, "1 on the wall", "{count} on the wall") : t("No boulders on the wall")}</p>
          </div>
          {signedIn && (
            <button type="button" className={`btn follow-btn ${sector.isFollowing ? "btn--secondary is-following" : "btn--primary"}`} aria-pressed={sector.isFollowing} onClick={onToggleFollow}>
              {sector.isFollowing ? <Check aria-hidden /> : <Plus aria-hidden />} {sector.isFollowing ? t("Following") : t("Follow")}
            </button>
          )}
          <button type="button" className="icon-btn" onClick={onClose} aria-label={t("Close")}><X aria-hidden /></button>
        </div>
        <TopPhoto sector={sector} chosen={lit} onWall={!!lit && onWall(lit)} />
      </div>
      {sector.description && <p className="sector-sheet__text">{sector.description}</p>}
      {pickable && (
        <p className="sector-panel__hint">{lit ? (hasPhoto ? t("Tap it again to see the whole wall.") : t("Tap it again to hide its photo.")) : t("Tap a boulder to see it up here; the arrow opens it.")}</p>
      )}

      {boulders.isPending ? <LoadingState label={t("Loading boulders")} />
        : boulders.isError ? <ErrorState error={boulders.error} onRetry={() => boulders.refetch()} />
        : items.length > 0 && (
          <ul className="sector-panel__list">
            {items.map((b) => (
              <li key={b.id}>
                <PanelRow boulder={b} canPick={canPick(b)} onWall={onWall(b)} lit={b.id === litId}
                  onLight={() => onLight(litId === b.id ? null : b.id)} showOwnState={signedIn} />
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

/** One boulder of the panel: a tap shows it at the top (its holds on the wall, or its own photo), and the arrow opens it. */
function PanelRow({ boulder, canPick, onWall, lit, onLight, showOwnState }: {
  boulder: BoulderSummary; canPick: boolean; onWall: boolean; lit: boolean; onLight: () => void; showOwnState: boolean;
}) {
  const primary = boulder.grades[0];
  // The climber's own state as a bar on the row's left edge, as in the gym's list of boulders.
  const state = showOwnState ? ownState(boulder) : null;
  const stateClass = state ? ` boulder-row--${state}` : "";
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
    </>
  );
  const label = [primary ? dataLabel(primary.label) : t("Ungraded"), name, holds, state && STATE_LABEL[state] ? t(STATE_LABEL[state]!) : null].filter(Boolean).join(", ");
  if (!canPick) {
    return <Link to={`/boulders/${boulder.id}`} className={`boulder-row sector-panel__row${stateClass}`} aria-label={label}>{cells}<ChevronRight aria-hidden className="sector-panel__go" /></Link>;
  }
  return (
    <div className={`boulder-row sector-panel__row sector-panel__row--pick${stateClass} ${lit ? "is-lit" : ""}`}>
      <button type="button" className="sector-panel__pick" aria-pressed={lit} onClick={onLight}
        aria-label={onWall
          ? (lit ? t("{boulder}: holds lit up on the photo", { boulder: label }) : t("Light up the holds of {boulder}", { boulder: label }))
          : (lit ? t("{boulder}: its photo shown", { boulder: label }) : t("Show the photo of {boulder}", { boulder: label }))}>
        {cells}
      </button>
      <Link to={`/boulders/${boulder.id}`} className="sector-panel__open" aria-label={t("Open {boulder}", { boulder: label })}><ChevronRight aria-hidden /></Link>
    </div>
  );
}
