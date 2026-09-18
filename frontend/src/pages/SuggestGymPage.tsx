import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { Send, Store } from "lucide-react";
import { useMyCandidates, useSubmitCandidate, type SubmitCandidateInput } from "@/features/candidates/api";
import { TextField } from "@/components/TextField";
import { TextAreaField } from "@/components/Fields";
import { Button } from "@/components/Button";
import { Badge } from "@/components/Badge";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { candidateStatusLabel, formatDate } from "@/lib/format";
import { t } from "@/i18n/i18n";

const empty: SubmitCandidateInput = { gymName: "", city: "", officialEmail: "", website: "", notes: "" };

export function SuggestGymPage() {
  const [form, setForm] = useState(empty);
  const submit = useSubmitCandidate();
  const mine = useMyCandidates();
  const toast = useToast();
  const err = submit.error instanceof ApiError ? submit.error : null;
  const set = (k: keyof SubmitCandidateInput) => (e: { target: { value: string } }) => setForm((f) => ({ ...f, [k]: e.target.value }));

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    submit.mutate(form, {
      onSuccess: () => {
        setForm(empty);
        toast.success(t("Thanks! We'll get in touch with the gym."));
      },
      onError: (error) => {
        if (!(error instanceof ApiError && error.isValidation)) toast.error(errorMessage(error));
      },
    });
  }

  return (
    <div className="page page--narrow">
      <header className="page__header">
        <h1 className="page__title">{t("Suggest a gym")}</h1>
        <p className="page__subtitle">{t("Tell us where you climb. The BoulderTime team contacts the gym to bring it on board.")}</p>
      </header>

      <form className="card form" onSubmit={onSubmit} noValidate>
        <TextField label={t("Gym name")} value={form.gymName} onChange={set("gymName")} error={err?.fieldError("gymName")} required />
        <TextField label={t("City")} value={form.city} onChange={set("city")} error={err?.fieldError("city")} autoComplete="address-level2" required />
        <TextField label={t("Website")} type="url" inputMode="url" placeholder={t("https://")} value={form.website} onChange={set("website")} error={err?.fieldError("website")} hint={t("Optional")} />
        <TextField label={t("Gym's email")} type="email" inputMode="email" value={form.officialEmail} onChange={set("officialEmail")} error={err?.fieldError("officialEmail")} hint={t("Optional — helps us reach the right person")} />
        <TextAreaField label={t("Anything else?")} rows={3} value={form.notes} onChange={set("notes")} error={err?.fieldError("notes")} hint={t("Optional")} />
        <Button type="submit" icon={<Send aria-hidden />} loading={submit.isPending}>{t("Send suggestion")}</Button>
      </form>

      {mine.data && mine.data.length > 0 && (
        <section className="section" aria-labelledby="my-suggestions">
          <h2 id="my-suggestions" className="section__title">{t("Your suggestions")}</h2>
          <ul className="list">
            {mine.data.map((c) => (
              <li key={c.id} className="list__row">
                <Store className="list__icon" aria-hidden />
                <div className="list__main">
                  <p className="list__title">{c.gymName}</p>
                  <p className="list__sub">{c.city} · {formatDate(c.createdAt)}</p>
                </div>
                <Badge tone={c.status === "ACCEPTED" ? "success" : c.status === "REJECTED" ? "danger" : "neutral"}>{candidateStatusLabel[c.status]}</Badge>
              </li>
            ))}
          </ul>
        </section>
      )}
      <Link to="/explore" className="btn btn--ghost"><span>{t("Back to Explore")}</span></Link>
    </div>
  );
}
