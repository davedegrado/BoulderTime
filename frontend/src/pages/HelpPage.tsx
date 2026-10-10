import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { ChevronDown, Mail } from "lucide-react";
import { SearchField } from "@/components/SearchField";
import { faqGuide, searchFaq, type FaqAudience, type FaqSection } from "@/features/help/faq";
import { renderLegal } from "@/pages/legal/LegalPage";
import { SUPPORT_EMAIL } from "@/lib/contact";
import { t } from "@/i18n/i18n";

/**
 * How things are done, as questions and answers: one guide for climbers and one for gym staff, with a search across
 * both. Readable without signing in. `#staff` in the address opens the staff guide, which is where the staff area
 * links to.
 */
export function HelpPage() {
  const { hash } = useLocation();
  const navigate = useNavigate();
  const guide = faqGuide();
  const [audience, setAudience] = useState<FaqAudience>(hash === "#staff" ? "staff" : "climbers");
  const [query, setQuery] = useState("");

  useEffect(() => { if (hash === "#staff") setAudience("staff"); }, [hash]);

  function choose(next: FaqAudience) {
    setAudience(next);
    navigate({ hash: next === "staff" ? "staff" : "" }, { replace: true });
  }

  // A search looks in both guides: people don't always know which side their question is on.
  const searching = query.trim().length > 1;
  const results = searching
    ? [...searchFaq(guide.climbers, query), ...searchFaq(guide.staff, query)]
    : guide[audience];

  return (
    <div className="page page--narrow help">
      <header className="page__header">
        <h1 className="page__title">{t("Help and FAQ")}</h1>
        <p className="page__subtitle">{t("How to do things in BoulderTime, step by step.")}</p>
      </header>

      <SearchField label={t("Search the guide")} placeholder={t("e.g. notifications, grade, sector")} value={query} onChange={setQuery} />

      {!searching && (
        <div className="chips" role="radiogroup" aria-label={t("Guide for")}>
          <button type="button" role="radio" aria-checked={audience === "climbers"} className="chip" onClick={() => choose("climbers")}>{t("Climbers")}</button>
          <button type="button" role="radio" aria-checked={audience === "staff"} className="chip" onClick={() => choose("staff")}>{t("Gym staff")}</button>
        </div>
      )}

      {results.length === 0
        ? <p className="help__empty">{t("Nothing found. Try other words, or write to us.")}</p>
        : results.map((section) => <HelpSection key={`${section.id}-${section.title}`} section={section} open={searching} />)}

      <p className="help__contact">
        <Mail aria-hidden />
        <span>{t("Didn't find the answer?")} <a href={`mailto:${SUPPORT_EMAIL}`}>{SUPPORT_EMAIL}</a></span>
      </p>
    </div>
  );
}

function HelpSection({ section, open }: { section: FaqSection; open: boolean }) {
  return (
    <section className="help__section" aria-labelledby={`help-${section.id}`}>
      <h2 id={`help-${section.id}`} className="section__title">{section.title}</h2>
      <div className="disclosures">
        {section.items.map((item) => (
          // Search results arrive open: the answer is what was looked for.
          <details key={item.q} className="disclosure" open={open || undefined}>
            <summary className="disclosure__summary">
              <span className="disclosure__text"><span className="list__title">{item.q}</span></span>
              <ChevronDown className="disclosure__chevron" aria-hidden />
            </summary>
            <div className="disclosure__body help__answer">{renderLegal(item.a)}</div>
          </details>
        ))}
      </div>
    </section>
  );
}
