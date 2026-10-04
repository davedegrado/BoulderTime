import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { TriangleAlert } from "lucide-react";
import { api } from "@/lib/api";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { formatDate } from "@/lib/format";
import { SUPPORT_EMAIL } from "@/lib/contact";
import { t } from "@/i18n/i18n";

interface DeletionStatus { pendingDeletion: boolean; requestedAt: string | null; erasedAfter: string | null }

/**
 * Deleting the account. Two deliberate frictions: the person types their own address, and the data survives a week
 * so a decision taken in a bad moment can be undone.
 */
export function DeleteAccountSection({ email }: { email: string }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [open, setOpen] = useState(false);
  const [typed, setTyped] = useState("");
  const [error, setError] = useState<string | null>(null);

  const status = useQuery({ queryKey: ["users", "me", "deletion"], queryFn: () => api.get<DeletionStatus>("/api/users/me/deletion") });
  const refresh = () => qc.invalidateQueries({ queryKey: ["users"] });

  const request = useMutation({
    mutationFn: () => api.post<DeletionStatus>("/api/users/me/deletion", { confirmEmail: typed.trim() }),
    onSuccess: () => { setOpen(false); setTyped(""); refresh(); toast.success(t("Account scheduled for deletion")); },
    onError: (e) => setError(errorMessage(e)),
  });
  const cancel = useMutation({
    mutationFn: () => api.delete<DeletionStatus>("/api/users/me/deletion"),
    onSuccess: () => { refresh(); toast.success(t("Your account is active again")); },
    onError: (e) => toast.error(errorMessage(e)),
  });

  if (status.data?.pendingDeletion) {
    return (
      <section className="section" aria-labelledby="delete-title">
        <h2 id="delete-title" className="section__title">{t("Account deletion")}</h2>
        <div className="card stack">
          <p className="notice">
            <TriangleAlert aria-hidden />
            {t("Your account will be erased on {date}. Until then you can bring it back, and nothing is lost.", {
              date: status.data.erasedAfter ? formatDate(status.data.erasedAfter) : "",
            })}
          </p>
          <Button loading={cancel.isPending} onClick={() => cancel.mutate()}>{t("Keep my account")}</Button>
        </div>
      </section>
    );
  }

  return (
    <section className="section" aria-labelledby="delete-title">
      <h2 id="delete-title" className="section__title">{t("Account deletion")}</h2>
      <div className="card stack">
        {!open ? (
          <>
            <p className="field__hint">{t("Deleting removes your profile, your climbing history, your comments and your videos.")}</p>
            <Button variant="secondary" onClick={() => setOpen(true)}>{t("Delete my account")}</Button>
          </>
        ) : (
          <>
            <p className="field__hint">
              {t("These are erased for good: profile and photo, attempts and sends, ratings and grade suggestions, comments, your videos, the gyms you follow.")}
            </p>
            <p className="field__hint">
              {t("These stay with the gym, without your name: boulders you set and official beta you filmed.")}
            </p>
            <p className="field__hint">{t("You have 7 days to change your mind. After that it can't be undone.")}</p>
            <TextField label={t("Type {email} to confirm", { email })} value={typed} autoComplete="off"
              onChange={(e) => { setTyped(e.target.value); setError(null); }} error={error ?? undefined} />
            <div className="row">
              <Button variant="danger" loading={request.isPending} disabled={typed.trim().toLowerCase() !== email.toLowerCase()}
                onClick={() => request.mutate()}>
                {t("Delete my account")}
              </Button>
              <Button variant="ghost" onClick={() => { setOpen(false); setTyped(""); setError(null); }}>{t("Cancel")}</Button>
            </div>
            <p className="field__hint">{t("Need help instead? Write to {email}.", { email: SUPPORT_EMAIL })}</p>
          </>
        )}
      </div>
    </section>
  );
}
