/**
 * Instagram and Facebook marks, drawn here because the icon library dropped brand icons.
 * Single-colour and stroke-width matched to the lucide icons they sit next to in lists.
 */
const base = {
  width: 24, height: 24, viewBox: "0 0 24 24", fill: "none",
  stroke: "currentColor", strokeWidth: 2, strokeLinecap: "round", strokeLinejoin: "round",
} as const;

export function InstagramIcon(props: { className?: string; "aria-hidden"?: boolean }) {
  return (
    <svg {...base} {...props}>
      <rect x="2" y="2" width="20" height="20" rx="5" />
      <circle cx="12" cy="12" r="4" />
      <circle cx="17.5" cy="6.5" r="1" fill="currentColor" stroke="none" />
    </svg>
  );
}

export function FacebookIcon(props: { className?: string; "aria-hidden"?: boolean }) {
  return (
    <svg {...base} {...props}>
      <circle cx="12" cy="12" r="10" />
      {/* The lower-case f: down stroke, crossbar, and the curve into the top. */}
      <path d="M14.5 8.2h-1.3c-1 0-1.6.6-1.6 1.6V12m0 0v5.2M11.6 12h3" />
    </svg>
  );
}
