import { useEffect, useState } from "react";
import { Check, UserCheck, UserPlus, UserX, Users } from "lucide-react";
import { SearchField } from "@/components/SearchField";
import { Button } from "@/components/Button";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { useCurrentUser } from "@/features/users/api";
import { FriendList, PersonLink, RequestRow } from "@/features/users/Friends";
import {
  SEARCH_MIN_LENGTH, useFriendAction, useFriendRequests, usePeopleSearch, useSentFriendRequests,
  type FriendAction, type FriendPerson, type PersonResult,
} from "@/features/users/friendsApi";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/**
 * Friends, from the tab bar (ADR-063): a search for climbers by name on top, then the requests waiting — received, to
 * accept or decline, and sent, to cancel — then your friends. While searching, the results take the page.
 */
export function FriendsPage() {
  const me = useCurrentUser();
  const [query, setQuery] = useState("");
  // The list is asked for once typing pauses, not at every letter.
  const [searched, setSearched] = useState("");
  useEffect(() => {
    const id = window.setTimeout(() => setSearched(query.trim()), 300);
    return () => window.clearTimeout(id);
  }, [query]);
  const searching = query.trim().length > 0;

  return (
    <div className="page page--narrow friends-page">
      <h1 className="page__title">{t("Friends")}</h1>
      <SearchField label={t("Search climbers")} placeholder={t("Search climbers by name")} value={query} onChange={setQuery} />
      {searching ? <SearchResults query={searched} typed={query.trim()} /> : (
        <>
          <ReceivedRequests />
          <SentRequests />
          {me.data && <FriendList userId={me.data.id} isMe />}
        </>
      )}
    </div>
  );
}

function SearchResults({ query, typed }: { query: string; typed: string }) {
  const results = usePeopleSearch(query);
  if (typed.length < SEARCH_MIN_LENGTH) return <p className="field__hint">{t("Type at least two letters of a name.")}</p>;
  if (results.isPending || query !== typed) return <LoadingState label={t("Searching")} />;
  if (results.isError) return <ErrorState error={results.error} onRetry={() => results.refetch()} />;
  if (results.data.length === 0) return <EmptyState icon={<Users />} title={t("Nobody called “{query}”", { query })} />;
  return (
    <section className="profile-block" aria-label={t("Search results")}>
      <ul className="people">{results.data.map((r) => <ResultRow key={r.person.userId} result={r} />)}</ul>
    </section>
  );
}

/** One climber found: what you can do with them right there, as on their profile. */
function ResultRow({ result: { person, friendship } }: { result: PersonResult }) {
  const action = useFriendAction(person.userId);
  const toast = useToast();
  const run = (a: FriendAction, done: string) => action.mutate(a, { onSuccess: () => toast.success(done), onError: (e) => toast.error(errorMessage(e)) });
  const busy = action.isPending;
  return (
    <li className="people__row">
      <PersonLink person={person} />
      <span className="people__actions">
        {friendship === "FRIENDS" ? <span className="people__state"><UserCheck aria-hidden /> {t("Friends")}</span>
          : friendship === "OUTGOING" ? (
            <Button variant="secondary" icon={<UserX aria-hidden />} loading={busy} onClick={() => run("remove", t("Request cancelled"))}>{t("Cancel request")}</Button>
          ) : friendship === "INCOMING" ? (
            <Button icon={<Check aria-hidden />} loading={busy} onClick={() => run("accept", t("You're friends now"))}>{t("Accept")}</Button>
          ) : (
            <Button icon={<UserPlus aria-hidden />} loading={busy} onClick={() => run("request", t("Friend request sent"))}>{t("Add")}</Button>
          )}
      </span>
    </li>
  );
}

function ReceivedRequests() {
  const requests = useFriendRequests();
  if (!requests.data?.length) return null;
  return (
    <section className="profile-block" aria-labelledby="received-title">
      <h2 id="received-title" className="profile-block__title">{t("Requests received")}</h2>
      <ul className="people">{requests.data.map((r) => <RequestRow key={r.person.userId} person={r.person} />)}</ul>
    </section>
  );
}

function SentRequests() {
  const sent = useSentFriendRequests();
  if (!sent.data?.length) return null;
  return (
    <section className="profile-block" aria-labelledby="sent-title">
      <h2 id="sent-title" className="profile-block__title">{t("Requests sent")}</h2>
      <ul className="people">{sent.data.map((r) => <SentRow key={r.person.userId} person={r.person} />)}</ul>
    </section>
  );
}

function SentRow({ person }: { person: FriendPerson }) {
  const action = useFriendAction(person.userId);
  const toast = useToast();
  return (
    <li className="people__row">
      <PersonLink person={person} />
      <span className="people__actions">
        <Button variant="secondary" aria-label={t("Cancel the request to {name}", { name: person.displayName })} loading={action.isPending}
          onClick={() => action.mutate("remove", { onSuccess: () => toast.success(t("Request cancelled")), onError: (e) => toast.error(errorMessage(e)) })}>
          {t("Cancel")}
        </Button>
      </span>
    </li>
  );
}
