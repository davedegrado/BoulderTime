import { Link, useParams } from "react-router-dom";
import { ArrowLeft, CalendarDays, History, Layers, Pencil, UserRound } from "lucide-react";
import { useBoulder } from "@/features/boulders/api";
import { ProgressTracker } from "@/features/climbing/ProgressTracker";
import { FollowButton, RatingSummaryText } from "@/features/climbing/ClimbingBits";
import { useFollowBoulder } from "@/features/climbing/api";
import { useAuth } from "@/auth/AuthProvider";
import { CommentsSection } from "@/features/community/CommentsSection";
import { CommunityGradeSection } from "@/features/community/CommunityGradeSection";
import { BetaSection, CommunityVideosSection } from "@/features/community/VideoSections";
import { ReportButton } from "@/features/community/ReportButton";
import { GradeBadge, HoldBadge } from "@/features/boulders/BoulderBits";
import { ErrorState, LoadingState } from "@/components/States";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { ApiError } from "@/lib/apiError";
import { formatDate } from "@/lib/format";
import { t } from "@/i18n/i18n";

export function BoulderPage() {
  const { id } = useParams();
  const boulder = useBoulder(id);
  const { session } = useAuth();
  const follow = useFollowBoulder(id ?? "");

  if (boulder.isPending) return <LoadingState label={t("Loading boulder")} />;
  if (boulder.isError) return boulder.error instanceof ApiError && boulder.error.isNotFound ? <NotFoundPage /> : <ErrorState error={boulder.error} onRetry={() => boulder.refetch()} />;

  const b = boulder.data;
  const removed = b.status === "REMOVED";
  const [primary, ...others] = b.grades;

  return (
    <article className="page page--flush boulder-page">
      <div className="boulder-page__photo">
        <img src={b.photoUrl} alt={`Boulder in ${b.sectorName}`} />
        <Link to={`/gyms/${b.gymSlug}`} className="boulder-page__back" aria-label={`Back to ${b.gymName}`}><ArrowLeft aria-hidden /></Link>
      </div>

      <div className="page__pad stack">
        {removed && (
          <div className="notice" role="note">
            <History aria-hidden />
            <p>This boulder was removed{b.removedAt ? ` on ${formatDate(b.removedAt)}` : ""}. It stays here so climbers keep their history.</p>
          </div>
        )}

        <header className="boulder-page__head">
          <div className="boulder-page__grades">
            <p className="boulder-page__caption">{t("Grade")}</p>
            {primary && <GradeBadge grade={primary} size="lg" />}
            {others.length > 0 && (
              <p className="boulder-page__other-grades">
                {others.map((g) => <span key={g.gradeSystemId}><GradeBadge grade={g} size="sm" /> <span className="list__sub">{g.systemName}</span></span>)}
              </p>
            )}
          </div>
          <div className="boulder-page__actions">
            {session && <FollowButton following={b.isFollowing} onToggle={() => follow.mutate(!b.isFollowing)} />}
            {b.viewerRole && (
              <Link to={`/manage/${b.gymSlug}/boulders/${b.id}/edit`} className="btn btn--secondary"><Pencil aria-hidden /><span>{t("Edit")}</span></Link>
            )}
          </div>
        </header>
        <p className="boulder-page__rating"><RatingSummaryText rating={b.rating} /></p>

        <ProgressTracker key={b.id} boulder={b} />

        <ul className="list">
          <li className="list__row">
            <span className="list__lead"><HoldBadge color={b.holdColor} /></span>
          </li>
          <li className="list__row">
            <Layers className="list__icon" aria-hidden />
            <div className="list__main"><p className="list__sub">{t("Sector")}</p><p className="list__title">{b.sectorName}</p></div>
          </li>
          {b.setter && (
            <li className="list__row">
              <UserRound className="list__icon" aria-hidden />
              <div className="list__main"><p className="list__sub">{t("Setter")}</p><p className="list__title">{b.setter.displayName}</p></div>
            </li>
          )}
          <li className="list__row">
            <CalendarDays className="list__icon" aria-hidden />
            <div className="list__main"><p className="list__sub">{t("Set on")}</p><p className="list__title">{formatDate(b.createdAt)}</p></div>
          </li>
        </ul>

        <BetaSection boulderId={b.id} isStaff={!!b.viewerRole} />
        <CommunityVideosSection boulderId={b.id} />
        <CommunityGradeSection boulderId={b.id} />
        <CommentsSection boulderId={b.id} />

        <div className="form__actions">
          <Link to={`/gyms/${b.gymSlug}`} className="btn btn--ghost"><span>More boulders at {b.gymName}</span></Link>
          {session && <ReportButton entityType="BOULDER" entityId={b.id} label={t("Report a problem with this boulder")} />}
        </div>
      </div>
    </article>
  );
}
