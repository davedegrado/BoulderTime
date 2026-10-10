import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ChevronRight, Users, X } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { useCompletions } from "@/features/climbing/api";
import { Avatar } from "@/components/Avatar";
import { Button } from "@/components/Button";
import { ErrorState, LoadingState } from "@/components/States";
import { formatDate } from "@/lib/format";
import { plural, t } from "@/i18n/i18n";

/**
 * "Completed by 18 people" on a boulder's page. The number is for everyone; tapping it lists the people by name,
 * which needs an account. Nobody yet: it says so, because a first send is worth chasing.
 */
export function CompletionsLine({ boulderId, count }: { boulderId: string; count: number }) {
  const { session } = useAuth();
  const [open, setOpen] = useState(false);
  if (count === 0) return <p className="completions completions--none"><Users aria-hidden /> {t("Nobody has completed it yet")}</p>;
  const label = plural(count, "Completed by 1 person", "Completed by {count} people");
  if (!session) return <p className="completions"><Users aria-hidden /> {label}</p>;
  return (
    <>
      <button type="button" className="completions completions--button" onClick={() => setOpen(true)} aria-haspopup="dialog">
        <Users aria-hidden /> <span>{label}</span> <ChevronRight aria-hidden className="completions__chevron" />
      </button>
      {open && <CompletionsSheet boulderId={boulderId} onClose={() => setOpen(false)} />}
    </>
  );
}

function CompletionsSheet({ boulderId, onClose }: { boulderId: string; onClose: () => void }) {
  const list = useCompletions(boulderId, true);
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const pages = list.data?.pages ?? [];
  const people = pages.flatMap((p) => p.people.items);
  const total = pages[0]?.total ?? 0;
  const shown = pages[0]?.people.total ?? 0;
  const others = Math.max(0, total - shown);

  return (
    <div className="sheet-backdrop" onClick={onClose}>
      <div className="sheet completions-sheet" role="dialog" aria-modal="true" aria-labelledby="completions-title" onClick={(e) => e.stopPropagation()}>
        <div className="sector-sheet__head">
          <h2 id="completions-title" className="section__title">{total ? plural(total, "Completed by 1 person", "Completed by {count} people") : t("Completed by")}</h2>
          <button type="button" className="icon-btn" onClick={onClose} aria-label={t("Close")}><X aria-hidden /></button>
        </div>
        {list.isPending ? <LoadingState />
          : list.isError ? <ErrorState error={list.error} onRetry={() => list.refetch()} />
          : (
            <>
              <ul className="list completions-sheet__list">
                {people.map((c) => (
                  <li key={c.person.userId}>
                    <Link to={`/users/${c.person.userId}`} className="list__row list__row--link" onClick={onClose}>
                      <Avatar name={c.person.displayName} url={c.person.avatarUrl} size={36} />
                      <span className="list__main">
                        <span className="list__title">{c.person.displayName}</span>
                        {c.completedAt && <span className="list__sub">{formatDate(c.completedAt)}</span>}
                      </span>
                      <ChevronRight className="list__chevron" aria-hidden />
                    </Link>
                  </li>
                ))}
              </ul>
              {list.hasNextPage && <Button variant="secondary" onClick={() => list.fetchNextPage()} loading={list.isFetchingNextPage}>{t("Show more")}</Button>}
              {others > 0 && !list.hasNextPage && (
                <p className="field__hint">{plural(others, "And 1 person who prefers not to appear.", "And {count} people who prefer not to appear.")}</p>
              )}
            </>
          )}
      </div>
    </div>
  );
}
