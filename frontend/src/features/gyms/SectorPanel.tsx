import { useEffect, useRef, useState, type CSSProperties } from "react";
import { Link } from "react-router-dom";
import { Check, ChevronRight, MountainSnow, Plus, Sparkles, X } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useBoulders, type BoulderSummary } from "@/features/boulders/api";
import { GradeBadge, HoldIcon } from "@/features/boulders/BoulderBits";
import { holdColorName } from "@/features/boulders/holdColors";
import type { Sector } from "@/features/gyms/api";
import { LoadingState, ErrorState } from "@/components/States";
import { dataLabel } from "@/i18n/data";
import { plural, t } from "@/i18n/i18n";

/**
 * Where a boulder's holds are on its sector's photo, as a mask the browser can draw with: the API's black and white
 * picture turned into transparency (white holds, transparent wall). Read through the API rather than from storage so
 * its pixels can be read; kept for the session, a mask only changes with a new highlight.
 */
const masks = new Map<string, Promise<string>>();
function maskUrl(boulderId: string): Promise<string> {
  let found = masks.get(boulderId);
  if (!found) {
    found = (async () => {
      const blob = await api.blob(`/api/boulders/${boulderId}/holds-on-sector-photo`);
      const bitmap = await createImageBitmap(blob);
      const canvas = document.createElement("canvas");
      canvas.width = bitmap.width;
      canvas.height = bitmap.height;
      const ctx = canvas.getContext("2d");
      if (!ctx) throw new Error("no canvas");
      ctx.drawImage(bitmap, 0, 0);
      bitmap.close?.();
      const img = ctx.getImageData(0, 0, canvas.width, canvas.height);
      for (let i = 0; i < img.data.length; i += 4) { img.data[i + 3] = img.data[i]!; img.data[i] = img.data[i + 1] = img.data[i + 2] = 255; }
      ctx.putImageData(img, 0, 0);
      const alpha = await new Promise<Blob>((resolve, reject) => canvas.toBlob((b) => (b ? resolve(b) : reject(new Error("no mask"))), "image/png"));
      return URL.createObjectURL(alpha);
    })();
    found.catch(() => masks.delete(boulderId));
    masks.set(boulderId, found);
  }
  return found;
}

function useHoldsMask(boulderId: string | null) {
  return useQuery({ queryKey: ["holds-on-sector-photo", boulderId], queryFn: () => maskUrl(boulderId!), enabled: !!boulderId, staleTime: Infinity, retry: false });
}

/**
 * The sector's photo, and one boulder's holds lit up on it: the wall turns black and white and the holds keep their
 * colour, the same look as the boulder's own photo. The photo itself is never read, only shown twice — so it works
 * whatever the storage says about reading it.
 */
function SectorPhoto({ sector, lit }: { sector: Sector; lit: BoulderSummary | null }) {
  const mask = useHoldsMask(lit?.id ?? null);
  const on = !!lit && !!mask.data;
  // The frame takes the photo's own proportions, so the holds drawn over it land exactly on the wall's.
  const [ratio, setRatio] = useState<number | null>(null);
  return (
    // Stays in view while the list scrolls under it, so the holds of any boulder tapped show up where they are.
    <div className={`sector-photo ${on ? "is-lit" : ""}`}>
      <div className="sector-photo__frame" style={ratio ? ({ "--ratio": ratio } as CSSProperties) : undefined}>
        <img src={sector.imageUrl!} alt={t("Photo of {sector}", { sector: sector.name })} className="sector-photo__wall"
          onLoad={(e) => { const i = e.currentTarget; if (i.naturalWidth && i.naturalHeight) setRatio(i.naturalWidth / i.naturalHeight); }} />
        {/* The holds in colour over the black and white wall, with a glow around them that the mask shapes: they stand
            out whatever their colour, black holds on a dark wall included. */}
        {on && (
          <span className="sector-photo__glow" aria-hidden>
            <img src={sector.imageUrl!} alt="" className="sector-photo__holds" style={{ WebkitMaskImage: `url(${mask.data})`, maskImage: `url(${mask.data})` }} />
          </span>
        )}
        {lit && mask.isPending && <span className="sector-photo__note" role="status">{t("Finding the holds…")}</span>}
        {lit && mask.isError && <span className="sector-photo__note" role="status">{t("The holds of this boulder can't be shown on the photo.")}</span>}
      </div>
    </div>
  );
}

/**
 * A sector opened from the gym's map, right under it: its name, following it, its photo and its boulders. A boulder
 * highlighted on the sector's photo lights its holds up there when tapped, and offers to open it; any other opens
 * straight away.
 */
export function SectorPanel({ sector, signedIn, onToggleFollow, onClose, onShowBoulders }: {
  sector: Sector; signedIn: boolean; onToggleFollow: () => void; onClose: () => void; onShowBoulders: () => void;
}) {
  const boulders = useBoulders(sector.gymId, { sectorId: sector.id }, { sort: "GRADE" });
  const items = boulders.data?.pages.flatMap((p) => p.items) ?? [];
  const total = boulders.data?.pages[0]?.total ?? 0;
  const [litId, setLitId] = useState<string | null>(null);
  const lit = items.find((b) => b.id === litId) ?? null;
  const ref = useRef<HTMLElement>(null);
  const hasPhoto = !!sector.imageUrl;
  const onPhoto = hasPhoto ? items.filter((b) => b.holdsOnSectorPhoto).length : 0;

  // Opened from the map: brought into view, with nothing lit yet.
  useEffect(() => {
    setLitId(null);
    ref.current?.scrollIntoView?.({ behavior: "smooth", block: "start" });
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
                  onLight={() => setLitId((id) => (id === b.id ? null : b.id))} />
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
