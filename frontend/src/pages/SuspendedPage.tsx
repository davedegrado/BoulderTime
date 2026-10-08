import { ShieldAlert } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { useCurrentUser } from "@/features/users/api";
import { DeleteAccountSection } from "@/features/users/DeleteAccountSection";
import { Button } from "@/components/Button";
import { SUPPORT_EMAIL } from "@/lib/contact";
import { t } from "@/i18n/i18n";

/**
 * What a suspended account sees instead of the app (ADR-037). The API refuses everything else anyway; this says why,
 * how to object, and keeps the two things nobody may be prevented from doing: signing out and deleting the account.
 */
export function SuspendedPage() {
  const { signOut } = useAuth();
  const me = useCurrentUser();

  return (
    <div className="page legal-gate">
      <div className="card stack">
        <div className="state__icon" aria-hidden><ShieldAlert /></div>
        <h1 className="page__title">{t("Your account is suspended")}</h1>
        <p className="legal__p">{t("BoulderTime suspended this account because it was used against the terms of use. While it is suspended you can't use BoulderTime, and what you wrote is hidden from everyone.")}</p>
        <p className="legal__p">
          {t("If you think it's a mistake, write to us:")} <a href={`mailto:${SUPPORT_EMAIL}`}>{SUPPORT_EMAIL}</a>
        </p>
        <Button variant="secondary" onClick={() => void signOut()} block>{t("Sign out")}</Button>
        {me.data && <DeleteAccountSection email={me.data.email} />}
      </div>
    </div>
  );
}
