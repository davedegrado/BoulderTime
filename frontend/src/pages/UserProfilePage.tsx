import { useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { Ban, Copy, Flag, LayoutList, Lock, Pencil, Share2, ShieldCheck, Undo2 } from "lucide-react";
import { DEFAULT_PROFILE_SECTIONS, useProfile, type Profile, type ProfileSection } from "@/features/climbing/api";
import { HighestGrades, HistoryRow, WeeklyChart } from "@/features/climbing/ClimbingBits";
import { FavoriteSectorCard } from "@/features/climbing/FavoriteSector";
import { HistorySection, StatsGrid } from "@/pages/ActivityPage";
import { CustomizeProfile, sectionLabel } from "@/features/profile/CustomizeProfile";
import { PrivacySettings } from "@/features/profile/PrivacySettings";
import { EnlargeableAvatar } from "@/components/EnlargeableAvatar";
import { GymAvatar } from "@/components/GymAvatar";
import { DetailHeader } from "@/components/DetailHeader";
import { ActionMenu, type ActionMenuItem } from "@/components/ActionMenu";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { formatDate } from "@/lib/format";
import { copyText, shareLink, shareUrl } from "@/lib/share";
import { t } from "@/i18n/i18n";
import { StaffDistinctions } from "@/features/gyms/StaffDistinctions";
import { useAuth } from "@/auth/AuthProvider";
import { BlockAction } from "@/features/users/BlockAction";
import { ReportPersonAction } from "@/features/users/ReportPersonAction";

type Panel = "customize" | "privacy" | "block" | "report" | null;
type Tab = "overview" | "activity" | "gyms";

/**
 * A person's profile (ADR-061): compact header over a plain cover, who they are, then their sections in the order they
 * chose. A private profile shows its content only to its owner; others see the name and photo and are told it's
 * private (the server sends nothing more). Back returns where it was opened from, Home when opened directly.
 */
export function UserProfilePage() {
  const { id } = useParams();
  const profile = useProfile(id);
  const [panel, setPanel] = useState<Panel>(null);
  const p = profile.data;

  // Never stuck: the way back is there while the profile loads, and if it can't be loaded.
  if (profile.isPending) return <div className="page page--flush person-page"><DetailHeader /><LoadingState label={t("Loading activity")} /></div>;
  if (!p) return <div className="page page--flush person-page"><DetailHeader /><div className="page__pad"><ErrorState error={profile.error} onRetry={() => profile.refetch()} /></div></div>;

  // Older servers send no arrangement: the default one, and nothing to customise.
  const sections = p.sections ?? DEFAULT_PROFILE_SECTIONS;
  const canArrange = p.isMe && Boolean(p.sections);

  return (
    <div className="page page--flush person-page">
      <DetailHeader menu={<ProfileMenu profile={p} canArrange={canArrange} onOpen={setPanel} />} />
      <header className="person-hero">
        <div className="person-hero__cover" />
        <div className="person-hero__body">
          <EnlargeableAvatar name={p.displayName} url={p.avatarUrl} size={84} />
          <h1 className="page__title person-hero__name">{p.displayName}</h1>
          <p className="person-hero__meta">{t("On BoulderTime since {date}", { date: formatDate(p.memberSince, { month: "long", year: "numeric" }) })}</p>
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

function ProfileMenu({ profile: p, canArrange, onOpen }: { profile: Profile; canArrange: boolean; onOpen: (panel: Panel) => void }) {
  const { session } = useAuth();
  const toast = useToast();
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
          { label: t("Report {name}", { name: p.displayName }), icon: <Flag aria-hidden />, onSelect: () => onOpen("report"), danger: true },
        ] : []),
      ];
  return <ActionMenu items={items} />;
}

