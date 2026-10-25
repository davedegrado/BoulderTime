import { useState } from "react";

/**
 * The one place that turns a badge's picture key into a picture (ADR-069). Pictures live in `public/badges/<key>.png`:
 * replacing a file there changes the badge everywhere, without touching the badge, its rules or who earned it. A key
 * with no file shows the generic picture; if even that fails, a plain disc.
 */
export const badgeArtUrl = (art: string) => `/badges/${encodeURIComponent(art)}.png`;
const PLACEHOLDER = "/badges/placeholder.svg";

export function BadgeArt({ art, size = 56, alt = "" }: { art: string; size?: number; alt?: string }) {
  const [src, setSrc] = useState(() => (art ? badgeArtUrl(art) : PLACEHOLDER));
  const [failed, setFailed] = useState(false);
  if (failed) return <span className="badge-art badge-art--none" style={{ width: size, height: size }} role={alt ? "img" : undefined} aria-label={alt || undefined} aria-hidden={alt ? undefined : true} />;
  return (
    <img className="badge-art" src={src} width={size} height={size} alt={alt} draggable={false} loading="lazy"
      onError={() => (src === PLACEHOLDER ? setFailed(true) : setSrc(PLACEHOLDER))} />
  );
}
