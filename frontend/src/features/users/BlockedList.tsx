import { Link } from "react-router-dom";
import { useBlockedPeople, useBlockPerson } from "@/features/users/api";
import { Avatar } from "@/components/Avatar";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/** The people you've blocked, so a block made months ago can still be found and undone. */
export function BlockedList() {
  const blocked = useBlockedPeople();
  const change = useBlockPerson();
  const toast = useToast();
  const people = blocked.data ?? [];

  if (blocked.isPending || people.length === 0) return null;

  return (
    <section className="section" aria-labelledby="blocked-title">
      <h2 id="blocked-title" className="section__title">{t("Blocked people")}</h2>
      <ul className="list card">
        {people.map((person) => (
          <li key={person.userId} className="list__row">
            <Link className="list__main" to={`/users/${person.userId}`}>
              <Avatar name={person.displayName} url={person.avatarUrl} size={32} />
              <span>{person.displayName}</span>
            </Link>
            <Button variant="ghost" loading={change.isPending}
              onClick={() => change.mutate({ userId: person.userId, blocked: false }, {
                onSuccess: () => toast.success(t("{name} is unblocked", { name: person.displayName })),
                onError: (e) => toast.error(errorMessage(e)),
              })}>
              {t("Unblock")}
            </Button>
          </li>
        ))}
      </ul>
    </section>
  );
}
