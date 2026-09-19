import { useConsensus, useSuggestGrade, type SystemConsensus } from "@/features/community/api";
import { useAuth } from "@/auth/AuthProvider";
import { SelectField } from "@/components/Fields";
import { ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { plural, t } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";

/** Community grade: vote distribution per system, clearly separate from the official grade. */
export function CommunityGradeSection({ boulderId }: { boulderId: string }) {
  const consensus = useConsensus(boulderId);
  const { session } = useAuth();
  if (consensus.isPending) return <LoadingState label={t("Loading community grade")} />;
  if (consensus.isError) return <ErrorState error={consensus.error} onRetry={() => consensus.refetch()} />;
  const data = consensus.data;
  if (data.systems.length === 0) return null;

  return (
    <section className="section card" aria-labelledby="community-grade-title">
      <h2 id="community-grade-title" className="section__title">{t("Community grade")}</h2>
      <p className="field__hint">{t("What climbers think. The official grade is set by the gym and doesn't change.")}</p>
      {data.systems.map((s) => <SystemBlock key={s.gradeSystemId} boulderId={boulderId} system={s} canSuggest={data.viewerCanSuggest} />)}
      {session && !data.viewerCanSuggest && <p className="field__hint">{t("Log an attempt to suggest a grade.")}</p>}
    </section>
  );
}

function SystemBlock({ boulderId, system: s, canSuggest }: { boulderId: string; system: SystemConsensus; canSuggest: boolean }) {
  const suggest = useSuggestGrade(boulderId);
  const toast = useToast();
  const max = Math.max(1, ...s.buckets.map((b) => b.votes));
  const consensus = s.buckets.find((b) => b.gradeValueId === s.consensusValueId);

  return (
    <div className="consensus">
      <div className="consensus__head">
        <p className="list__title">{dataLabel(s.systemName)}</p>
        <p className="list__sub">{s.totalVotes === 0 ? t("No votes yet") : plural(s.totalVotes, "{count} vote", "{count} votes") + (consensus ? " · " + t("consensus {grade}", { grade: consensus.label }) : "")}</p>
      </div>
      {s.buckets.length > 0 && (
        <ul className="consensus__bars" aria-label={`${s.systemName} votes`}>
          {s.buckets.map((b) => (
            <li key={b.gradeValueId} className={`consensus__row ${b.gradeValueId === s.consensusValueId ? "is-consensus" : ""}`}>
              <span className="consensus__label">{dataLabel(b.label)}</span>
              <span className="consensus__track"><span className="consensus__fill" style={{ width: `${(b.votes / max) * 100}%` }} /></span>
              <span className="consensus__count">{b.votes}</span>
              {b.gradeValueId === s.officialValueId && <span className="tag tag--dark">{t("Official")}</span>}
            </li>
          ))}
        </ul>
      )}
      {canSuggest && (
        <SelectField label={t("Your {system} grade", { system: dataLabel(s.systemName) })} value={s.viewerValueId ?? ""} disabled={suggest.isPending}
          onChange={(e) => suggest.mutate({ gradeSystemId: s.gradeSystemId, gradeValueId: e.target.value || null }, { onError: (err) => toast.error(errorMessage(err)) })}
          options={[{ value: "", label: t("No suggestion") }, ...s.scale.map((v) => ({ value: v.gradeValueId, label: dataLabel(v.label) }))]} />
      )}
    </div>
  );
}
