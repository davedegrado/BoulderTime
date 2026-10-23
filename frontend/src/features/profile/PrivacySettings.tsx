import { Globe, Lock } from "lucide-react";
import { useCurrentUser, useLeaderboardVisibility, useProfilePrivacy } from "@/features/users/api";
import { Toggle } from "@/features/notifications/NotificationBits";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { SUPPORT_EMAIL } from "@/lib/contact";
import { t } from "@/i18n/i18n";

/**
 * Two independent choices (ADR-061): who sees the content of the profile, and whether the person appears in the
 * leaderboards. A private profile can still appear in them (name, photo and the row's numbers only), and a public one
 * can stay out. The leaderboard switch is the one that always existed; this only puts it next to the privacy.
 */
export function PrivacySettings() {
  const me = useCurrentUser();
  const privacy = useProfilePrivacy();
  const visibility = useLeaderboardVisibility();
  const toast = useToast();
  const user = me.data;
  if (!user) return null;
  const isPrivate = user.profilePrivate ?? false;
  const choose = (next: boolean) => {
    if (next === isPrivate) return;
    privacy.mutate(next, {
      onSuccess: () => toast.success(next ? t("Your profile is private now") : t("Your profile is public now")),
      onError: (e) => toast.error(errorMessage(e)),
    });
  };

  return (
    <div className="stack">
      <div className="choice-list" role="radiogroup" aria-label={t("Who sees your profile")}>
        <button type="button" role="radio" aria-checked={!isPrivate} className="choice" disabled={privacy.isPending} onClick={() => choose(false)}>
          <Globe aria-hidden />
          <span className="choice__text">
            <span className="list__title">{t("Public profile")}</span>
            <span className="list__sub">{t("Anyone can see your statistics, history and gyms.")}</span>
          </span>
          <span className="choice__mark" aria-hidden />
        </button>
        <button type="button" role="radio" aria-checked={isPrivate} className="choice" disabled={privacy.isPending} onClick={() => choose(true)}>
          <Lock aria-hidden />
          <span className="choice__text">
            <span className="list__title">{t("Private profile")}</span>
            <span className="list__sub">{t("Others see only your name and photo. Your statistics, history and gyms stay yours.")}</span>
          </span>
          <span className="choice__mark" aria-hidden />
        </button>
      </div>
      <div className="card">
        {/* Opt-in: off for every new account until the person turns it on. */}
        <Toggle
          label={t("Show me in leaderboards and lists")}
          description={t("You appear in gyms' rankings and by name among the people who completed a boulder. Off, your sends and history stay yours, and nobody sees your name there.")}
          checked={!user.leaderboardOptOut}
          disabled={visibility.isPending}
          onChange={(shown) => visibility.mutate(!shown, {
            onSuccess: () => toast.success(shown ? t("You appear in leaderboards now") : t("You no longer appear in leaderboards")),
            onError: (e) => toast.error(errorMessage(e)),
          })} />
      </div>
      {isPrivate && !user.leaderboardOptOut && (
        <p className="field__hint">{t("Your profile is private and you appear in leaderboards: there people see your name, photo and score, nothing else.")}</p>
      )}
      {user.leaderboardExcluded && (
        <p className="notice">
          {t("You don't appear in leaderboards at the moment. If you think this is a mistake, write to {email}.", { email: SUPPORT_EMAIL })}
        </p>
      )}
    </div>
  );
}
