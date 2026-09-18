import { useState, type FormEvent } from "react";
import { Flag } from "lucide-react";
import { useCreateReport, reportReasonLabel, type ReportEntityType, type ReportReason } from "@/features/community/api";
import { SelectField, TextAreaField } from "@/components/Fields";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/** Small "Report" link that expands into an inline form (no modal: works well one-handed on a phone). */
export function ReportButton({ entityType, entityId, label = t("Report") }: { entityType: ReportEntityType; entityId: string; label?: string }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState<ReportReason>("INAPPROPRIATE");
  const [description, setDescription] = useState("");
  const report = useCreateReport();
  const toast = useToast();
  const err = report.error instanceof ApiError ? report.error : null;

  if (!open) {
    return <button type="button" className="text-btn" onClick={() => setOpen(true)}><Flag aria-hidden /> {label}</button>;
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    report.mutate({ entityType, entityId, reason, description: description.trim() || undefined }, {
      onSuccess: () => { toast.success("Thanks — the gym will review it."); setOpen(false); setDescription(""); },
      onError: (e2) => { if (!(e2 instanceof ApiError && e2.isValidation)) toast.error(errorMessage(e2)); },
    });
  }

  return (
    <form className="report-form" onSubmit={onSubmit} noValidate>
      <SelectField label={t("What's wrong?")} value={reason} onChange={(e) => setReason(e.target.value as ReportReason)}
        options={(Object.keys(reportReasonLabel) as ReportReason[]).map((r) => ({ value: r, label: reportReasonLabel[r] }))} />
      <TextAreaField label={t("Details")} rows={2} value={description} onChange={(e) => setDescription(e.target.value)}
        error={err?.fieldError("description")} hint={reason === "OTHER" ? t("Required for “Other”.") : t("Optional")} maxLength={500} />
      <div className="form__actions">
        <Button type="submit" variant="danger" loading={report.isPending}>{t("Send report")}</Button>
        <Button variant="ghost" onClick={() => setOpen(false)}>{t("Cancel")}</Button>
      </div>
    </form>
  );
}
