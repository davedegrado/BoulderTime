type Variant = "horizontal" | "icon";

interface LogoProps {
  /** "horizontal": full wordmark lockup. "icon": the official app icon. */
  variant?: Variant;
  /** Horizontal logo only: use the light-on-dark version on charcoal backgrounds. */
  onDark?: boolean;
  height?: number;
  className?: string;
}

/**
 * Renders the official BoulderTime brand assets from /public/assets.
 * Assets are derived from the originals in docs/brand/originals — never redraw or substitute them.
 */
export function Logo({ variant = "horizontal", onDark = false, height = 32, className }: LogoProps) {
  const src = variant === "icon" ? "/assets/app-icon-512.png" : `/assets/logo-horizontal${onDark ? "-dark" : ""}.png`;
  return (
    <img
      src={src}
      alt="BoulderTime"
      height={height}
      style={{ height, width: "auto" }}
      className={className}
      decoding="async"
    />
  );
}
