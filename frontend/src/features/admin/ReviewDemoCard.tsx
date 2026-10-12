import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { FlaskConical } from "lucide-react";
import { api } from "@/lib/api";
import { ApiError, errorMessage } from "@/lib/apiError";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";
import { t } from "@/i18n/i18n";

/** `photosReplaced`: demo boulders that still had a drawn wall and now have a photo (absent from older servers). */
interface ReviewDemoResult { gymsCreated: number; bouldersCreated: number; gyms: string[]; staffEmail: string | null; photosReplaced?: number }

/**
 * Demo gyms for the stores' reviewers (ADR-051): live but unlisted, so they work like any gym for the reviewers'
 * account (made admin, and following them) while nobody else finds them. Pressing again adds what is missing and changes nothing else.
 */
export function ReviewDemoCard() {
  const qc = useQueryClient();
  const [email, setEmail] = useState("review@bouldertime.com");
  const create = useMutation({
    mutationFn: () => api.post<ReviewDemoResult>("/api/admin/review-demo", { staffEmail: email.trim() || null }),
    onSuccess: () => qc.invalidateQueries(),
  });
  const result = create.data;
  const fieldError = create.error instanceof ApiError ? create.error.fieldError("staffEmail") : undefined;

  return (
    <section className="card stack" aria-labelledby="review-demo-title">
      <h2 id="review-demo-title" className="section__title"><FlaskConical aria-hidden className="title-icon" /> {t("Demo gyms for store review")}</h2>
      <p className="field__hint">
        {t("Creates three demo gyms with boulders, climbers and comments. They work like any gym, but they never show up in search or on the map, so real users don't find them. You become owner; the account below becomes admin and follows them, so reviewers can try both the climber and the staff side. Sign up in the app with that email first.")}
      </p>
      <TextField label={t("Reviewers' account email")} type="email" value={email} onChange={(e) => setEmail(e.target.value)}
        error={fieldError} />
      <Button onClick={() => create.mutate()} loading={create.isPending}>{t("Create the demo gyms")}</Button>
      {create.isError && !fieldError && <p className="field__error" role="alert">{errorMessage(create.error)}</p>}
      {result && (
        <p className="notice" role="status">
          {t("Done: {gyms}.", { gyms: result.gyms.join(", ") })}{" "}
          {result.gymsCreated > 0 ? t("{count} new gyms, {boulders} boulders.", { count: result.gymsCreated, boulders: result.bouldersCreated }) : t("They already existed; nothing was duplicated.")}{" "}
          {result.staffEmail && t("{email} is admin of them and follows them.", { email: result.staffEmail })}{" "}
          {(result.photosReplaced ?? 0) > 0 && t("{count} boulders now have a photo instead of a drawing.", { count: result.photosReplaced ?? 0 })}
        </p>
      )}
    </section>
  );
}
