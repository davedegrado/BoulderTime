import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { activeLanguage, translate, type Language } from "@/i18n/i18n";
import { LanguageSwitch } from "@/pages/legal/LanguageSwitch";
import { renderLegal } from "@/pages/legal/LegalPage";
import { DELETION_EN, DELETION_IT } from "@/pages/legal/deletion";

/**
 * How to delete an account, at /delete-account, readable without signing in (the page Google Play asks for). It
 * opens in the app's language, or in the one named by ?lang=, and the reader can switch: the choice stays on this
 * page and doesn't change the app's language, which for a signed-in person follows their profile.
 */
export function DeletionPage() {
  const [params] = useSearchParams();
  const asked = params.get("lang");
  const [language, setLanguage] = useState<Language>(asked === "it" || asked === "en" ? asked : activeLanguage());
  useEffect(() => window.scrollTo(0, 0), []);
  useEffect(() => { document.documentElement.lang = language; }, [language]);
  const tr = (text: string) => translate(language, text);

  return (
    <div className="page legal">
      <LanguageSwitch language={language} onChange={setLanguage} />
      <article className="card legal__body" lang={language}>{renderLegal(language === "it" ? DELETION_IT : DELETION_EN)}</article>
      <p className="legal__switch" lang={language}>
        <Link to={`/privacy?lang=${language}`}>{tr("Read the privacy notice")}</Link> · <Link to="/profile">{tr("Go to your profile")}</Link>
      </p>
    </div>
  );
}
