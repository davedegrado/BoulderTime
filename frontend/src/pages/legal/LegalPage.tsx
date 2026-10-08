import { useEffect } from "react";
import { Link, useLocation } from "react-router-dom";
import { PRIVACY_IT } from "@/pages/legal/privacy.it";
import { TERMS_IT } from "@/pages/legal/terms.it";
import { DELETION_IT } from "@/pages/legal/deletion.it";
import { t } from "@/i18n/i18n";

/**
 * The privacy notice, the terms and how to delete an account, rendered from Markdown-ish text kept in one place.
 * Reachable without signing in: people read them before creating an account, or after losing access to it.
 */
export function LegalPage({ document }: { document: "privacy" | "terms" | "deletion" }) {
  const { pathname } = useLocation();
  useEffect(() => window.scrollTo(0, 0), [pathname]);

  const text = document === "privacy" ? PRIVACY_IT : document === "terms" ? TERMS_IT : DELETION_IT;
  return (
    <div className="page legal">
      <article className="card legal__body">{render(text)}</article>
      <p className="legal__switch">
        {document === "privacy"
          ? <Link to="/termini">{t("Read the terms of use")}</Link>
          : <Link to="/privacy">{t("Read the privacy notice")}</Link>}
        {document === "deletion" && <> · <Link to="/profile">{t("Go to your profile")}</Link></>}
      </p>
    </div>
  );
}

/** A deliberately small renderer: headings, paragraphs, lists and bold. No library for three documents. */
function render(text: string) {
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
