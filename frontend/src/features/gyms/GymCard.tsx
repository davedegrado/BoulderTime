import { Link } from "react-router-dom";
import { MapPin } from "lucide-react";
import type { GymSummary } from "@/features/gyms/api";
import { GymAvatar } from "@/components/GymAvatar";

export function GymCard({ gym }: { gym: GymSummary }) {
  return (
    <Link to={`/gyms/${gym.slug}`} className="gym-card">
      <div className="gym-card__cover" style={gym.coverImageUrl ? { backgroundImage: `url(${gym.coverImageUrl})` } : undefined}>
        <GymAvatar name={gym.name} logoUrl={gym.logoUrl} size={52} />
      </div>
      <div className="gym-card__body">
        <h3 className="gym-card__name">{gym.name}</h3>
        <p className="gym-card__meta"><MapPin aria-hidden /> {gym.city}</p>
      </div>
    </Link>
  );
}
