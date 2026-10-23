import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { ArrowLeft } from "lucide-react";
import { Logo } from "@/components/Logo";
import { useGoBack } from "@/lib/navigation";
import { t } from "@/i18n/i18n";

/**
 * The compact header of a gym's or a person's page, in place of the logo bar: back on the left, a smaller logo, the
 * page's "…" menu on the right — one row. Back returns where the page was opened from (Explore, a leaderboard, a
 * notification…) and goes Home only when it was opened directly (useGoBack).
 */
export function DetailHeader({ menu }: { menu?: ReactNode }) {
  const goBack = useGoBack();
  return (
    <div className="detail-header">
      <button type="button" className="icon-btn detail-header__back" aria-label={t("Back")} onClick={() => goBack("/")}>
        <ArrowLeft aria-hidden />
      </button>
      <Link to="/" className="detail-header__logo" aria-label={t("BoulderTime home")}><Logo variant="horizontal" height={22} /></Link>
      <div className="detail-header__end">{menu}</div>
    </div>
  );
}
