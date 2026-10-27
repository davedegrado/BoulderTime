import { Link, useLocation } from "react-router-dom";
import { useState } from "react";
import { Check, ChevronRight, UserCheck, UserMinus, UserPlus, UserX, Users, X } from "lucide-react";
import type { Profile } from "@/features/climbing/api";
import { useFriendAction, useFriendRequests, useFriendsOf, type Friend, type FriendAction, type FriendPerson, type FriendSort } from "@/features/users/friendsApi";
import { GradeBadge } from "@/features/boulders/BoulderBits";
import { Avatar } from "@/components/Avatar";
import { Button } from "@/components/Button";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { useAuth } from "@/auth/AuthProvider";
import { errorMessage } from "@/lib/apiError";
import { timeAgo } from "@/lib/format";
import { plural, t } from "@/i18n/i18n";

/** Whether the server knows friendships: older ones send no `friendship` on a profile, and then the app offers none. */
export const supportsFriends = (p: Profile) => "friendship" in p;

/**
 * The friendship with the person whose profile this is (ADR-063), always as buttons of the same size in the same
 * place, so the page doesn't change shape from one state to the next: "Add friend"; once asked, "Cancel request";
 * their request, "Accept" and "Decline" under a line saying they asked; friends, "Friends", which opens the choice to
 * remove them. Nothing on your own profile, on someone you blocked, or against a server without friends. Signed out,
 * adding asks to sign in first. While a request travels the buttons wait, so nothing is sent twice.
 */
export function FriendButton({ profile: p }: { profile: Profile }) {
  const { session } = useAuth();
  const location = useLocation();
  const action = useFriendAction(p.id);
  const toast = useToast();
  const [confirmRemove, setConfirmRemove] = useState(false);
  if (p.isMe || p.isBlocked || !supportsFriends(p)) return null;
  if (!session) {
    return (
      <div className="friend-actions">
        <Link to={`/sign-in?next=${encodeURIComponent(location.pathname)}`} className="btn btn--primary">
          <UserPlus aria-hidden /><span>{t("Add friend")}</span>
        </Link>
      </div>
    );
  }
  const run = (a: FriendAction, done: string) =>
    action.mutate(a, { onSuccess: () => { setConfirmRemove(false); toast.success(done); }, onError: (e) => toast.error(errorMessage(e)) });
  const busy = action.isPending;
  const doing = (a: FriendAction) => busy && action.variables === a;

  switch (p.friendship) {
    case "OUTGOING":
      return (
        <div className="friend-actions">
          <Button variant="secondary" icon={<UserX aria-hidden />} loading={busy} onClick={() => run("remove", t("Request cancelled"))}>
            {t("Cancel request")}
          </Button>
        </div>
      );
    case "INCOMING":
      return (
        <div className="friend-actions">
          <p className="friend-actions__note">{t("{name} sent you a friend request", { name: p.displayName })}</p>
          <Button icon={<Check aria-hidden />} loading={doing("accept")} disabled={busy} onClick={() => run("accept", t("You're friends now"))}>
            {t("Accept")}
          </Button>
          <Button variant="secondary" icon={<X aria-hidden />} loading={doing("remove")} disabled={busy} onClick={() => run("remove", t("Request declined"))}>
            {t("Decline")}
          </Button>
        </div>
      );
    case "FRIENDS":
      return confirmRemove ? (
        <div className="friend-actions">
          <p className="friend-actions__note">{t("Remove {name} from your friends?", { name: p.displayName })}</p>
          <Button variant="danger" icon={<UserMinus aria-hidden />} loading={busy} onClick={() => run("remove", t("Removed from friends"))}>
            {t("Remove from friends")}
          </Button>
          <Button variant="secondary" disabled={busy} onClick={() => setConfirmRemove(false)}>{t("Cancel")}</Button>
        </div>
      ) : (
        <div className="friend-actions">
          <Button variant="secondary" icon={<UserCheck aria-hidden />} onClick={() => setConfirmRemove(true)}>
            {t("Friends")}
          </Button>
        </div>
      );
    default:
      return (
        <div className="friend-actions">
          <Button icon={<UserPlus aria-hidden />} loading={busy} onClick={() => run("request", t("Friend request sent"))}>
            {t("Add friend")}
          </Button>
        </div>
      );
  }
}

/** The "Friends" tab: on your own profile the requests you received first, then the friends. */
export function FriendsTab({ profile: p }: { profile: Profile }) {
  return (
    <div className="stack">
      {p.isMe && <IncomingRequests />}
      <FriendList userId={p.id} isMe={p.isMe} />
    </div>
  );
}

function IncomingRequests() {
  const requests = useFriendRequests();
  if (!requests.data?.length) return null;
  return (
    <section className="profile-block" aria-labelledby="friend-requests-title">
      <h2 id="friend-requests-title" className="profile-block__title">{t("Friend requests")}</h2>
      <ul className="people">{requests.data.map((r) => <RequestRow key={r.person.userId} person={r.person} mutualFriends={r.mutualFriends} />)}</ul>
    </section>
  );
}

