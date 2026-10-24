import { useRef, useState, type ReactNode } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { Ban, Copy, Flag, LayoutList, Lock, MapPin, Pencil, Share2, ShieldCheck, Undo2, UserMinus } from "lucide-react";
import { DEFAULT_PROFILE_SECTIONS, useProfile, type Profile, type ProfileSection } from "@/features/climbing/api";
import { CompactHistoryRow, HighestGrades, WeeklyChart } from "@/features/climbing/ClimbingBits";
import { FavoriteSectorCard } from "@/features/climbing/FavoriteSector";
import { HistorySection } from "@/pages/ActivityPage";
import { CustomizeProfile, sectionLabel } from "@/features/profile/CustomizeProfile";
import { PrivacySettings } from "@/features/profile/PrivacySettings";
import { EnlargeableAvatar } from "@/components/EnlargeableAvatar";
import { GymAvatar } from "@/components/GymAvatar";
import { ClimbedGymList } from "@/features/profile/ClimbedGyms";
import type { GymSummary } from "@/features/gyms/api";
import { DetailHeader } from "@/components/DetailHeader";
import { ActionMenu, type ActionMenuItem } from "@/components/ActionMenu";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { formatDate } from "@/lib/format";
import { copyText, shareLink, shareUrl } from "@/lib/share";
import { plural, t } from "@/i18n/i18n";
import { StaffDistinctions } from "@/features/gyms/StaffDistinctions";
import { useAuth } from "@/auth/AuthProvider";
import { BlockAction } from "@/features/users/BlockAction";
import { FriendButton, FriendsTab, supportsFriends } from "@/features/users/Friends";
import { useFriendAction } from "@/features/users/friendsApi";
import { errorMessage } from "@/lib/apiError";
import { ReportPersonAction } from "@/features/users/ReportPersonAction";

type Panel = "customize" | "privacy" | "block" | "report" | null;
type Tab = "overview" | "activity" | "gyms" | "friends";

/**
 * A person's profile (ADR-061, ADR-062): a header transparent over a plain cover until the page scrolls, who they are,
 * then tabs — the overview with the sections they chose, in their order, the whole activity, the gyms. A private
 * profile shows its content only to its owner; others see the name and photo and are told it's private (the server
 * sends nothing more). Back returns where it was opened from, Home when opened directly.
 */
export function UserProfilePage() {
  const { id } = useParams();
  const profile = useProfile(id);
  const [panel, setPanel] = useState<Panel>(null);
  const cover = useRef<HTMLDivElement>(null);
  const name = useRef<HTMLHeadingElement>(null);
  const p = profile.data;

  // Never stuck: the way back is there while the profile loads, and if it can't be loaded.
  if (profile.isPending) return <div className="page page--flush person-page"><DetailHeader /><LoadingState label={t("Loading activity")} /></div>;
  if (!p) return <div className="page page--flush person-page"><DetailHeader /><div className="page__pad"><ErrorState error={profile.error} onRetry={() => profile.refetch()} /></div></div>;

  // Older servers send no arrangement: the default one, and nothing to customise.
  const sections = p.sections ?? DEFAULT_PROFILE_SECTIONS;
  const canArrange = p.isMe && Boolean(p.sections);

  return (
    <div className="page page--flush person-page">
      <DetailHeader menu={<ProfileMenu profile={p} canArrange={canArrange} onOpen={setPanel} />} title={p.displayName} coverRef={cover} titleRef={name} />
      <header className="person-hero">
        <div ref={cover} className="person-hero__cover"><span className="detail-cover__scrim" aria-hidden /></div>
        <div className="person-hero__body">
          <EnlargeableAvatar name={p.displayName} url={p.avatarUrl} size={84} />
          <h1 ref={name} className="page__title person-hero__name">{p.displayName}</h1>
          {p.friendCount != null && <FriendCount count={p.friendCount} />}
          {p.bio && <p className="person-hero__bio">{p.bio}</p>}
          <p className="person-hero__meta">{t("On BoulderTime since {date}", { date: formatDate(p.memberSince, { month: "long", year: "numeric" }) })}</p>
          <FriendButton profile={p} />
          {!p.contentHidden && <StaffDistinctions distinctions={p.staffDistinctions} />}
          {p.isMe && p.isPrivate && <p className="person-hero__private"><Lock aria-hidden /> {t("Private profile: others see only your name and photo.")}</p>}
          {p.isBlocked && (
            <p className="notice notice--inline">
              <Ban aria-hidden />
              {t("You've blocked {name}. Their comments and videos are hidden from you, and yours from them. They aren't told.", { name: p.displayName })}
            </p>
          )}
        </div>
      </header>

      {panel && <div className="page__pad stack">
        {panel === "customize" && <CustomizeProfile sections={sections} onClose={() => setPanel(null)} />}
        {panel === "privacy" && (
          <section className="card stack profile-panel" aria-labelledby="privacy-title">
            <h2 id="privacy-title" className="section__title">{t("Privacy and leaderboards")}</h2>
            <PrivacySettings />
            <Button variant="ghost" onClick={() => setPanel(null)}>{t("Close")}</Button>
          </section>
        )}
        {panel === "block" && <div className="card"><BlockAction userId={p.id} displayName={p.displayName} isBlocked={p.isBlocked} startConfirming onClose={() => setPanel(null)} /></div>}
        {panel === "report" && <div className="card"><ReportPersonAction userId={p.id} displayName={p.displayName} startOpen onClose={() => setPanel(null)} /></div>}
      </div>}

      {p.contentHidden
        ? (
          <div className="page__pad">
            <EmptyState icon={<Lock />} title={t("This profile is private")} body={t("{name} keeps their statistics, history and gyms to themselves.", { name: p.displayName })} />
          </div>
        )
        : <ProfileContent profile={p} sections={sections} onCustomize={canArrange ? () => setPanel("customize") : undefined} />}
    </div>
  );
}

