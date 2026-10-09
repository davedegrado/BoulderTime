import { useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ShieldCheck } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthProvider";
import { useCurrentUser } from "@/features/users/api";
import { DeleteAccountSection } from "@/features/users/DeleteAccountSection";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { LEGAL_VERSION } from "@/pages/legal/version";
import { t } from "@/i18n/i18n";

/**
 * Asked once, before the app can be used, and again only when the documents change substantially. The tick is a
 * deliberate act we can record with its version: for an app that holds videos of people who never signed up, being
 * able to show what someone agreed to matters more than saving them a tap. Someone who doesn't agree can sign out or
 * delete the account from here: the rest of the app is behind this page, and nobody may be kept from deleting.
 */
export function AcceptLegalPage({ returning }: { returning: boolean }) {
  const [checked, setChecked] = useState(false);
  const [ageChecked, setAgeChecked] = useState(false);
  const qc = useQueryClient();
  const toast = useToast();
  const { signOut } = useAuth();
  const me = useCurrentUser();
  // Asked once, of whoever hasn't declared it yet: everyone who signed up before the sign-up form asked (ADR-041).
  const needsAge = !(me.data?.minimumAgeConfirmed ?? false);

  const accept = useMutation({
    mutationFn: () => api.post("/api/users/me/legal-acceptance", needsAge ? { version: LEGAL_VERSION, confirmsMinimumAge: true } : { version: LEGAL_VERSION }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["users"] }),
    onError: (e) => toast.error(errorMessage(e)),
  });

  return (
    <div className="page legal-gate">
      <div className="card stack">
        <div className="state__icon" aria-hidden><ShieldCheck /></div>
        <h1 className="page__title">{returning ? t("We've updated our terms") : t("Before you start")}</h1>
        <p className="legal__p">
          {returning
            ? t("The terms of use and the privacy notice have changed. Please read them and accept to continue.")
            : t("Read the terms of use and the privacy notice. They explain what we do with your data, and what you promise when you upload a video of other people.")}
        </p>

        <ul className="list card">
          <li className="list__row"><Link className="list__main list__link" to="/termini">{t("Terms of use")}</Link></li>
          <li className="list__row"><Link className="list__main list__link" to="/privacy">{t("Privacy notice")}</Link></li>
        </ul>

        {needsAge && (
          <label className="check">
            <input type="checkbox" checked={ageChecked} onChange={(e) => setAgeChecked(e.target.checked)} />
            <span>{t("I'm at least 14 years old.")}</span>
          </label>
        )}
        <label className="check">
          <input type="checkbox" checked={checked} onChange={(e) => setChecked(e.target.checked)} />
          <span>{t("I accept the terms of use and I have read the privacy notice.")}</span>
        </label>

        <Button disabled={!checked || (needsAge && !ageChecked)} loading={accept.isPending} onClick={() => accept.mutate()} block>
          {t("Continue")}
        </Button>
        <Button variant="ghost" onClick={() => void signOut()} block>{t("Sign out")}</Button>
        {me.data && <DeleteAccountSection email={me.data.email} />}
      </div>
    </div>
  );
}
