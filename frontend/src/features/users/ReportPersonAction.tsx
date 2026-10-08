import { useState, type FormEvent } from "react";
import { Flag } from "lucide-react";
import { personReportReasons, useReportPerson, type PersonReportReason } from "@/features/users/api";
import { SelectField, TextAreaField } from "@/components/Fields";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/**
 * Reporting a person to BoulderTime, from their profile. Blocking is the reader's own tool; this asks BoulderTime to
 * act, and can end in the account being suspended everywhere. Inline rather than a modal, like the content reports.
 */
export function ReportPersonAction({ userId, displayName }: { userId: string; displayName: string }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState<PersonReportReason>("HARASSMENT");
  const [description, setDescription] = useState("");
  const report = useReportPerson();
  const toast = useToast();
  const err = report.error instanceof ApiError ? report.error : null;

  if (!open) {
    return <button type="button" className="text-btn" onClick={() => setOpen(true)}><Flag aria-hidden /> {t("Report {name}", { name: displayName })}</button>;
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    report.mutate({ userId, reason, description: description.trim() || undefined }, {
      onSuccess: () => { toast.success(t("Thanks — the BoulderTime team will review it.")); setOpen(false); setDescription(""); },
      onError: (e2) => { if (!(e2 instanceof ApiError && e2.isValidation)) toast.error(errorMessage(e2)); },
    });
  }

  return (
    <form className="report-form" onSubmit={onSubmit} noValidate>
      <p className="field__hint">{t("Reports about a person go to the BoulderTime team, not to the gym. {name} isn't told who reported them.", { name: displayName })}</p>
      <SelectField label={t("What's wrong?")} value={reason} onChange={(e) => setReason(e.target.value as PersonReportReason)}
        options={personReportReasons()} />
      <TextAreaField label={t("Details")} rows={2} value={description} onChange={(e) => setDescription(e.target.value)}
        error={err?.fieldError("description")} hint={t("Optional")} maxLength={500} />
      <div className="form__actions">
        <Button type="submit" variant="danger" loading={report.isPending}>{t("Send report")}</Button>
        <Button variant="ghost" onClick={() => setOpen(false)}>{t("Cancel")}</Button>
      </div>
    </form>
  );
}
