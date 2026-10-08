import { useState } from "react";
import { Link } from "react-router-dom";
import { UserRound } from "lucide-react";
import { ReportsList } from "@/features/community/ReportsList";
import { useAccountSuspension, useUserReports } from "@/features/admin/api";
import { personReportReasons } from "@/features/users/api";
import { Avatar } from "@/components/Avatar";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { TextField } from "@/components/TextField";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { formatDate } from "@/lib/format";
import { plural, t } from "@/i18n/i18n";

export function AdminReports() {
  return (
    <div className="stack">
      <PersonReports />
      <ReportsList scope="all" showGym />
    </div>
  );
}

/**
 * People reported to BoulderTime (ADR-037). Suspending hides everything the person wrote and stops the account at
 * the door, everywhere; it settles every open report about them, and it can be lifted from the list of users.
 */
function PersonReports() {
  const reports = useUserReports();
  const { handle } = useAccountSuspension();
  const toast = useToast();
  const [notes, setNotes] = useState<Record<string, string>>({});
  const onError = (e: unknown) => toast.error(errorMessage(e));
  const reasonLabel = Object.fromEntries(personReportReasons().map((r) => [r.value, r.label]));

  return (
    <section className="card stack" aria-labelledby="people-reports-title">
      <h2 id="people-reports-title" className="section__title"><UserRound aria-hidden className="title-icon" /> {t("Reported people")}</h2>
      <p className="field__hint">{t("Sent by climbers about a person, not a single comment. Suspending stops the account everywhere and hides what it wrote, until you lift it from Users.")}</p>
      {reports.isPending ? <LoadingState label={t("Loading reports")} />
        : reports.isError ? <ErrorState error={reports.error} onRetry={() => reports.refetch()} />
        : reports.data.length === 0 ? <EmptyState icon={<UserRound />} title={t("No open reports")} body={t("All clear.")} />
        : (
          <ul className="list">
            {reports.data.map((r) => (
              <li key={r.id} className="list__row list__row--stack">
                <div className="list__main">
                  <Link to={`/users/${r.person.userId}`} className="list__title">
                    <Avatar name={r.person.displayName} url={r.person.avatarUrl} size={28} /> {r.person.displayName}
                  </Link>
                  <p className="list__sub">
                    {reasonLabel[r.reason] ?? r.reason} · {t("reported by {name}", { name: r.reportedBy.displayName })} · {formatDate(r.createdAt)}
                    {r.openReportsAgainstPerson > 1 && ` · ${plural(r.openReportsAgainstPerson, "{count} open report", "{count} open reports")}`}
                  </p>
                  {r.description && <p className="list__sub">{r.description}</p>}
                  <TextField label={t("Note (optional)")} value={notes[r.id] ?? ""} maxLength={500}
                    onChange={(e) => setNotes((n) => ({ ...n, [r.id]: e.target.value }))} />
                </div>
                <div className="row">
                  <ConfirmButton variant="danger" confirmLabel={t("Suspend?")} loading={handle.isPending}
                    onConfirm={() => handle.mutate({ reportId: r.id, suspend: true, note: notes[r.id] }, {
                      onSuccess: () => toast.success(t("{name}'s account is suspended", { name: r.person.displayName })), onError,
                    })}>
                    {t("Suspend account")}
                  </ConfirmButton>
                  <Button variant="secondary" loading={handle.isPending}
                    onClick={() => handle.mutate({ reportId: r.id, suspend: false, note: notes[r.id] }, {
                      onSuccess: () => toast.success(t("Report dismissed")), onError,
                    })}>
                    {t("Dismiss")}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
    </section>
  );
}
