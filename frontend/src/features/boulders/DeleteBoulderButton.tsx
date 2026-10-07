import { useState } from "react";
import { Trash2 } from "lucide-react";
import { useDeleteBoulder, useDeletionImpact } from "@/features/boulders/api";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { plural, t } from "@/i18n/i18n";

/**
 * Erasing a removed boulder. It cannot be undone, so the second tap is only offered once the gym has been told what
 * goes — and, more importantly, what stays: the sends. A gym tidying its wall is not taking points off the people
 * who did the work, and saying so is the difference between a safe button and a frightening one.
 */
export function DeleteBoulderButton({ boulderId }: { boulderId: string }) {
  const [asked, setAsked] = useState(false);
  const impact = useDeletionImpact(boulderId, asked);
  const remove = useDeleteBoulder();
  const toast = useToast();

  if (!asked) {
    return (
      <Button variant="ghost" icon={<Trash2 aria-hidden />} onClick={() => setAsked(true)}>{t("Delete for good")}</Button>
    );
  }

  const i = impact.data;
  const losing = i ? [
    i.comments > 0 ? plural(i.comments, "{count} comment", "{count} comments") : null,
    i.videos > 0 ? plural(i.videos, "{count} climber video", "{count} climber videos") : null,
    i.hasBeta ? t("the official beta") : null,
  ].filter(Boolean) as string[] : [];

  return (
    <div className="notice notice--inline stack" role="alertdialog" aria-label={t("Delete this boulder for good?")}>
      <p><strong>{t("Delete this boulder for good?")}</strong></p>
      {impact.isPending ? <p className="list__sub">{t("Checking what this would remove…")}</p> : (
        <>
          <p className="list__sub">
            {losing.length > 0
              ? t("Its photo goes, and with it {things}. This cannot be undone.", { things: losing.join(", ") })
              : t("Its photo goes. This cannot be undone.")}
          </p>
          {i && i.sends > 0 && (
            <p className="list__sub">
              <strong>{plural(i.sends, "{count} climber keeps this in their history", "{count} climbers keep this in their history")}</strong>
              {" "}{t("with the points it is worth. Deleting does not take that away.")}
            </p>
          )}
        </>
      )}
      <div className="form__actions">
        <Button variant="danger" icon={<Trash2 aria-hidden />} loading={remove.isPending} disabled={impact.isPending}
          onClick={() => remove.mutate(boulderId, {
            onSuccess: () => toast.success(t("Boulder deleted")),
            onError: (e) => { toast.error(errorMessage(e)); setAsked(false); },
          })}>
          {t("Delete for good")}
        </Button>
        <Button variant="ghost" onClick={() => setAsked(false)} disabled={remove.isPending}>{t("Cancel")}</Button>
      </div>
    </div>
  );
}
