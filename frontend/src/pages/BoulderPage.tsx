import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Bell, BellRing, CalendarDays, Drill, History, MapPin, Pencil, Star } from "lucide-react";
import { DetailHeader } from "@/components/DetailHeader";
import { useBoulder } from "@/features/boulders/api";
import { useGym, useSectors } from "@/features/gyms/api";
import { floorPlanOf, SectorMap } from "@/features/gyms/SectorMap";
import { ProgressTracker } from "@/features/climbing/ProgressTracker";
import { useFollowBoulder } from "@/features/climbing/api";
import { useAuth } from "@/auth/AuthProvider";
import { CommentsSection } from "@/features/community/CommentsSection";
import { CommunityGradeSection } from "@/features/community/CommunityGradeSection";
import { BetaSection, CommunityVideosSection } from "@/features/community/VideoSections";
import { ReportButton } from "@/features/community/ReportButton";
import { GradeBadge, HoldBadge } from "@/features/boulders/BoulderBits";
import { IntensityBadge, type Intensity } from "@/features/boulders/intensity";
import { CompletionsLine } from "@/features/climbing/Completions";
import { PhotoViewer } from "@/features/boulders/PhotoViewer";
import { ErrorState, LoadingState } from "@/components/States";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { ApiError } from "@/lib/apiError";
import { formatDate } from "@/lib/format";
import { activeLanguage, plural, t } from "@/i18n/i18n";
import { useHideStaffTools } from "@/lib/climberView";
import type { BoulderGrade, RatingSummary } from "@/features/boulders/api";

/**
 * One boulder. A plain header (back, follow, edit) and then the photo, the largest thing on the page, because it is
 * how a climber finds the problem on the wall: the hold colour in its top-left corner, full screen top-right, the
 * official grade bottom-left, the points bottom-right — hold colour and grade are two different things and never sit
 * together. Under it what the boulder is (name, sector, date, sends, community rating), its tags, what the climber does
 * with it (progress), then what others say. Whatever a boulder doesn't have takes no room: no name, no title row.
 */