/** How many friends, under the name; a tap opens the Friends tab. */
function FriendCount({ count }: { count: number }) {
  const [, setParams] = useSearchParams();
  return (
    <button type="button" className="person-hero__friends"
      onClick={() => setParams((q) => { const n = new URLSearchParams(q); n.set("tab", "friends"); return n; }, { replace: true })}>
      {plural(count, "{count} friend", "{count} friends")}
    </button>
  );
}

function ProfileMenu({ profile: p, canArrange, onOpen }: { profile: Profile; canArrange: boolean; onOpen: (panel: Panel) => void }) {
  const { session } = useAuth();
  const toast = useToast();
  const friendship = useFriendAction(p.id);
  const url = shareUrl(`/users/${p.id}`);
  const said = (r: "shared" | "copied" | "failed") =>
    r === "copied" ? toast.success(t("Link copied")) : r === "failed" ? toast.error(t("Couldn't copy the link")) : undefined;

  const items: ActionMenuItem[] = p.isMe
    ? [
        { kind: "link", label: t("Edit profile"), icon: <Pencil aria-hidden />, to: "/profile" },
        ...(canArrange ? [
          { label: t("Customise profile"), icon: <LayoutList aria-hidden />, onSelect: () => onOpen("customize") },
          { label: t("Privacy and leaderboards"), icon: <ShieldCheck aria-hidden />, onSelect: () => onOpen("privacy") },
        ] : []),
        { label: t("Share profile"), icon: <Share2 aria-hidden />, onSelect: () => void shareLink(p.displayName, url).then(said) },
      ]
    : [
        { label: t("Share profile"), icon: <Share2 aria-hidden />, onSelect: () => void shareLink(p.displayName, url).then(said) },
        { label: t("Copy link"), icon: <Copy aria-hidden />, onSelect: () => void copyText(url).then((ok) => said(ok ? "copied" : "failed")) },
        ...(session ? [
          p.isBlocked
            ? { label: t("Unblock"), icon: <Undo2 aria-hidden />, onSelect: () => onOpen("block") }
            : { label: t("Block"), icon: <Ban aria-hidden />, onSelect: () => onOpen("block"), danger: true },
          ...(p.friendship === "FRIENDS" ? [{
            label: t("Remove from friends"), icon: <UserMinus aria-hidden />, danger: true,
            onSelect: () => friendship.mutate("remove", { onSuccess: () => toast.success(t("Removed from friends")), onError: (e) => toast.error(errorMessage(e)) }),
          }] : []),
          { label: t("Report {name}", { name: p.displayName }), icon: <Flag aria-hidden />, onSelect: () => onOpen("report"), danger: true },
        ] : []),
      ];
  return <ActionMenu items={items} />;
}

/**
 * The overview shows the sections the person chose, in their order, each as large as what it holds: a heading and its
 * content, no box around each. The statistics appear only if the person turned them on. The whole history and the
 * gyms have tabs of their own.
 */
