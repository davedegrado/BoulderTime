import { Link, useNavigate } from "react-router-dom";
import { BellOff, CheckCheck, ChevronRight, Settings, UserPlus } from "lucide-react";
import { useMarkRead, useNotifications, type AppNotification } from "@/features/notifications/api";
import { notificationIcon, relativeTime } from "@/features/notifications/NotificationBits";
import { Button } from "@/components/Button";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useFriendRequests } from "@/features/users/friends";
import { useCurrentUser } from "@/features/users/api";
import { plural, t } from "@/i18n/i18n";

export function NotificationsPage() {
  const list = useNotifications();
  const markRead = useMarkRead();
  const navigate = useNavigate();
  const items = list.data?.pages.flatMap((p) => p.items) ?? [];
  const unread = items.filter((n) => !n.isRead).length;

  const today = new Date().toDateString();
  const groups: [string, AppNotification[]][] = [
    [t("Today"), items.filter((n) => new Date(n.createdAt).toDateString() === today)],
    [t("Earlier"), items.filter((n) => new Date(n.createdAt).toDateString() !== today)],
  ];

  function open(n: AppNotification) {
    if (!n.isRead) markRead.mutate(n.id);
    navigate(n.link);
  }

  return (
    <div className="page page--narrow">
      <header className="page__header page__header--row">
        <h1 className="page__title">{t("Notifications")}</h1>
        <Link to="/notifications/settings" className="icon-link" aria-label={t("Notification settings")}><Settings aria-hidden /></Link>
      </header>

      <FriendRequestsLink />

      {unread > 0 && (
        <Button variant="ghost" className="mark-all" icon={<CheckCheck aria-hidden />} onClick={() => markRead.mutate("all")} loading={markRead.isPending && markRead.variables === "all"}>
          {t("Mark all as read")}
        </Button>
      )}

      {list.isPending ? <LoadingState label={t("Loading notifications")} />
        : list.isError ? <ErrorState error={list.error} onRetry={() => list.refetch()} />
        : items.length === 0 ? (
          <EmptyState icon={<BellOff />} title={t("You're all caught up")}
            body={t("Follow gyms, sectors and boulders to hear about new circuits, retraces, beta and events.")}
            action={<Link to="/explore" className="btn btn--primary"><span>{t("Find a gym")}</span></Link>} />
        ) : groups.filter(([, g]) => g.length > 0).map(([label, group]) => (
          <section key={label} className="section" aria-label={label}>
            <h2 className="section__meta">{label}</h2>
            <ul className="list">
              {group.map((n) => {
                const Icon = notificationIcon[n.type];
                return (
                  <li key={n.id}>
                    <button type="button" className={`notification ${n.isRead ? "" : "is-unread"}`} onClick={() => open(n)}>
                      <span className={`notification__icon notification__icon--${n.category.toLowerCase()}`} aria-hidden><Icon /></span>
                      <span className="notification__text">
                        <span className="notification__title">{n.title}</span>
                        {n.body && <span className="list__sub">{n.body}</span>}
                        <span className="notification__time">{relativeTime(n.createdAt)}</span>
                      </span>
                      {!n.isRead && <span className="notification__dot"><span className="sr-only">{t("Unread")}</span></span>}
                    </button>
                  </li>
                );
              })}
            </ul>
          </section>
        ))}
      {list.hasNextPage && <Button variant="secondary" onClick={() => list.fetchNextPage()} loading={list.isFetchingNextPage}>{t("Older notifications")}</Button>}
    </div>
  );
}

/**
 * Friend requests are not notifications of the feed (apps already installed would not know how to show them, ADR-063):
 * a line of their own, there only when someone asked, leading to the requests on your profile.
 */
function FriendRequestsLink() {
  const requests = useFriendRequests();
  const me = useCurrentUser();
  const count = requests.data?.length ?? 0;
  if (count === 0 || !me.data) return null;
  return (
    <Link to={`/users/${me.data.id}?tab=friends`} className="card friend-requests-link">
      <span className="friend-requests-link__icon" aria-hidden><UserPlus /></span>
      <span className="friend-requests-link__text">{plural(count, "1 friend request", "{count} friend requests")}</span>
      <ChevronRight className="list__chevron" aria-hidden />
    </Link>
  );
}