export function BoulderPage() {
  const { id } = useParams();
  const boulder = useBoulder(id);
  const { session } = useAuth();
  const follow = useFollowBoulder(id ?? "");
  const [viewing, setViewing] = useState(false);
  const hideStaff = useHideStaffTools();

  if (boulder.isPending) return <div className="page page--flush boulder-page"><DetailHeader /><LoadingState label={t("Loading boulder")} /></div>;
  if (boulder.isError) {
    return boulder.error instanceof ApiError && boulder.error.isNotFound
      ? <NotFoundPage />
      : <div className="page page--flush boulder-page"><DetailHeader /><div className="page__pad"><ErrorState error={boulder.error} onRetry={() => boulder.refetch()} /></div></div>;
  }

  const b = boulder.data;
  const removed = b.status === "REMOVED";
  const name = b.name?.trim() || null;
  const photoAlt = name ? t("{name}, boulder in {sector}", { name, sector: b.sectorName }) : t("Boulder in {sector}", { sector: b.sectorName });
  const pointsLabel = b.points != null ? plural(b.points, "Worth {count} point", "Worth {count} points") : null;
  const completions = b.completions ?? 0;

  const actions = (
    <div className="boulder-page__actions">
      {session && (
        <button type="button" className={`round-btn ${b.isFollowing ? "is-following" : ""}`}
          aria-pressed={b.isFollowing} onClick={() => follow.mutate(!b.isFollowing)}
          aria-label={b.isFollowing ? t("Stop following this boulder") : t("Follow this boulder")}
          title={b.isFollowing ? t("Stop following this boulder") : t("Follow this boulder")}>
          {b.isFollowing ? <BellRing aria-hidden /> : <Bell aria-hidden />}
        </button>
      )}
      {b.viewerRole && !hideStaff && (
        <Link to={`/manage/${b.gymSlug}/boulders/${b.id}/edit`} className="round-btn" aria-label={t("Edit")} title={t("Edit")}>
          <Pencil aria-hidden />
        </Link>
      )}
    </div>
  );

  return (
    <article className="page page--flush boulder-page">
      {/* Back returns where the boulder was opened from (a profile, a filtered list, the notifications); the gym only
          when the app was opened right here. */}
      {/* The name, when the boulder has one, is the header's title: always there, cut with an info button if too long. */}
      <DetailHeader menu={actions} title={name ?? undefined} backTo={`/gyms/${b.gymSlug}`} wide titleAlways />

      {/* A frame of fixed height, the photo whole inside it (contain): bands of the neutral colour above and below a
          landscape photo, at the sides of a tall one. The corners are the frame's, so they never move with the photo.
          Tapping the photo opens it full screen. */}
      {b.photoUrl && (
        <div className="boulder-page__photo">
          <button type="button" className="boulder-page__photo-btn" onClick={() => setViewing(true)} aria-label={t("Enlarge the photo")}>
            <img src={b.photoUrl} alt={photoAlt} />
          </button>
          <span className="boulder-page__holds"><HoldBadge color={b.holdColor} compact /></span>
          <CommunityRating rating={b.rating} className="boulder-page__rating" />
          <Grades grades={b.grades} intensity={b.intensity} className="boulder-page__grades--on-photo" />
          {b.points != null && (
            <span className="boulder-page__points" title={pointsLabel ?? undefined}>
              <span aria-hidden>{t("+{points} pt", { points: b.points })}</span><span className="sr-only">{pointsLabel}</span>
            </span>
          )}
        </div>
      )}
      {viewing && b.photoUrl && <PhotoViewer src={b.photoUrl} alt={photoAlt} onClose={() => setViewing(false)} />}

      <div className="page__pad stack">
        {removed && (
          <div className="notice" role="note">
            <History aria-hidden />
            <p>{b.removedAt ? t("This boulder was removed on {date}.", { date: formatDate(b.removedAt) }) : t("This boulder was removed.")} {t("It stays here so climbers keep their history.")}</p>
          </div>
        )}

        <header className="boulder-page__head">
          <div className="boulder-page__intro">
            {/* The name is in the header: the heading is for screen readers only, so it takes no room. */}
            <h1 className="sr-only">{name ?? photoAlt}</h1>
            {!b.photoUrl && <Grades grades={b.grades} intensity={b.intensity} />}
            <ul className="boulder-facts" aria-label={t("About this boulder")}>
              {!b.photoUrl && <li><HoldBadge color={b.holdColor} compact /></li>}
              {!b.photoUrl && b.points != null && <li>{pointsLabel}</li>}
              {!b.photoUrl && <li><CommunityRating rating={b.rating} /></li>}
              <li><MapPin aria-hidden /><span className="sr-only">{t("Sector")}: </span>{b.sectorName}</li>
              <li><CalendarDays aria-hidden /><span className="sr-only">{t("Set on")}: </span>{formatDate(b.createdAt)}</li>
              {b.setter && <li><Drill aria-hidden /><span className="sr-only">{t("Setter")}: </span>{b.setter.displayName}</li>}
            </ul>
          </div>
        </header>

        {(b.tags ?? []).length > 0 && (
          <ul className="tag-list" aria-label={t("Tags")}>
            {(b.tags ?? []).map((tag) => <li key={tag.id} className="tag-pill">{tag.name}</li>)}
          </ul>
        )}

        <ProgressTracker key={b.id} boulder={b} />
        <CompletionsLine boulderId={b.id} count={completions} />
        <BoulderWhere gymSlug={b.gymSlug} gymId={b.gymId} sectorId={b.sectorId} />

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

/**
 * The community's rating as one compact line: star, average, and how many rated it in brackets ("★ 4,0 (12)"). Nothing
 * when nobody rated it; the number only when there is one. Information, not a button. The climber's own rating is in
 * Your progress.
 */
function CommunityRating({ rating, className = "" }: { rating: RatingSummary; className?: string }) {
  if (rating.average === null || rating.average === undefined) return null;
  const average = rating.average.toLocaleString(activeLanguage(), { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  const count = typeof rating.count === "number" && rating.count > 0 ? rating.count : null;
  const label = count !== null
    ? t("Rated {average} out of 5 by {count} climbers", { average, count })
    : t("Rated {average} out of 5", { average });
  return (
    <span className={`community-rating ${className}`} role="img" aria-label={label}>
      <Star aria-hidden className="community-rating__star" />
      <span aria-hidden>{average}</span>
      {count !== null && <span className="community-rating__count" aria-hidden>({count})</span>}
    </span>
  );
}

/** The official grades: the first one large, the gym's other systems after it, then the intensity, as on the cards. */
function Grades({ grades, intensity, className = "" }: { grades: BoulderGrade[]; intensity?: Intensity | null; className?: string }) {
  const [primary, ...others] = grades;
  if (!primary && !intensity) return null;
  return (
    <div className={`boulder-page__grades ${className}`}>
      {primary && <GradeBadge grade={primary} size="lg" />}
      {others.map((g) => <GradeBadge key={g.gradeSystemId} grade={g} size="md" />)}
      {intensity && <span className="boulder-page__intensity"><IntensityBadge intensity={intensity} compact /></span>}
    </div>
  );
}

/** "Where is it": the gym's floor plan with this boulder's sector picked out, when the gym has drawn it. */
function BoulderWhere({ gymSlug, gymId, sectorId }: { gymSlug: string; gymId: string; sectorId: string }) {
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
      {/* No sector name here, neither above the plan nor on it: the facts at the top of the page already name it, and
          the highlighted area shows where it is. */}
      <div style={{ maxWidth, width: "100%", margin: "0 auto" }}>
        <SectorMap plan={plan} sectors={sectors.data} highlightId={sectorId} labels={false} />
      </div>
    </section>
  );
}
