import { Link, useLocation } from "react-router-dom";
import { Check, Clock, UserCheck, UserPlus, Users, X } from "lucide-react";
import type { Profile } from "@/features/climbing/api";
import { useFriendAction, useFriendRequests, useFriendsOf, type FriendAction, type FriendPerson } from "@/features/users/friendsApi";
import { Avatar } from "@/components/Avatar";
import { Button } from "@/components/Button";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { useAuth } from "@/auth/AuthProvider";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/** Whether the server knows friendships: older ones send no `friendship` on a profile, and then the app offers none. */
export const supportsFriends = (p: Profile) => "friendship" in p;

/**
 * The friendship with the person whose profile this is (ADR-063): add, the request sent (with a way to cancel it),
 * their request to accept or decline, or that you are friends — ending it is in the page's menu. Nothing on your own
 * profile, on someone you blocked, or against a server without friends. Signed out, adding asks to sign in first.
 * While a request travels the buttons wait, so nothing is sent twice.
 */
export function FriendButton({ profile: p }: { profile: Profile }) {
  const { session } = useAuth();
  const location = useLocation();
  const action = useFriendAction(p.id);
  const toast = useToast();
  if (p.isMe || p.isBlocked || !supportsFriends(p)) return null;
  if (!session) {
    return (
      <Link to={`/sign-in?next=${encodeURIComponent(location.pathname)}`} className="btn btn--primary friend-btn">
        <UserPlus aria-hidden /><span>{t("Add friend")}</span>
      </Link>
    );
  }
  const run = (a: FriendAction, done?: string) =>
    action.mutate(a, { onSuccess: () => done && toast.success(done), onError: (e) => toast.error(errorMessage(e)) });
  const busy = action.isPending;
  const variable = action.variables;

  switch (p.friendship) {
    case "OUTGOING":
      return (
        <div className="friend-actions">
          <span className="friend-state" role="status"><Clock aria-hidden /> {t("Request sent")}</span>
          <Button variant="ghost" loading={busy} onClick={() => run("remove", t("Request cancelled"))}>{t("Cancel request")}</Button>
        </div>
      );
    case "INCOMING":
      return (
        <div className="friend-actions">
          <Button icon={<Check aria-hidden />} loading={busy && variable === "accept"} disabled={busy} onClick={() => run("accept", t("You're friends now"))}>
            {t("Accept request")}
          </Button>
          <Button variant="ghost" icon={<X aria-hidden />} loading={busy && variable === "remove"} disabled={busy} onClick={() => run("remove", t("Request declined"))}>
            {t("Decline")}
          </Button>
        </div>
      );
    case "FRIENDS":
      return <span className="friend-state friend-state--friends" role="status"><UserCheck aria-hidden /> {t("Friends")}</span>;
    default:
      return (
        <Button className="friend-btn" icon={<UserPlus aria-hidden />} loading={busy} onClick={() => run("request", t("Friend request sent"))}>
          {t("Add friend")}
        </Button>
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
      <ul className="people">{requests.data.map((r) => <RequestRow key={r.person.userId} person={r.person} />)}</ul>
    </section>
  );
}

function RequestRow({ person }: { person: FriendPerson }) {
  const action = useFriendAction(person.userId);
  const toast = useToast();
  const run = (a: FriendAction, done: string) => action.mutate(a, { onSuccess: () => toast.success(done), onError: (e) => toast.error(errorMessage(e)) });
  return (
    <li className="people__row">
      <PersonLink person={person} />
      <span className="people__actions">
        <Button loading={action.isPending && action.variables === "accept"} disabled={action.isPending} onClick={() => run("accept", t("You're friends now"))}>{t("Accept")}</Button>
        <Button variant="ghost" aria-label={t("Decline {name}'s request", { name: person.displayName })} icon={<X aria-hidden />}
          loading={action.isPending && action.variables === "remove"} disabled={action.isPending} onClick={() => run("remove", t("Request declined"))} />
      </span>
    </li>
  );
}

function FriendList({ userId, isMe }: { userId: string; isMe: boolean }) {
  const friends = useFriendsOf(userId);
  const items = friends.data?.pages.flatMap((pg) => pg.items) ?? [];
  if (friends.isPending) return <LoadingState label={t("Loading friends")} />;
  if (friends.isError) return <ErrorState error={friends.error} onRetry={() => friends.refetch()} />;
  if (items.length === 0) {
    return <EmptyState icon={<Users />} title={isMe ? t("No friends yet") : t("No friends to show")}
      body={isMe ? t("Open a climber's profile and tap “Add friend”.") : undefined} />;
  }
  return (
    <section className="profile-block" aria-labelledby="friends-title">
      <h2 id="friends-title" className="profile-block__title">{t("Friends")}</h2>
      <ul className="people">{items.map((f) => <li key={f.person.userId} className="people__row"><PersonLink person={f.person} /></li>)}</ul>
      {friends.hasNextPage && <Button variant="secondary" onClick={() => friends.fetchNextPage()} loading={friends.isFetchingNextPage}>{t("Show more")}</Button>}
    </section>
  );
}

function PersonLink({ person }: { person: FriendPerson }) {
  return (
    <Link to={`/users/${person.userId}`} className="people__person">
      <Avatar name={person.displayName} url={person.avatarUrl} size={40} />
      <span className="people__name">{person.displayName}</span>
    </Link>
  );
}
