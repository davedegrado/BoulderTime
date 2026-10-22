import { Link, useParams } from "react-router-dom";
import { useProfile } from "@/features/climbing/api";
import { ClimbingActivity } from "@/pages/ActivityPage";
import { EnlargeableAvatar } from "@/components/EnlargeableAvatar";
import { GymAvatar } from "@/components/GymAvatar";
import { formatDate } from "@/lib/format";
import { t } from "@/i18n/i18n";
import { StaffDistinctions } from "@/features/gyms/StaffDistinctions";
import { ArrowLeft, Ban } from "lucide-react";
import { useGoBack } from "@/lib/navigation";
import { ErrorState, LoadingState } from "@/components/States";
import { useAuth } from "@/auth/AuthProvider";
import { BlockAction } from "@/features/users/BlockAction";
import { ReportPersonAction } from "@/features/users/ReportPersonAction";

export function UserProfilePage() {
  const { id } = useParams();
  const { session } = useAuth();
  const profile = useProfile(id);
  const goBack = useGoBack();
  const p = profile.data;

  // Back to where the profile was opened from (a boulder's "completed by", a leaderboard, a comment); Home when it was
  // opened directly.
  const back = <button type="button" className="manage-head__back" aria-label={t("Back")} onClick={() => goBack("/")}><ArrowLeft aria-hidden /></button>;
  // Never stuck: the way back is there while the profile loads, and if it can't be loaded.
  if (profile.isPending) return <div className="page">{back}<LoadingState label={t("Loading activity")} /></div>;
  if (!p) return <div className="page">{back}<ErrorState error={profile.error} onRetry={() => profile.refetch()} /></div>;

  const header = (
    <header className="stack">
      {back}
      <div className="profile-head">
        <EnlargeableAvatar name={p.displayName} url={p.avatarUrl} size={72} />
        <div>
          <h1 className="page__title">{p.displayName}</h1>
          <p className="page__subtitle">{t("On BoulderTime since {date}", { date: formatDate(p.memberSince, { month: "long", year: "numeric" }) })}</p>
          <StaffDistinctions distinctions={p.staffDistinctions} />
        </div>
        {!p.isMe && session && <BlockAction userId={p.id} displayName={p.displayName} isBlocked={p.isBlocked} />}
      </div>
      {p.isBlocked && (
        <p className="notice notice--inline">
          <Ban aria-hidden />
          {t("You've blocked {name}. Their comments and videos are hidden from you, and yours from them. They aren't told.", { name: p.displayName })}
        </p>
      )}
      {!p.isMe && session && <ReportPersonAction userId={p.id} displayName={p.displayName} />}
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