/** The sections the person chose, in their order, with the history and the gyms as tabs of their own. */
function ProfileContent({ profile: p, sections, onCustomize }: { profile: Profile; sections: ProfileSection[]; onCustomize?: () => void }) {
  const [params, setParams] = useSearchParams();
  const shown = (key: ProfileSection["key"]) => sections.some((s) => s.key === key && s.visible);
  const tabs: Tab[] = ["overview", ...(shown("history") ? ["activity" as const] : []), ...(shown("gyms") && p.followedGyms.length > 0 ? ["gyms" as const] : [])];
  const asked = params.get("tab") as Tab | null;
  const tab: Tab = asked && tabs.includes(asked) ? asked : "overview";
  const setTab = (next: Tab) => setParams((q) => { const n = new URLSearchParams(q); if (next === "overview") n.delete("tab"); else n.set("tab", next); return n; }, { replace: true });
  const label: Record<Tab, string> = { overview: t("Overview"), activity: t("Activity"), gyms: t("Gyms") };

  // Only sections with something to show: nothing invented, no empty boxes.
  const blocks = sections.filter((s) => s.visible && s.key !== "stats").map((s) => {
    switch (s.key) {
      case "favoriteSector": return p.favoriteSector ? <FavoriteSectorCard key={s.key} sector={p.favoriteSector} isMe={p.isMe} /> : null;
      case "weekly": return p.weekly.some((w) => w.completed > 0)
        ? <section key={s.key} className="section card" aria-labelledby="weekly-title"><h2 id="weekly-title" className="section__title">{t("Last 12 weeks")}</h2><WeeklyChart weeks={p.weekly} /></section>
        : null;
      case "highestGrades": return p.highestGrades.length > 0
        ? <section key={s.key} className="section card" aria-labelledby="highest-title"><h2 id="highest-title" className="section__title">{t("Highest grades")}</h2><HighestGrades grades={p.highestGrades} /></section>
        : null;
      case "gyms": return p.followedGyms.length > 0
        ? <section key={s.key} className="section card" aria-labelledby="gyms-title"><h2 id="gyms-title" className="section__title">{sectionLabel("gyms")}</h2><GymChips profile={p} /></section>
        : null;
      case "history": return p.recentCompletions.length > 0
        ? (
          <section key={s.key} className="section card" aria-labelledby="recent-title">
            <div className="section__head">
              <h2 id="recent-title" className="section__title">{t("Recent activity")}</h2>
              <button type="button" className="text-btn" onClick={() => setTab("activity")}>{t("See all")}</button>
            </div>
            <ul className="history">{p.recentCompletions.slice(0, 3).map((i) => <HistoryRow key={i.boulder.id} item={i} />)}</ul>
          </section>
        )
        : null;
      default: return null;
    }
  }).filter(Boolean);

  return (
    <>
      {shown("stats") && <div className="page__pad"><StatsGrid stats={p.stats} /></div>}
      {tabs.length > 1 && (
        <div className="tabs" role="tablist" aria-label={t("Profile sections")}>
          {tabs.map((x) => <button key={x} role="tab" aria-selected={tab === x} className="tabs__tab" onClick={() => setTab(x)}>{label[x]}</button>)}
        </div>
      )}
      <div className="page__pad stack">
        {tab === "activity" ? <HistorySection userId={p.id} isMe={p.isMe} titled={false} />
          : tab === "gyms" ? <GymChips profile={p} />
          : blocks.length > 0 ? blocks
          : <EmptyState icon={<LayoutList />} title={p.isMe ? t("Nothing to show yet") : t("No activity yet")}
              action={onCustomize ? <Button variant="secondary" onClick={onCustomize}>{t("Customise profile")}</Button> : undefined} />}
      </div>
    </>
  );
}

function GymChips({ profile: p }: { profile: Profile }) {
  return (
    <div className="gym-chips" aria-label={t("Gyms")}>
      {p.followedGyms.map((g) => (
        <Link key={g.id} to={`/gyms/${g.slug}`} className="gym-chip"><GymAvatar name={g.name} logoUrl={g.logoUrl} size={24} /><span>{g.name}</span></Link>
      ))}
    </div>
  );
}
