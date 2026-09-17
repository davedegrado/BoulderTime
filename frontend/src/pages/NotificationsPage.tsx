import { Link, useNavigate } from "react-router-dom";
import { BellOff, CheckCheck, Settings } from "lucide-react";
import { useMarkRead, useNotifications, type AppNotification } from "@/features/notifications/api";
import { notificationIcon, relativeTime } from "@/features/notifications/NotificationBits";
import { Button } from "@/components/Button";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";

export function NotificationsPage() {
  const list = useNotifications();
  const markRead = useMarkRead();
  const navigate = useNavigate();
  const items = list.data?.pages.flatMap((p) => p.items) ?? [];
  const unread = items.filter((n) => !n.isRead).length;

  const today = new Date().toDateString();
  const groups: [string, AppNotification[]][] = [
    ["Today", items.filter((n) => new Date(n.createdAt).toDateString() === today)],
    ["Earlier", items.filter((n) => new Date(n.createdAt).toDateString() !== today)],
  ];

  function open(n: AppNotification) {
    if (!n.isRead) markRead.mutate(n.id);
    navigate(n.link);
  }

  return (
    <div className="page page--narrow">
      <header className="page__header section__row">
        <h1 className="page__title">Notifications</h1>
        <Link to="/notifications/settings" className="icon-link" aria-label="Notification settings"><Settings aria-hidden /></Link>
      </header>

      {unread > 0 && (
        <Button variant="secondary" icon={<CheckCheck aria-hidden />} onClick={() => markRead.mutate("all")} loading={markRead.isPending && markRead.variables === "all"}>
          Mark all as read
        </Button>
      )}

      {list.isPending ? <LoadingState label="Loading notifications" />
        : list.isError ? <ErrorState error={list.error} onRetry={() => list.refetch()} />
        : items.length === 0 ? (
          <EmptyState icon={<BellOff />} title="You're all caught up"
            body="Follow gyms, sectors and boulders to hear about new circuits, retraces, beta and events."
            action={<Link to="/explore" className="btn btn--primary"><span>Find a gym</span></Link>} />
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
                      {!n.isRead && <span className="notification__dot"><span className="sr-only">Unread</span></span>}
                    </button>
                  </li>
                );
              })}
            </ul>
          </section>
        ))}
      {list.hasNextPage && <Button variant="secondary" onClick={() => list.fetchNextPage()} loading={list.isFetchingNextPage}>Older notifications</Button>}
    </div>
  );
}
