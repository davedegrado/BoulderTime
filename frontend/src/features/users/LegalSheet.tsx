import { useEffect, useRef } from "react";
import { X } from "lucide-react";
import { renderLegal } from "@/pages/legal/LegalPage";
import { legalText } from "@/pages/legal/texts";
import { activeLanguage, t } from "@/i18n/i18n";

export type LegalDocument = "terms" | "privacy";

/**
 * The terms or the privacy notice over the form that links to them, so reading them doesn't throw away what was
 * already typed — and inside the store apps there is no second tab to open them in.
 */
export function LegalSheet({ document, onClose }: { document: LegalDocument; onClose: () => void }) {
  const close = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    close.current?.focus();
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="sheet-backdrop" onClick={onClose}>
      <div className="sheet legal-sheet" role="dialog" aria-modal="true"
        aria-label={document === "terms" ? t("Terms of use") : t("Privacy notice")} onClick={(e) => e.stopPropagation()}>
        <div className="legal-sheet__top">
          <button ref={close} type="button" className="icon-btn" onClick={onClose} aria-label={t("Close")}><X aria-hidden /></button>
        </div>
        {/* In the app's language: English is a courtesy translation that says the Italian prevails. */}
        <div className="legal-sheet__body legal__body" lang={activeLanguage()}>{renderLegal(legalText(document, activeLanguage()))}</div>
      </div>
    </div>
  );
}
