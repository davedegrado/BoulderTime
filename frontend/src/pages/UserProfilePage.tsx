import { Link, useParams } from "react-router-dom";
import { useProfile } from "@/features/climbing/api";
import { ClimbingActivity } from "@/pages/ActivityPage";
import { Avatar } from "@/components/Avatar";
import { GymAvatar } from "@/components/GymAvatar";
import { formatDate } from "@/lib/format";
import { t } from "@/i18n/i18n";

export function UserProfilePage() {
  const { id } = useParams();
  const profile = useProfile(id);
  const p = profile.data;

  const header = p && (
    <header className="stack">
      <div className="profile-head">
        <Avatar name={p.displayName} url={p.avatarUrl} size={72} />
        <div>
          <h1 className="page__title">{p.displayName}</h1>
          <p className="page__subtitle">{t("On BoulderTime since {date}", { date: formatDate(p.memberSince, { month: "long", year: "numeric" }) })}</p>
        </div>
      </div>
      {p.followedGyms.length > 0 && (
        <div className="gym-chips" aria-label={t("Gyms")}>
          {p.followedGyms.map((g) => (
            <Link key={g.id} to={`/gyms/${g.slug}`} className="gym-chip"><GymAvatar name={g.name} logoUrl={g.logoUrl} size={24} /><span>{g.name}</span></Link>
          ))}
        </div>
      )}
    </header>
  );

  return <ClimbingActivity userId={id ?? ""} header={header} />;
}
