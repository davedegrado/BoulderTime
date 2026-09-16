type Variant = "horizontal" | "vertical" | "mark";

interface LogoProps {
  variant?: Variant;
  /** Use on charcoal backgrounds. */
  onDark?: boolean;
  height?: number;
  className?: string;
}

/**
 * Renders the official BoulderTime brand asset from /public/assets.
 * Never substitute a generic icon for the logo; replace the asset files instead.
 */
export function Logo({ variant = "horizontal", onDark = false, height = 32, className }: LogoProps) {
  const file = `logo-${variant}${onDark ? "-dark" : ""}.svg`;
  return <img src={`/assets/${file}`} alt="BoulderTime" height={height} style={{ height, width: "auto" }} className={className} />;
}
