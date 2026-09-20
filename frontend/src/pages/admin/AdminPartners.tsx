import { useState } from "react";
import { Link } from "react-router-dom";
import { Handshake, Trophy } from "lucide-react";
import { useAdminGyms, usePartners, usePartnerMutations, type AdminGym } from "@/features/admin/api";
import { GymBadges } from "@/features/gyms/GymBadges";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { SearchField } from "@/components/SearchField";
import { TextField } from "@/components/TextField";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { formatDate } from "@/lib/format";
import { useDebounced } from "@/lib/useDebounced";
import { t } from "@/i18n/i18n";

type Target = "founding" | "early";

/**
 * Platform distinctions. Gyms are chosen by searching for them by name — an admin should never have to know an id.
 * The founding gym is one and historical; early partners are several and hold a period.
 */
export function AdminPartners() {
  const partners = usePartners();
  const m = usePartnerMutations();
  const toast = useToast();
  const [picking, setPicking] = useState<Target | null>(null);
  const [note, setNote] = useState("");
  const onError = (e: unknown) => toast.error(errorMessage(e));

  if (partners.isPending) return <LoadingState label={t("Loading partners")} />;
  if (partners.isError) return <ErrorState error={partners.error} onRetry={() => partners.refetch()} />;

  const founding = partners.data.find((p) => p.gym.isFoundingGym);
  const early = partners.data.filter((p) => p.earlyPartner);

  function choose(gym: AdminGym) {
    if (picking === "founding") {
      m.setFounding.mutate({ gymId: gym.id, isFoundingGym: true }, {
        onSuccess: () => { toast.success(t("{name} is now the founding gym", { name: gym.name })); setPicking(null); },
        onError,
      });
    } else {
      m.startEarlyPartner.mutate({ gymId: gym.id, note: note.trim() || undefined }, {
        onSuccess: () => { toast.success(t("{name} is now an early partner", { name: gym.name })); setPicking(null); setNote(""); },
        onError,
      });
    }
  }

  return (
    <div className="stack">
      <section className="card stack" aria-labelledby="founding-title">
        <h2 id="founding-title" className="section__title"><Trophy aria-hidden className="title-icon" /> {t("Founding Gym")}</h2>
        <p className="field__hint">{t("The first gym that launched BoulderTime with us. There can only be one.")}</p>
        {founding ? (
          <div className="list__row list__row--stack">
            <div className="list__main">
              <Link className="list__title" to={`/gyms/${founding.gym.slug}`}>{founding.gym.name}</Link>
              <p className="list__sub">{founding.gym.city}</p>
            </div>
            <ConfirmButton confirmLabel={t("Remove?")} loading={m.setFounding.isPending}
              onConfirm={() => m.setFounding.mutate({ gymId: founding.gym.id, isFoundingGym: false }, { onSuccess: () => toast.success(t("Founding gym removed")), onError })}>
              {t("Remove")}
            </ConfirmButton>
          </div>
        ) : (
          <p className="list__sub">{t("No founding gym yet.")}</p>
        )}
        {picking === "founding"
          ? <GymPicker busy={m.setFounding.isPending} onPick={choose} onCancel={() => setPicking(null)} actionLabel={t("Set as founding gym")} />
          : !founding && <Button onClick={() => setPicking("founding")}>{t("Choose the founding gym")}</Button>}
      </section>

      <section className="card stack" aria-labelledby="early-title">
        <h2 id="early-title" className="section__title"><Handshake aria-hidden className="title-icon" /> {t("Early Partner")}</h2>
        <p className="field__hint">{t("Gyms in the early-adopter programme. Several gyms can be partners at the same time.")}</p>
        {early.length === 0 ? (
          <EmptyState icon={<Handshake />} title={t("No early partners yet")} />
        ) : (
          <ul className="list">
            {early.map((p) => (
              <li key={p.gym.id} className="list__row list__row--stack">
                <div className="list__main">
                  <Link className="list__title" to={`/gyms/${p.gym.slug}`}>{p.gym.name}</Link>
                  <p className="list__sub">
                    {t("Partner since {date}", { date: formatDate(p.earlyPartner!.startedAt) })}
                    {p.earlyPartner!.note && ` · ${p.earlyPartner!.note}`}
                  </p>
                  <GymBadges isFoundingGym={p.gym.isFoundingGym} isEarlyPartner size="compact" />
                </div>
                <ConfirmButton confirmLabel={t("End?")} loading={m.endEarlyPartner.isPending}
                  onConfirm={() => m.endEarlyPartner.mutate(p.gym.id, { onSuccess: () => toast.success(t("Partnership ended")), onError })}>
                  {t("End partnership")}
                </ConfirmButton>
              </li>
            ))}
          </ul>
        )}
        {picking === "early" ? (
          <>
            <TextField label={t("Note")} value={note} onChange={(e) => setNote(e.target.value)} maxLength={300} hint={t("Optional")} />
            <GymPicker busy={m.startEarlyPartner.isPending} onPick={choose} onCancel={() => setPicking(null)} actionLabel={t("Add as early partner")} />
          </>
        ) : (
          <Button variant="secondary" onClick={() => setPicking("early")}>{t("Add early partner")}</Button>
        )}
      </section>
    </div>
  );
}

/** Search a gym by name or city and pick it — no identifiers to copy around. */
function GymPicker({ onPick, onCancel, actionLabel, busy }: { onPick: (gym: AdminGym) => void; onCancel: () => void; actionLabel: string; busy: boolean }) {
  const [text, setText] = useState("");
  const query = useDebounced(text.trim(), 300);
  const gyms = useAdminGyms(query, "");
  const results = gyms.data?.items ?? [];

  return (
    <div className="stack">
      <SearchField label={t("Search gyms")} placeholder={t("Gym name or city")} value={text} onChange={setText} />
      {gyms.isPending ? <LoadingState label={t("Loading gyms")} />
        : gyms.isError ? <ErrorState error={gyms.error} onRetry={() => gyms.refetch()} />
        : results.length === 0 ? <p className="list__sub">{t("No gyms found")}</p>
        : (
          <ul className="list">
            {results.slice(0, 8).map((gym) => (
              <li key={gym.id} className="list__row list__row--stack">
                <div className="list__main">
                  <p className="list__title">{gym.name}</p>
                  <p className="list__sub">{gym.city}</p>
                </div>
                <Button loading={busy} onClick={() => onPick(gym)}>{actionLabel}</Button>
              </li>
            ))}
          </ul>
        )}
      <Button variant="ghost" onClick={onCancel}>{t("Cancel")}</Button>
    </div>
  );
}