function ProfileContent({ profile: p, sections, onCustomize }: { profile: Profile; sections: ProfileSection[]; onCustomize?: () => void }) {
  const [params, setParams] = useSearchParams();
  const shown = (key: ProfileSection["key"]) => sections.some((s) => s.key === key && s.visible);
  const tabs: Tab[] = ["overview", ...(shown("history") ? ["activity" as const] : []), ...(shown("gyms") ? ["gyms" as const] : []),
    ...(supportsFriends(p) ? ["friends" as const] : [])];
  const asked = params.get("tab") as Tab | null;
  const tab: Tab = asked && tabs.includes(asked) ? asked : "overview";
  const setTab = (next: Tab) => setParams((q) => { const n = new URLSearchParams(q); if (next === "overview") n.delete("tab"); else n.set("tab", next); return n; }, { replace: true });
  const label: Record<Tab, string> = { overview: t("Overview"), activity: t("Activity"), gyms: t("Gyms"), friends: t("Friends") };

  // Where they climbed (ADR-064); the gyms followed only against an older server that doesn't say.
  const gyms = p.climbedGyms ? p.climbedGyms.map((c) => c.gym) : p.followedGyms;

  // Only sections with something to show: nothing invented, no empty boxes.
  const blocks = sections.filter((s) => s.visible).map((s) => {
    switch (s.key) {
      case "stats": return <Block key={s.key} id="stats" title={t("Statistics")}><StatsRow stats={p.stats} /></Block>;
      case "favoriteSector": return p.favoriteSector ? <FavoriteSectorCard key={s.key} sector={p.favoriteSector} isMe={p.isMe} /> : null;
      case "weekly": return p.weekly.some((w) => w.completed > 0)
        ? <Block key={s.key} id="weekly" title={t("Last 12 weeks")}><WeeklyChart weeks={p.weekly} /></Block>
        : null;
      case "highestGrades": return p.highestGrades.length > 0
        ? <Block key={s.key} id="highest" title={t("Highest grades")}><HighestGrades grades={p.highestGrades} /></Block>
        : null;
      case "gyms": return gyms.length > 0
        ? <Block key={s.key} id="gyms" title={sectionLabel("gyms")}><GymChips gyms={gyms} /></Block>
        : null;
      case "history": return p.recentCompletions.length > 0
        ? (
          <Block key={s.key} id="recent" title={t("Recent activity")}
            action={<button type="button" className="text-btn" onClick={() => setTab("activity")}>{t("See all")}</button>}>
            <ul className="hlist">{p.recentCompletions.slice(0, 3).map((i) => <CompactHistoryRow key={i.boulder.id} item={i} />)}</ul>
          </Block>
        )
        : null;
      default: return null;
    }
  }).filter(Boolean);

  return (
    <>
      {tabs.length > 1 && (
        <div className="tabs" role="tablist" aria-label={t("Profile sections")}>
          {tabs.map((x) => <button key={x} role="tab" aria-selected={tab === x} className="tabs__tab" onClick={() => setTab(x)}>{label[x]}</button>)}
        </div>
      )}
      <div className="page__pad profile-tab">
        {tab === "activity" ? <HistorySection userId={p.id} isMe={p.isMe} titled={false} compact />
          : tab === "friends" ? <FriendsTab profile={p} />
          : tab === "gyms" ? <GymsTab profile={p} />
          : blocks.length > 0 ? <div className="profile-overview">{blocks}</div>
          : <EmptyState icon={<LayoutList />} title={p.isMe ? t("Nothing to show yet") : t("No activity yet")}
              action={onCustomize ? <Button variant="secondary" onClick={onCustomize}>{t("Customise profile")}</Button> : undefined} />}
      </div>
    </>
  );
}

function Block({ id, title, action, children }: { id: string; title: string; action?: ReactNode; children: ReactNode }) {
  return (
    <section className="profile-block" aria-labelledby={`${id}-title`}>
      <div className="profile-block__head">
        <h2 id={`${id}-title`} className="profile-block__title">{title}</h2>
        {action}
      </div>
      {children}
    </section>
  );
}

/** The four numbers in one row, for those who choose to show them. */
function StatsRow({ stats }: { stats: Profile["stats"] }) {
  const items: [number, string][] = [
    [stats.completed, t("Sends")], [stats.projects, t("Projects")], [stats.completedThisMonth, t("Sent this month")], [stats.totalAttempts, t("Attempts")],
  ];
  return (
    <dl className="profile-stats">
      {items.map(([value, name]) => <div key={name} className="profile-stats__item"><dt>{name}</dt><dd>{value}</dd></div>)}
    </dl>
  );
}

/**
 * The "Gyms" tab: where the person completed at least one boulder (ADR-064) — following a gym is not enough, and it is
 * never shown in its place. Against an older server, which doesn't say where they climbed, the gyms followed, as before.
 */
function GymsTab({ profile: p }: { profile: Profile }) {
  if (!p.climbedGyms) {
    return p.followedGyms.length > 0
      ? <GymChips gyms={p.followedGyms} />
      : <EmptyState icon={<MapPin />} title={p.isMe ? t("You don't follow any gym yet") : t("No gyms followed yet")} />;
  }
  if (p.climbedGyms.length === 0) {
    return <EmptyState icon={<MapPin />} title={p.isMe ? t("No gyms climbed yet") : t("No gyms to show yet")}
      body={p.isMe ? t("The gyms where you complete at least one boulder appear here.") : t("The gyms where {name} completes at least one boulder appear here.", { name: p.displayName })} />;
  }
  return <ClimbedGymList gyms={p.climbedGyms} />;
}

function GymChips({ gyms }: { gyms: GymSummary[] }) {
  return (
    <div className="gym-chips" aria-label={t("Gyms")}>
      {gyms.map((g) => (
        <Link key={g.id} to={`/gyms/${g.slug}`} className="gym-chip"><GymAvatar name={g.name} logoUrl={g.logoUrl} size={24} /><span>{g.name}</span></Link>
      ))}
    </div>
  );
}
