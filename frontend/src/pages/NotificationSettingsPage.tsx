import { Link } from "react-router-dom";
import { ArrowLeft, X } from "lucide-react";
import { useFollowSetting, useMyFollows, useNotificationSettings, useUpdateNotificationSettings, type NotificationSettings } from "@/features/notifications/api";
import { Toggle } from "@/features/notifications/NotificationBits";
import { ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { disablePush, enablePush, needsInstallFirst, pushStatus, pushSupportedHere } from "@/features/notifications/push";
import { isNativeApp } from "@/lib/native";

const CATEGORIES: { key: keyof NotificationSettings; label: string; description: string }[] = [
  { key: "gymUpdates", label: "Gym updates", description: "New boulders, announcements, events and schedule changes at gyms you follow." },
  { key: "sectorUpdates", label: "Sector updates", description: "New boulders in sectors you follow, and retraces of sectors you follow or with boulders you follow." },
  { key: "boulderUpdates", label: "Boulder updates", description: "Changes, new official beta and comments on boulders you follow or are projecting." },
  { key: "myContent", label: "Your videos and reports", description: "When your videos are reviewed or your reports are handled." },
];

/**
 * Notifications with the app closed. Kept above the per-category switches because it answers a different question:
 * those decide what you are told, this decides whether your phone buzzes at all.
 */
function PhoneNotifications() {
  const status = useQuery({ queryKey: ["push", "status"], queryFn: pushStatus, enabled: pushSupportedHere() });
  const toast = useToast();
  const [busy, setBusy] = useState(false);

  if (isNativeApp()) {
    return (
      <section className="card stack" aria-labelledby="phone-title">
        <h2 id="phone-title" className="section__title">{t("On your phone")}</h2>
        <p className="field__hint">{t("Notifications with the app closed are coming in the next update of the app. Until then you'll find everything here in Notifications.")}</p>
      </section>
    );
  }
  if (!pushSupportedHere() || status.data?.available === false) return null;

  async function toggle(on: boolean) {
    setBusy(true);
    try {
      if (!on) {
        await disablePush();
        toast.success(t("Phone notifications off"));
      } else {
        const outcome = await enablePush(status.data?.publicKey ?? "");
        if (outcome === "denied") toast.error(t("Your phone refused notifications. Turn them on for BoulderTime in the phone's settings."));
        else if (outcome === "unsupported") toast.error(t("This browser can't do notifications."));
        else if (outcome === "misconfigured") toast.error(t("Phone notifications aren't set up correctly on the server."));
        else toast.success(t("Phone notifications on"));
      }
      await status.refetch();
    } catch (e) {
      toast.error(errorMessage(e));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card stack" aria-labelledby="phone-title">
      <h2 id="phone-title" className="section__title">{t("On your phone")}</h2>
      {needsInstallFirst() ? (
        <p className="notice">{t("On iPhone, notifications arrive only once BoulderTime is on your home screen: tap Share, then “Add to Home Screen”, and open it from there.")}</p>
      ) : (
        <Toggle
          label={t("Notifications on this phone")}
          description={t("New boulders, retraces and gym updates reach you even with the app closed. What you receive follows the settings below.")}
          checked={status.data?.subscribedOnThisDevice ?? false}
          disabled={busy || status.isPending}
          onChange={toggle} />
      )}
    </section>
  );
}

export function NotificationSettingsPage() {
  const settings = useNotificationSettings();
  const update = useUpdateNotificationSettings();
  const follows = useMyFollows();
  const follow = useFollowSetting();
  const toast = useToast();
  const onError = (e: unknown) => toast.error(errorMessage(e));

  return (
    <div className="page page--narrow">
      <header className="editor__head">
        <Link to="/notifications" className="manage-head__back" aria-label={t("Back to notifications")}><ArrowLeft aria-hidden /></Link>
        <h1 className="page__title">{t("Notification settings")}</h1>
      </header>

      <PhoneNotifications />

      <section className="card stack" aria-label={t("Categories")}>
        {settings.isPending ? <LoadingState /> : settings.isError ? <ErrorState error={settings.error} onRetry={() => settings.refetch()} />
          : CATEGORIES.map((c) => (
            <Toggle key={c.key} label={t(c.label)} description={t(c.description)} checked={settings.data[c.key]}
              onChange={(v) => update.mutate({ [c.key]: v }, { onError })} />
          ))}
      </section>

      <section className="section" aria-labelledby="following-title">
        <h2 id="following-title" className="section__title">{t("What you follow")}</h2>
        <p className="field__hint">{t("Turn off notifications for one thing without unfollowing it.")}</p>
        {follows.isPending ? <LoadingState /> : follows.isError ? <ErrorState error={follows.error} onRetry={() => follows.refetch()} /> : (
          <div className="stack">
            {[
              { title: t("Gyms"), kind: "gyms" as const, rows: follows.data.gyms.map((g) => ({ id: g.gym.id, label: g.gym.name, sub: g.isFavorite ? t("Favourite") : g.gym.city, on: g.notificationsEnabled, to: `/gyms/${g.gym.slug}` })) },
              { title: t("Sectors"), kind: "sectors" as const, rows: follows.data.sectors.map((s) => ({ id: s.sectorId, label: s.sectorName, sub: s.gymName, on: s.notificationsEnabled, to: `/gyms/${s.gymSlug}` })) },
              { title: t("Boulders"), kind: "boulders" as const, rows: follows.data.boulders.map((b) => ({ id: b.boulderId, label: `${b.sectorName}`, sub: b.gymName, on: b.notificationsEnabled, to: `/boulders/${b.boulderId}` })) },
            ].map((group) => (
              <div key={group.kind} className="stack">
                <h3 className="section__meta">{group.title}</h3>
                {group.rows.length === 0 ? <p className="list__sub">{t("You aren't following any {what}.", { what: group.title.toLowerCase() })}</p> : (
                  <ul className="list">
                    {group.rows.map((r) => (
                      <li key={r.id} className="list__row">
                        <Link to={r.to} className="list__main list__link-plain">
                          <span className="list__title">{r.label}</span>
                          <span className="list__sub">{r.sub}</span>
                        </Link>
                        <button type="button" role="switch" aria-checked={r.on} aria-label={t("Notifications for {name}", { name: r.label })}
                          className={`switch ${r.on ? "is-on" : ""}`} onClick={() => follow.mutate({ kind: group.kind, id: r.id, enabled: !r.on }, { onError })}>
                          <span className="switch__thumb" />
                        </button>
                        <button type="button" className="icon-btn" aria-label={t("Unfollow {name}", { name: r.label })} onClick={() => follow.mutate({ kind: group.kind, id: r.id, enabled: null }, { onError })}>
                          <X aria-hidden />
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
