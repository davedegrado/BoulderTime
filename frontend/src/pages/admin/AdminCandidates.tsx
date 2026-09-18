import { useState } from "react";
import { Link } from "react-router-dom";
import { Globe, Inbox, Mail } from "lucide-react";
import { useAdminCandidates, useSetCandidateStatus } from "@/features/admin/api";
import type { GymCandidate } from "@/features/candidates/api";
import { CreateGymForm } from "@/pages/admin/CreateGymForm";
import { Button } from "@/components/Button";
import { Badge } from "@/components/Badge";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { candidateStatusLabel, formatDate, type CandidateStatus } from "@/lib/format";
import { t } from "@/i18n/i18n";

const FILTERS: { value: CandidateStatus | ""; label: string }[] = [
  { value: "PENDING", label: t("Pending") },
  { value: "CONTACTED", label: t("Contacted") },
  { value: "ACCEPTED", label: t("Accepted") },
  { value: "REJECTED", label: t("Rejected") },
  { value: "", label: t("All") },
];

export function AdminCandidates() {
  const [status, setStatus] = useState<CandidateStatus | "">("PENDING");
  const list = useAdminCandidates(status);

  return (
    <div className="stack">
      <div className="chips" role="radiogroup" aria-label={t("Filter by status")}>
        {FILTERS.map((f) => (
          <button key={f.label} role="radio" aria-checked={status === f.value} className="chip" onClick={() => setStatus(f.value)}>{f.label}</button>
        ))}
      </div>
      {list.isPending ? <LoadingState label={t(t("Loading suggestions"))} />
        : list.isError ? <ErrorState error={list.error} onRetry={() => list.refetch()} />
        : list.data.items.length === 0 ? <EmptyState icon={<Inbox />} title={t(t("Nothing here"))} body={status === "PENDING" ? t("No new gym suggestions. Nice and tidy.") : t("No suggestions with this status.")} />
        : <div className="stack">{list.data.items.map((c) => <CandidateCard key={c.id} candidate={c} />)}</div>}
    </div>
  );
}

function CandidateCard({ candidate: c }: { candidate: GymCandidate }) {
  const setStatus = useSetCandidateStatus();
  const toast = useToast();
  const [creating, setCreating] = useState(false);
  const act = (status: CandidateStatus) =>
    setStatus.mutate({ id: c.id, status }, {
      onSuccess: () => toast.success(t("Marked as {status}", { status: candidateStatusLabel[status].toLowerCase() })),
      onError: (e) => toast.error(errorMessage(e)),
    });

  if (creating) {
    return <CreateGymForm initial={{ name: c.gymName, city: c.city, website: c.website ?? "", email: c.officialEmail ?? "", candidateId: c.id }} onCancel={() => setCreating(false)} />;
  }

  return (
    <article className="card candidate">
      <header className="candidate__head">
        <div>
          <h3 className="list__title">{c.gymName}</h3>
          <p className="list__sub">{c.city} · suggested by {c.submittedBy ?? "a climber"} on {formatDate(c.createdAt)}</p>
        </div>
        <Badge tone={c.status === "ACCEPTED" ? "success" : c.status === "REJECTED" ? "danger" : c.status === "CONTACTED" ? "orange" : "neutral"}>
          {candidateStatusLabel[c.status]}
        </Badge>
      </header>
      {(c.website || c.officialEmail) && (
        <div className="candidate__links">
          {c.website && <a href={c.website} target="_blank" rel="noreferrer"><Globe aria-hidden /> {c.website.replace(/^https?:\/\//, "")}</a>}
          {c.officialEmail && <a href={`mailto:${c.officialEmail}`}><Mail aria-hidden /> {c.officialEmail}</a>}
        </div>
      )}
      {c.notes && <p className="prose candidate__notes">{c.notes}</p>}
      {c.gymId ? (
        <Link to="/admin/gyms" className="btn btn--secondary"><span>{t("Gym created — open gyms")}</span></Link>
      ) : (
        <div className="form__actions">
          <Button onClick={() => setCreating(true)}>{t(t("Create gym"))}</Button>
          {c.status !== "CONTACTED" && <Button variant="secondary" onClick={() => act("CONTACTED")} disabled={setStatus.isPending}>{t(t("Mark contacted"))}</Button>}
          {c.status !== "REJECTED" && <Button variant="ghost" onClick={() => act("REJECTED")} disabled={setStatus.isPending}>{t(t("Reject"))}</Button>}
        </div>
      )}
    </article>
  );
}
