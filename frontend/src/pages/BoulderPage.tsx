import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, Bell, BellRing, CalendarDays, History, Layers, Maximize2, Pencil, UserRound } from "lucide-react";
import { useBoulder } from "@/features/boulders/api";
import { ProgressTracker } from "@/features/climbing/ProgressTracker";
import { RatingSummaryText } from "@/features/climbing/ClimbingBits";
import { useFollowBoulder } from "@/features/climbing/api";
import { useAuth } from "@/auth/AuthProvider";
import { CommentsSection } from "@/features/community/CommentsSection";
import { CommunityGradeSection } from "@/features/community/CommunityGradeSection";
import { BetaSection, CommunityVideosSection } from "@/features/community/VideoSections";
import { ReportButton } from "@/features/community/ReportButton";
import { GradeBadge, HoldBadge } from "@/features/boulders/BoulderBits";
import { PhotoViewer } from "@/features/boulders/PhotoViewer";
import { ErrorState, LoadingState } from "@/components/States";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { ApiError } from "@/lib/apiError";
import { formatDate } from "@/lib/format";
import { t } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";

/**
 * One boulder. The photo comes first, because it is how a climber finds the problem on the wall, but it no longer
 * fills the screen: a tap opens it full size. Under it, everything that identifies the boulder in one block — grade,
 * holds, sector, date — then what the climber does with it (progress), then what others say (beta, grade, comments).
 */
export function BoulderPage() {
  const { id } = useParams();
  const boulder = useBoulder(id);
  const { session } = useAuth();
  const follow = useFollowBoulder(id ?? "");
  const [viewing, setViewing] = useState(false);

  if (boulder.isPending) return <LoadingState label={t("Loading boulder")} />;
  if (boulder.isError) return boulder.error instanceof ApiError && boulder.error.isNotFound ? <NotFoundPage /> : <ErrorState error={boulder.error} onRetry={() => boulder.refetch()} />;

  const b = boulder.data;
  const removed = b.status === "REMOVED";
  const [primary, ...others] = b.grades;
  const photoAlt = t("Boulder in {sector}", { sector: b.sectorName });

  return (
    <article className="page page--flush boulder-page">
      <div className="boulder-page__photo">
        {b.photoUrl && (
          <button type="button" className="boulder-page__photo-btn" onClick={() => setViewing(true)} aria-label={t("Enlarge the photo")}>
            <img src={b.photoUrl} alt={photoAlt} />
          </button>
        )}
        <Link to={`/gyms/${b.gymSlug}`} className="boulder-page__back" aria-label={t("Back to {name}", { name: b.gymName })}><ArrowLeft aria-hidden /></Link>
        {b.photoUrl && (
          <button type="button" className="boulder-page__enlarge" onClick={() => setViewing(true)} aria-label={t("Enlarge the photo")}>
            <Maximize2 aria-hidden />
          </button>
        )}
      </div>
      {viewing && b.photoUrl && <PhotoViewer src={b.photoUrl} alt={photoAlt} onClose={() => setViewing(false)} />}

      <div className="page__pad stack">
        {removed && (
          <div className="notice" role="note">
            <History aria-hidden />
            <p>{b.removedAt ? t("This boulder was removed on {date}.", { date: formatDate(b.removedAt) }) : t("This boulder was removed.")} {t("It stays here so climbers keep their history.")}</p>
          </div>
        )}

        <header className="boulder-page__head">
          <div className="boulder-page__title-row">
            <div className="boulder-page__grades">
              {primary && <GradeBadge grade={primary} size="lg" />}
              {others.map((g) => (
                <span key={g.gradeSystemId} className="boulder-page__other-grade">
                  <GradeBadge grade={g} size="md" /> <span className="boulder-page__system">{dataLabel(g.systemName)}</span>
                </span>
              ))}
            </div>
            <div className="boulder-page__actions">
              {session && (
                <button type="button" className={`icon-btn icon-btn--outlined ${b.isFollowing ? "is-following" : ""}`}
                  aria-pressed={b.isFollowing} onClick={() => follow.mutate(!b.isFollowing)}
                  aria-label={b.isFollowing ? t("Stop following this boulder") : t("Follow this boulder")}
                  title={b.isFollowing ? t("Stop following this boulder") : t("Follow this boulder")}>
                  {b.isFollowing ? <BellRing aria-hidden /> : <Bell aria-hidden />}
                </button>
              )}
              {b.viewerRole && (
                <Link to={`/manage/${b.gymSlug}/boulders/${b.id}/edit`} className="icon-btn icon-btn--outlined" aria-label={t("Edit")} title={t("Edit")}>
                  <Pencil aria-hidden />
                </Link>
              )}
            </div>
          </div>
          <p className="boulder-page__rating"><RatingSummaryText rating={b.rating} /></p>
          <ul className="facts" aria-label={t("About this boulder")}>
            <li className="fact"><HoldBadge color={b.holdColor} compact /></li>
            <li className="fact"><Layers aria-hidden /><span className="sr-only">{t("Sector")}: </span>{b.sectorName}</li>
            <li className="fact"><CalendarDays aria-hidden /><span className="sr-only">{t("Set on")}: </span>{formatDate(b.createdAt)}</li>
            {b.setter && <li className="fact"><UserRound aria-hidden /><span className="sr-only">{t("Setter")}: </span>{b.setter.displayName}</li>}
          </ul>
        </header>

        <ProgressTracker key={b.id} boulder={b} />

        <BetaSection boulderId={b.id} />
        {b.communityVideosEnabled && <CommunityVideosSection boulderId={b.id} />}
        <CommunityGradeSection boulderId={b.id} />
        <CommentsSection boulderId={b.id} />

        <div className="form__actions">
          <Link to={`/gyms/${b.gymSlug}`} className="btn btn--ghost"><span>{t("More boulders at {gym}", { gym: b.gymName })}</span></Link>
          {session && <ReportButton entityType="BOULDER" entityId={b.id} label={t("Report a problem with this boulder")} />}
        </div>
      </div>
    </article>
  );
}