/** A request received: who, and how many friends you have in common (when the server says), to accept or decline. */
export function RequestRow({ person, mutualFriends }: { person: FriendPerson; mutualFriends?: number | null }) {
  const action = useFriendAction(person.userId);
  const toast = useToast();
  const run = (a: FriendAction, done: string) => action.mutate(a, { onSuccess: () => toast.success(done), onError: (e) => toast.error(errorMessage(e)) });
  return (
    <li className="people__row">
      <PersonLink person={person}
        sub={mutualFriends ? plural(mutualFriends, "1 friend in common", "{count} friends in common") : undefined} />
      <span className="people__actions">
        <Button loading={action.isPending && action.variables === "accept"} disabled={action.isPending} onClick={() => run("accept", t("You're friends now"))}>{t("Accept")}</Button>
        <Button variant="ghost" aria-label={t("Decline {name}'s request", { name: person.displayName })} icon={<X aria-hidden />}
          loading={action.isPending && action.variables === "remove"} disabled={action.isPending} onClick={() => run("remove", t("Request declined"))} />
      </span>
    </li>
  );
}

const SORT_KEY = "bt:friends-sort";
const readSort = (): FriendSort => { try { return localStorage.getItem(SORT_KEY) === "name" ? "name" : "activity"; } catch { return "activity"; } };

/**
 * A person's friends, in a box: the most recently active first (or by name, the choice kept on this device), each with
 * their latest send — the boulder's grade, the gym, how long ago — and how many boulders they completed this month.
 */
export function FriendList({ userId, isMe }: { userId: string; isMe: boolean }) {
  const [sort, setSortState] = useState<FriendSort>(readSort);
  const setSort = (next: FriendSort) => { setSortState(next); try { localStorage.setItem(SORT_KEY, next); } catch { /* kept for this visit only */ } };
  const friends = useFriendsOf(userId, true, sort);
  const items = friends.data?.pages.flatMap((pg) => pg.items) ?? [];
  const total = friends.data?.pages[0]?.total;
  if (friends.isPending) return <LoadingState label={t("Loading friends")} />;
  if (friends.isError) return <ErrorState error={friends.error} onRetry={() => friends.refetch()} />;
  if (items.length === 0) {
    return <EmptyState icon={<Users />} title={isMe ? t("No friends yet") : t("No friends to show")}
      body={isMe ? t("Search for climbers by name in Friends, or open a profile and tap “Add friend”.") : undefined} />;
  }
  return (
    <section className="profile-block friends-box" aria-labelledby="friends-title">
      <div className="profile-block__head">
        <h2 id="friends-title" className="profile-block__title">{isMe ? t("Your friends") : t("Friends")}{total != null && <span className="friends-box__count"> · {total}</span>}</h2>
        <div className="chips chips--light friends-box__sort" role="radiogroup" aria-label={t("Sort friends")}>
          <button type="button" role="radio" aria-checked={sort === "activity"} className="chip" onClick={() => setSort("activity")}>{t("Activity")}</button>
          <button type="button" role="radio" aria-checked={sort === "name"} className="chip" onClick={() => setSort("name")}>{t("A–Z")}</button>
        </div>
      </div>
      <ul className="people">{items.map((f) => <FriendRow key={f.person.userId} friend={f} />)}</ul>
      {friends.hasNextPage && <Button variant="secondary" onClick={() => friends.fetchNextPage()} loading={friends.isFetchingNextPage}>{t("Show more")}</Button>}
    </section>
  );
}

/** One friend: photo and name, then what they climbed lately (when the server says, and the viewer may see it). */
function FriendRow({ friend: { person, activity } }: { friend: Friend }) {
  const a = activity;
  return (
    <li>
      <Link to={`/users/${person.userId}`} className="people__row friend-row">
        <Avatar name={person.displayName} url={person.avatarUrl} size={40} />
        <span className="friend-row__text">
          <span className="people__name">{person.displayName}</span>
          {a ? (
            <>
              <span className="friend-row__last">
                {a.grade && <GradeBadge grade={a.grade} size="sm" />}
                <span className="friend-row__where">{t("at {gym} · {when}", { gym: a.gymName, when: timeAgo(a.lastSendAt) })}</span>
              </span>
              <span className="friend-row__month">{plural(a.sentThisMonth, "1 boulder this month", "{count} boulders this month")}</span>
            </>
          ) : a === null ? <span className="friend-row__month">{t("No recent activity to show")}</span> : null}
        </span>
        <ChevronRight className="list__chevron" aria-hidden />
      </Link>
    </li>
  );
}

export function PersonLink({ person, sub }: { person: FriendPerson; sub?: string }) {
  return (
    <Link to={`/users/${person.userId}`} className="people__person">
      <Avatar name={person.displayName} url={person.avatarUrl} size={40} />
      <span className="friend-row__text">
        <span className="people__name">{person.displayName}</span>
        {sub && <span className="friend-row__month">{sub}</span>}
      </span>
    </Link>
  );
}
