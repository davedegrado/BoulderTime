import { useState } from "react";
import { Link } from "react-router-dom";
import { Handshake, Trophy } from "lucide-react";
import { usePartners, usePartnerMutations } from "@/features/admin/api";
import { GymBadges } from "@/features/gyms/GymBadges";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { TextField } from "@/components/TextField";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { formatDate } from "@/lib/format";
import { t } from "@/i18n/i18n";

/**
 * Platform distinctions. The founding gym is one and historical; early partners are several and have a period.
 * Both live here because only BoulderTime administrators may change them.
 */
export function AdminPartners() {
  const partners = usePartners();
  const m = usePartnerMutations();
  const toast = useToast();
  const [gymId, setGymId] = useState("");
  const [note, setNote] = useState("");
  const onError = (e: unknown) => toast.error(errorMessage(e));

  if (partners.isPending) return <LoadingState label={t("Loading partners")} />;
  if (partners.isError) return <ErrorState error={partners.error} onRetry={() => partners.refetch()} />;

  const founding = partners.data.find((p) => p.gym.isFoundingGym);
  const early = partners.data.filter((p) => p.earlyPartner);

  return (
    <div className="stack">
      <section className="card stack" aria-labelledby="founding-title">
        <h2 id="founding-title" className="section__title"><Trophy aria-hidden className="title-icon" /> {t("Founding Gym")}</h2>
        <p className="field__hint">{t("The first gym that launched BoulderTime with us. There can only be one.")}</p>
        {founding ? (
          <div className="list__row">
            <div className="list__main">
              <Link className="list__title" to={`/gyms/${founding.gym.slug}`}>{founding.gym.name}</Link>
              <p className="list__sub">{founding.gym.city}</p>
            </div>
            <ConfirmButton confirmLabel={t("Remove?")} loading={m.setFounding.isPending}
              onConfirm={() => m.setFounding.mutate({ gymId: founding.gym.id, isFoundingGym: false }, { onSuccess: () => toast.success(t("Founding gym removed")), onError })}>
              {t("Remove")}
            </ConfirmButton>
          </div>
        ) : <p className="list__sub">{t("No founding gym yet.")}</p>}
        <div className="inline-form">
          <TextField label={t("Gym ID")} value={gymId} onChange={(e) => setGymId(e.target.value)} hint={t("Copy it from the gyms list.")} />
          <Button disabled={!gymId.trim()} loading={m.setFounding.isPending}
            onClick={() => m.setFounding.mutate({ gymId: gymId.trim(), isFoundingGym: true }, { onSuccess: () => { toast.success(t("Founding gym set")); setGymId(""); }, onError })}>
            {t("Set as founding gym")}
          </Button>
        </div>
      </section>

      <section className="card stack" aria-labelledby="early-title">
        <h2 id="early-title" className="section__title"><Handshake aria-hidden className="title-icon" /> {t("Early Partner")}</h2>
        <p className="field__hint">{t("Gyms in the early-adopter programme. Several gyms can be partners at the same time.")}</p>
        {early.length === 0 ? (
          <EmptyState icon={<Handshake />} title={t("No early partners yet")} />
        ) : (
          <ul className="list">
            {early.map((p) => (
              <li key={p.gym.id} className="list__row">
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
        <div className="inline-form">
          <TextField label={t("Gym ID")} value={gymId} onChange={(e) => setGymId(e.target.value)} />
          <TextField label={t("Note")} value={note} onChange={(e) => setNote(e.target.value)} maxLength={300} hint={t("Optional")} />
          <Button disabled={!gymId.trim()} loading={m.startEarlyPartner.isPending}
            onClick={() => m.startEarlyPartner.mutate({ gymId: gymId.trim(), note: note.trim() || undefined }, { onSuccess: () => { toast.success(t("Early partner added")); setGymId(""); setNote(""); }, onError })}>
            {t("Add early partner")}
          </Button>
        </div>
      </section>
    </div>
  );
}
