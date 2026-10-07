import { useState } from "react";
import { Ban, Undo2 } from "lucide-react";
import { useBlockPerson } from "@/features/users/api";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/**
 * Blocking someone, from their profile. Deliberately quiet: no moderator is involved, the other person is never
 * told, and it is undone from the same button. Reporting, which asks the gym to act on a piece of content, is a
 * different thing and lives next to the content.
 */
export function BlockAction({ userId, displayName, isBlocked }: { userId: string; displayName: string; isBlocked: boolean }) {
  const block = useBlockPerson();
  const toast = useToast();
  const [confirming, setConfirming] = useState(false);

  const apply = (blocked: boolean) =>
    block.mutate({ userId, blocked }, {
      onSuccess: () => {
        setConfirming(false);
        toast.success(blocked ? t("{name} is blocked", { name: displayName }) : t("{name} is unblocked", { name: displayName }));
      },
      onError: (e) => toast.error(errorMessage(e)),
    });

  if (isBlocked) {
    return (
      <Button variant="secondary" icon={<Undo2 aria-hidden />} loading={block.isPending} onClick={() => apply(false)}>
        {t("Unblock")}
      </Button>
    );
  }

  if (!confirming) {
    return (
      <Button variant="ghost" icon={<Ban aria-hidden />} onClick={() => setConfirming(true)}>{t("Block")}</Button>
    );
  }

  return (
    <div className="stack">
      <p className="field__hint">{t("You won't see what {name} writes, and they won't see what you write. They aren't told, and you can undo it any time.", { name: displayName })}</p>
      <div className="row">
        <Button variant="danger" icon={<Ban aria-hidden />} loading={block.isPending} onClick={() => apply(true)}>{t("Block")}</Button>
        <Button variant="ghost" onClick={() => setConfirming(false)}>{t("Cancel")}</Button>
      </div>
    </div>
  );
}
