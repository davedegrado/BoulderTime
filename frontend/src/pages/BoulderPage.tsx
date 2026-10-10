import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, Bell, BellRing, History, Maximize2, Pencil } from "lucide-react";
import { useBoulder } from "@/features/boulders/api";
import { useGym, useSectors } from "@/features/gyms/api";
import { floorPlanOf, SectorMap } from "@/features/gyms/SectorMap";
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
import type { BoulderGrade } from "@/features/boulders/api";

/**
 * One boulder. The photo comes first, because it is how a climber finds the problem on the wall, but it no longer
 * fills the screen: a tap opens it full size. The grade sits on the photo's corner; right under it, one quiet line
 * with holds, sector, date, setter and rating, and the follow/edit buttons. Then what the climber does with it
 * (progress), then what others say (beta, grade, comments).
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
        {b.photoUrl && <Grades grades={b.grades} className="boulder-page__grades--on-photo" />}
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
          {!b.photoUrl && <Grades grades={b.grades} />}
          <div className="boulder-page__info">
            <div className="boulder-meta__clip">
            <ul className="boulder-meta" aria-label={t("About this boulder")}>
              <li><HoldBadge color={b.holdColor} compact /></li>
              <li><span className="sr-only">{t("Sector")}: </span>{b.sectorName}</li>
              <li><span className="sr-only">{t("Set on")}: </span>{formatDate(b.createdAt)}</li>
              {b.setter && <li><span className="sr-only">{t("Setter")}: </span>{b.setter.displayName}</li>}
              {b.rating.count > 0 && <li><RatingSummaryText rating={b.rating} compact /></li>}
            </ul>
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
        </header>

        <ProgressTracker key={b.id} boulder={b} />
        <BoulderWhere gymSlug={b.gymSlug} gymId={b.gymId} sectorId={b.sectorId} sectorName={b.sectorName} />

        <BetaSection boulderId={b.id} gymName={b.gymName} />
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

/** The official grades: the first one large, the gym's other systems after it. */
function Grades({ grades, className = "" }: { grades: BoulderGrade[]; className?: string }) {
  const [primary, ...others] = grades;
  if (!primary) return null;
  return (
    <div className={`boulder-page__grades ${className}`}>
      <GradeBadge grade={primary} size="lg" />
      {others.map((g) => <GradeBadge key={g.gradeSystemId} grade={g} size="md" />)}
    </div>
  );
}

/** "Where is it": the gym's floor plan with this boulder's sector picked out, when the gym has drawn it. */
function BoulderWhere({ gymSlug, gymId, sectorId, sectorName }: { gymSlug: string; gymId: string; sectorId: string; sectorName: string }) {
  const gym = useGym(gymSlug);
  const sectors = useSectors(gymId);
  const plan = gym.data ? floorPlanOf(gym.data) : null;
  if (!plan || !sectors.data?.some((s) => s.id === sectorId && s.zone)) return null;
  // A tall plan would fill the screen: it keeps to about 240 px high, as wide as that allows.
  const maxWidth = `min(100%, ${Math.round(240 * plan.width / plan.height)}px)`;
  return (
    <section className="section card boulder-where" aria-labelledby="where-title">
      <div className="boulder-where__head">
        <h2 id="where-title" className="section__title">{t("Where it is")}</h2>
        <Link to={`/gyms/${gymSlug}?tab=sectors`} className="section__link">{t("Gym map")}</Link>
      </div>
      <p className="list__sub">{t("In {sector}", { sector: sectorName })}</p>
      <div style={{ maxWidth, width: "100%", margin: "0 auto" }}>
        <SectorMap plan={plan} sectors={sectors.data} highlightId={sectorId} />
      </div>
    </section>
  );
}
