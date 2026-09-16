import { initials } from "@/lib/format";

export function GymAvatar({ name, logoUrl, size = 48 }: { name: string; logoUrl: string | null; size?: number }) {
  const style = { width: size, height: size };
  return logoUrl ? (
    <img className="gym-avatar" src={logoUrl} alt="" style={style} />
  ) : (
    <span className="gym-avatar gym-avatar--initials" style={{ ...style, fontSize: size * 0.36 }} aria-hidden>{initials(name)}</span>
  );
}
