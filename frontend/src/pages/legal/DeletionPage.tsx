import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { activeLanguage, LANGUAGES, translate, type Language } from "@/i18n/i18n";
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
      <div className="lang-switch" role="group" aria-label={tr("Language")}>
        {LANGUAGES.map((l) => (
          // Each language named in itself, so whoever needs it recognises it.
          <button key={l.code} type="button" className="lang-switch__option" aria-pressed={language === l.code} lang={l.code}
            onClick={() => setLanguage(l.code)}>
            {l.code === "it" ? <FlagIT /> : <FlagGB />} {l.label}
          </button>
        ))}
      </div>
      <article className="card legal__body" lang={language}>{renderLegal(language === "it" ? DELETION_IT : DELETION_EN)}</article>
      <p className="legal__switch" lang={language}>
        <Link to="/privacy">{tr("Read the privacy notice")}</Link> · <Link to="/profile">{tr("Go to your profile")}</Link>
      </p>
    </div>
  );
}

function FlagIT() {
  return (
    <svg className="lang-switch__flag" viewBox="0 0 3 2" aria-hidden>
      <rect width="1" height="2" fill="#009246" />
      <rect x="1" width="1" height="2" fill="#fff" />
      <rect x="2" width="1" height="2" fill="#CE2B37" />
    </svg>
  );
}

function FlagGB() {
  return (
    <svg className="lang-switch__flag" viewBox="0 0 60 30" aria-hidden>
      <clipPath id="flag-gb-clip"><path d="M30,15 h30 v15 z v15 h-30 z h-30 v-15 z v-15 h30 z" /></clipPath>
      <path d="M0,0 v30 h60 v-30 z" fill="#012169" />
      <path d="M0,0 L60,30 M60,0 L0,30" stroke="#fff" strokeWidth="6" />
      <path d="M0,0 L60,30 M60,0 L0,30" clipPath="url(#flag-gb-clip)" stroke="#C8102E" strokeWidth="4" />
      <path d="M30,0 v30 M0,15 h60" stroke="#fff" strokeWidth="10" />
      <path d="M30,0 v30 M0,15 h60" stroke="#C8102E" strokeWidth="6" />
    </svg>
  );
}
