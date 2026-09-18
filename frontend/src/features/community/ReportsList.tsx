import { useState } from "react";
import { Link } from "react-router-dom";
import { CheckCircle2, Flag, ShieldOff, XCircle } from "lucide-react";
import { useCloseReport, useReports, reportReasonLabel, type Report, type ReportStatus } from "@/features/community/api";
import { Badge } from "@/components/Badge";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { formatDate } from "@/lib/format";
import { t } from "@/i18n/i18n";

const TYPE_LABEL = { COMMENT: t("Comment"), VIDEO: t("Video"), BOULDER: t("Boulder") } as const;

/** Report queue shared by the gym staff area (scope = gym id) and platform admin (scope = "all"). */
export function ReportsList({ scope, showGym = false }: { scope: string; showGym?: boolean }) {
  const [status, setStatus] = useState<ReportStatus>("PENDING");
  const reports = useReports(scope, status);

  return (
    <div className="stack">
      <div className="chips" role="radiogroup" aria-label={t("Report status")}>
        {(["PENDING", "RESOLVED", "DISMISSED"] as ReportStatus[]).map((s) => (
          <button key={s} role="radio" aria-checked={status === s} className="chip" onClick={() => setStatus(s)}>
            {s === "PENDING" ? t("Open") : s === "RESOLVED" ? t("Resolved") : t("Dismissed")}
          </button>
        ))}
      </div>
      {reports.isPending ? <LoadingState label={t(t("Loading reports"))} />
        : reports.isError ? <ErrorState error={reports.error} onRetry={() => reports.refetch()} />
        : reports.data.items.length === 0 ? <EmptyState icon={<Flag />} title={status === "PENDING" ? t("No open reports") : t("Nothing here")} body={status === "PENDING" ? t("All clear.") : undefined} />
        : reports.data.items.map((r) => <ReportCard key={r.id} report={r} showGym={showGym} />)}
    </div>
  );
}

function ReportCard({ report: r, showGym }: { report: Report; showGym: boolean }) {
  const close = useCloseReport();
  const toast = useToast();
  const [note, setNote] = useState("");
  const act = (opts: { dismiss?: boolean; removeContent?: boolean }, message: string) =>
    close.mutate({ id: r.id, note: note.trim() || undefined, ...opts }, { onSuccess: () => toast.success(message), onError: (e) => toast.error(errorMessage(e)) });

  return (
    <article className="card report">
      <header className="candidate__head">
        <div>
          <p className="list__title">{TYPE_LABEL[r.entityType]} · {reportReasonLabel[r.reason]}</p>
          <p className="list__sub">By {r.reportedBy.displayName} · {formatDate(r.createdAt)}{showGym && ` · ${r.gymName}`}</p>
        </div>
        <Badge tone={r.status === "PENDING" ? "orange" : r.status === "RESOLVED" ? "success" : "neutral"}>
          {r.status === "PENDING" ? t("Open") : r.status === "RESOLVED" ? t("Resolved") : t("Dismissed")}
        </Badge>
      </header>
      {r.excerpt && <blockquote className="report__excerpt">{r.excerpt}</blockquote>}
      {r.description && <p className="prose">“{r.description}”</p>}
      {r.resolutionNote && <p className="list__sub">Note: {r.resolutionNote}</p>}
      {r.boulderId !== "00000000-0000-0000-0000-000000000000" && <Link to={`/boulders/${r.boulderId}`} className="section__link">{t("Open boulder")}</Link>}

      {r.status === "PENDING" && (
        <>
          <TextField label={t(t("Note (optional)"))} value={note} onChange={(e) => setNote(e.target.value)} maxLength={500} />
          <div className="form__actions">
            {r.entityType !== "BOULDER" && (
              <Button variant="danger" icon={<ShieldOff aria-hidden />} loading={close.isPending}
                onClick={() => act({ removeContent: true }, r.entityType === "COMMENT" ? t("Comment hidden") : t("Video rejected"))}>
                {r.entityType === "COMMENT" ? t("Hide comment") : t("Reject video")}
              </Button>
            )}
            <Button variant="secondary" icon={<CheckCircle2 aria-hidden />} loading={close.isPending} onClick={() => act({}, "Marked as resolved")}>{t(t("Resolved"))}</Button>
            <Button variant="ghost" icon={<XCircle aria-hidden />} loading={close.isPending} onClick={() => act({ dismiss: true }, "Report dismissed")}>{t(t("Dismiss"))}</Button>
          </div>
        </>
      )}
    </article>
  );
}
