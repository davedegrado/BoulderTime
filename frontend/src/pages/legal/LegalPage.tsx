import { useEffect, useState } from "react";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { ArrowLeft } from "lucide-react";
import { legalText } from "@/pages/legal/texts";
import { LanguageSwitch } from "@/pages/legal/LanguageSwitch";
import { activeLanguage, translate, type Language } from "@/i18n/i18n";

/**
 * The privacy notice and the terms, rendered from Markdown-ish text kept in one place. Reachable without signing in,
 * because people need to read them before deciding to create an account. In the app's language, or the one named by
 * ?lang=, and the reader can switch, as on the account deletion page: Italian is the original, English a courtesy
 * translation that says the Italian prevails.
 */
export function LegalPage({ document }: { document: "privacy" | "terms" }) {
  const { pathname, key } = useLocation();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const asked = params.get("lang");
  const [language, setLanguage] = useState<Language>(asked === "it" || asked === "en" ? asked : activeLanguage());
  useEffect(() => window.scrollTo(0, 0), [pathname]);
  // The page's language, for screen readers and hyphenation, while it is shown; the app's own afterwards.
  useEffect(() => {
    const before = window.document.documentElement.lang;
    window.document.documentElement.lang = language;
    return () => { window.document.documentElement.lang = before; };
  }, [language]);
  // The page sits outside the app's navigation and the iPhone app has no back button: say how to get back.
  const cameFromApp = key !== "default";
  const tr = (text: string) => translate(language, text);

  return (
    <div className="page legal">
      {cameFromApp
        ? <button type="button" className="legal__back" onClick={() => navigate(-1)}><ArrowLeft aria-hidden /> {tr("Back")}</button>
        : <Link className="legal__back" to="/"><ArrowLeft aria-hidden /> {tr("BoulderTime home")}</Link>}
      <LanguageSwitch language={language} onChange={setLanguage} />
      <article className="card legal__body" lang={language}>{renderLegal(legalText(document, language))}</article>
      <p className="legal__switch">
        {document === "privacy"
          ? <Link to={`/termini${asked ? `?lang=${language}` : ""}`}>{tr("Read the terms of use")}</Link>
          : <Link to={`/privacy${asked ? `?lang=${language}` : ""}`}>{tr("Read the privacy notice")}</Link>}
      </p>
    </div>
  );
}

/** A deliberately small renderer: headings, paragraphs, lists and bold. No library for a handful of documents. */
export function renderLegal(text: string) {
  return text.split("\n\n").map((block, index) => {
    const trimmed = block.trim();
    if (trimmed.startsWith("## ")) return <h2 key={index} className="legal__h2">{trimmed.slice(3)}</h2>;
    if (trimmed.startsWith("# ")) return <h1 key={index} className="legal__h1">{trimmed.slice(2)}</h1>;
    if (trimmed.startsWith("- ")) {
      return (
        <ul key={index} className="legal__list">
          {trimmed.split("\n- ").map((item, i) => <li key={i}>{inline(item.replace(/^- /, ""))}</li>)}
        </ul>
      );
    }
    return <p key={index} className="legal__p">{inline(trimmed)}</p>;
  });
}

/** **bold** is the only inline mark the documents use. */
function inline(text: string) {
  return text.split(/(\*\*[^*]+\*\*)/g).map((part, i) =>
    part.startsWith("**") && part.endsWith("**")
      ? <strong key={i}>{part.slice(2, -2)}</strong>
      : <span key={i}>{part.replace(/\n/g, " ")}</span>);
}
