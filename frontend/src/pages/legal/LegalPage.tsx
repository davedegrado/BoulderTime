import { useEffect } from "react";
import { Link, useLocation } from "react-router-dom";
import { PRIVACY_IT } from "@/pages/legal/privacy.it";
import { TERMS_IT } from "@/pages/legal/terms.it";
import { t } from "@/i18n/i18n";

/**
 * The privacy notice and the terms, rendered from Markdown-ish text kept in one place. Reachable without signing in,
 * because people need to read them before deciding to create an account. Account deletion has its own page
 * (DeletionPage), because it comes in two languages.
 */
export function LegalPage({ document }: { document: "privacy" | "terms" }) {
  const { pathname } = useLocation();
  useEffect(() => window.scrollTo(0, 0), [pathname]);

  const text = document === "privacy" ? PRIVACY_IT : TERMS_IT;
  return (
    <div className="page legal">
      <article className="card legal__body">{renderLegal(text)}</article>
      <p className="legal__switch">
        {document === "privacy"
          ? <Link to="/termini">{t("Read the terms of use")}</Link>
          : <Link to="/privacy">{t("Read the privacy notice")}</Link>}
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
