import { useRef, useState } from "react";
import { ChevronDown } from "lucide-react";
import { useConsensus, useSuggestGrade, type ConsensusBucket, type SystemConsensus } from "@/features/community/api";
import { useAuth } from "@/auth/AuthProvider";
import { SelectField } from "@/components/Fields";
import { ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { plural, t } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";

/**
 * Community grade: what climbers think, side by side with the gym's grade and never mixed with it. The rule for the
 * "most voted" grade is the server's and doesn't change here.
 *
 * One card, one grading system at a time, swiped sideways. The server sends them in order: the system the boulder is
 * graded in first, then the gym's other systems, then the standard scales the gym doesn't use (anyone may still vote
 * in those). Always in view for each: the official grade, the most voted one ("9 of 16") and the climber's own vote;
 * the distribution opens on request.
 */
export function CommunityGradeSection({ boulderId }: { boulderId: string }) {
  const consensus = useConsensus(boulderId);
  const { session } = useAuth();
  const track = useRef<HTMLDivElement>(null);
  const [active, setActive] = useState(0);
  if (consensus.isPending) return <LoadingState label={t("Loading community grade")} />;
  if (consensus.isError) return <ErrorState error={consensus.error} onRetry={() => consensus.refetch()} />;
  const data = consensus.data;
  if (data.systems.length === 0) return null;
  const several = data.systems.length > 1;

  function show(index: number) {
    setActive(index);
    const el = track.current;
    if (el) el.scrollTo?.({ left: index * el.clientWidth, behavior: "smooth" });
  }
  function onScroll() {
    const el = track.current;
    if (el && el.clientWidth) setActive(Math.round(el.scrollLeft / el.clientWidth));
  }

  return (
    <section className="section card community-grade" aria-labelledby="community-grade-title">
      <h2 id="community-grade-title" className="section__title">{t("Community grade")}</h2>
      <p className="field__hint">{t("What climbers think. The official grade is set by the gym and doesn't change.")}</p>
      {several && (
        <div className="grade-tabs" role="tablist" aria-label={t("Grading systems")}>
          {data.systems.map((s, i) => (
            <button key={s.gradeSystemId} type="button" role="tab" id={`grade-tab-${s.gradeSystemId}`} aria-controls={`grade-panel-${s.gradeSystemId}`}
              aria-selected={i === active} className="grade-tabs__tab" onClick={() => show(i)}>
              {dataLabel(s.systemName)}
            </button>
          ))}
        </div>
      )}
      <div className={several ? "grade-carousel" : undefined} ref={track} onScroll={several ? onScroll : undefined}>
        {data.systems.map((s) => (
          <div key={s.gradeSystemId} className="grade-carousel__slide" {...(several ? { role: "tabpanel", id: `grade-panel-${s.gradeSystemId}`, "aria-labelledby": `grade-tab-${s.gradeSystemId}` } : {})}>
            <SystemBlock boulderId={boulderId} system={s} canSuggest={data.viewerCanSuggest} showName={!several} />
          </div>
        ))}
      </div>
      {session && !data.viewerCanSuggest && <p className="field__hint">{t("Log an attempt to suggest a grade.")}</p>}
    </section>
  );
}

/** The voted grades plus the official one, in the order of the scale. */
function rowsOf(s: SystemConsensus): ConsensusBucket[] {
  const rows = [...s.buckets];
  if (s.officialValueId && !rows.some((b) => b.gradeValueId === s.officialValueId)) {
    const official = s.scale.find((v) => v.gradeValueId === s.officialValueId);
    if (official) rows.push({ ...official, votes: 0 });
  }
  return rows.sort((a, b) => a.rank - b.rank);
}

function Swatch({ bucket }: { bucket: ConsensusBucket }) {
  return bucket.colorHex ? <span className="consensus__swatch" style={{ background: bucket.colorHex }} aria-hidden /> : null;
}

function SystemBlock({ boulderId, system: s, canSuggest, showName }: { boulderId: string; system: SystemConsensus; canSuggest: boolean; showName: boolean }) {
  const suggest = useSuggestGrade(boulderId);
  const toast = useToast();
  const rows = rowsOf(s);
  const max = Math.max(1, ...rows.map((b) => b.votes));
  const official = rows.find((b) => b.gradeValueId === s.officialValueId);
  const top = s.totalVotes > 0 ? rows.find((b) => b.gradeValueId === s.consensusValueId) : undefined;
  const percent = (votes: number) => (s.totalVotes ? Math.round((votes / s.totalVotes) * 100) : 0);
  const usedByGym = s.usedByGym !== false;

  return (
    <div className="consensus">
      {/* With several systems the tab names this one, and "9 of 16" already says how many voted. */}
      {showName && (
        <div className="consensus__head">
          <p className="list__title">{dataLabel(s.systemName)}</p>
          <p className="list__sub">{s.totalVotes === 0 ? t("No votes yet") : plural(s.totalVotes, "{count} vote", "{count} votes")}</p>
        </div>
      )}

      <dl className="consensus__summary">
        <div className="consensus__fact">
          <dt>{t("Official")}</dt>
          <dd>{official
            ? <><Swatch bucket={official} />{dataLabel(official.label)}</>
            : <span className="consensus__share">{usedByGym ? "—" : t("Not used at this gym")}</span>}</dd>
        </div>
        <div className="consensus__fact">
          <dt>{t("Most voted")}</dt>
          <dd>
            {top
              ? <><Swatch bucket={top} />{dataLabel(top.label)} <span className="consensus__share">{t("{votes} of {total}", { votes: top.votes, total: s.totalVotes })}</span></>
              : <span className="consensus__share">—</span>}
          </dd>
        </div>
      </dl>

      {/* The detail is there for whoever wants it, folded away: the two answers above are what most people read. */}
      {rows.length > 0 && s.totalVotes > 0 && (
        <details className="consensus__details">
          <summary className="consensus__toggle">
            {t("See the votes")} <ChevronDown aria-hidden />
          </summary>
          <ul className="consensus__bars" aria-label={t("Votes for {system}", { system: dataLabel(s.systemName) })}>
            {rows.map((b) => (
              <li key={b.gradeValueId} className={`consensus__row ${b.gradeValueId === s.consensusValueId ? "is-consensus" : ""}`}
                aria-label={t("{grade}: {votes} of {total}", { grade: dataLabel(b.label), votes: b.votes, total: s.totalVotes })}>
                <span className="consensus__label"><Swatch bucket={b} />{dataLabel(b.label)}</span>
                <span className="consensus__track"><span className="consensus__fill" style={{ width: `${(b.votes / max) * 100}%` }} /></span>
                <span className="consensus__count">{b.votes} <small>{percent(b.votes)}%</small></span>
              </li>
            ))}
          </ul>
        </details>
      )}
      {canSuggest && (
        <SelectField label={t("Your {system} grade", { system: dataLabel(s.systemName) })} value={s.viewerValueId ?? ""} disabled={suggest.isPending}
          onChange={(e) => suggest.mutate({ gradeSystemId: s.gradeSystemId, gradeValueId: e.target.value || null }, { onError: (err) => toast.error(errorMessage(err)) })}
          options={[{ value: "", label: t("No suggestion") }, ...s.scale.map((v) => ({ value: v.gradeValueId, label: dataLabel(v.label) }))]} />
      )}
    </div>
  );
}
