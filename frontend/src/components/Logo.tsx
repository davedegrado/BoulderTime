import { useTheme } from "@/lib/theme";

type Variant = "horizontal" | "icon";

interface LogoProps {
  /** "horizontal": full wordmark lockup. "icon": the official app icon. */
  variant?: Variant;
  /** Horizontal logo only: use the light-on-dark version on charcoal backgrounds (always used in the dark theme). */
  onDark?: boolean;
  height?: number;
  className?: string;
}

/**
 * Renders the official BoulderTime brand assets from /public/assets.
 * Assets are derived from the originals in docs/brand/originals — never redraw or substitute them.
 */
export function Logo({ variant = "horizontal", onDark = false, height = 32, className }: LogoProps) {
  const { theme } = useTheme();
  const dark = onDark || theme === "dark";
  const src = variant === "icon" ? "/assets/app-icon-512.png" : `/assets/logo-horizontal${dark ? "-dark" : ""}.png`;
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
