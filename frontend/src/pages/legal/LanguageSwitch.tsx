import { LANGUAGES, translate, type Language } from "@/i18n/i18n";

/**
 * Italian or English, for the pages people read before or without signing in (privacy, terms, account deletion). The
 * choice stays on the page and doesn't change the app's language, which for a signed-in person follows their profile.
 */
export function LanguageSwitch({ language, onChange }: { language: Language; onChange: (l: Language) => void }) {
  return (
    <div className="lang-switch" role="group" aria-label={translate(language, "Language")}>
      {LANGUAGES.map((l) => (
        // Each language named in itself, so whoever needs it recognises it.
        <button key={l.code} type="button" className="lang-switch__option" aria-pressed={language === l.code} lang={l.code}
          onClick={() => onChange(l.code)}>
          {l.code === "it" ? <FlagIT /> : <FlagGB />} {l.label}
        </button>
      ))}
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
