import { Handshake, Trophy } from "lucide-react";
import { t } from "@/i18n/i18n";

interface GymBadgesProps {
  isFoundingGym: boolean;
  isEarlyPartner: boolean;
  /** "compact" shows the short wording used on cards and tight headers. */
  size?: "compact" | "full";
}

/**
 * The two platform distinctions. They belong to the gym, never to the people who follow it.
 * A gym can hold both: the founding gym is historical (one only), early partner is the adoption programme.
 */
export function GymBadges({ isFoundingGym, isEarlyPartner, size = "full" }: GymBadgesProps) {
  if (!isFoundingGym && !isEarlyPartner) return null;
  const compact = size === "compact";
  return (
    <span className="gym-badges">
      {isFoundingGym && (
        <span className="gym-badge gym-badge--founding" title={t("Founding Gym")}>
          <Trophy aria-hidden />
          {compact ? t("Founding") : t("Founding Gym")}
        </span>
      )}
      {isEarlyPartner && (
        <span className="gym-badge gym-badge--partner" title={t("Early Partner")}>
          <Handshake aria-hidden />
          {t("Early Partner")}
        </span>
      )}
    </span>
  );
}
