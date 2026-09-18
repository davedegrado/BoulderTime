import { useState } from "react";
import { Check, Video, X } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useModerationSummary, usePendingVideos, useReviewVideo, type ModerationVideo } from "@/features/community/api";
import { useCurrentUser } from "@/features/users/api";
import { ReportsList } from "@/features/community/ReportsList";
import { BoulderCard } from "@/features/boulders/BoulderCard";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

export function ManageModeration() {
  const { gym } = useManagedGym();
  const summary = useModerationSummary(gym.id);
  const [tab, setTab] = useState<"videos" | "reports">("videos");

  return (
    <div className="stack">
      <div className="chips" role="radiogroup" aria-label={t("Moderation queue")}>
        <button role="radio" aria-checked={tab === "videos"} className="chip" onClick={() => setTab("videos")}>
          Videos{summary.data?.pendingVideos ? ` (${summary.data.pendingVideos})` : ""}
        </button>
        <button role="radio" aria-checked={tab === "reports"} className="chip" onClick={() => setTab("reports")}>
          Reports{summary.data?.pendingReports ? ` (${summary.data.pendingReports})` : ""}
        </button>
      </div>
      {tab === "videos" ? <VideoQueue gymId={gym.id} /> : <ReportsList scope={gym.id} />}
    </div>
  );
}

function VideoQueue({ gymId }: { gymId: string }) {
  const queue = usePendingVideos(gymId);
  if (queue.isPending) return <LoadingState label={t(t("Loading videos"))} />;
  if (queue.isError) return <ErrorState error={queue.error} onRetry={() => queue.refetch()} />;
  if (queue.data.length === 0) return <EmptyState icon={<Video />} title={t(t("No videos waiting"))} body={t(t("New community videos show up here for approval."))} />;
  return <div className="stack">{queue.data.map((item) => <PendingVideo key={item.video.id} gymId={gymId} item={item} />)}</div>;
}

function PendingVideo({ gymId, item }: { gymId: string; item: ModerationVideo }) {
  const review = useReviewVideo(gymId);
  const me = useCurrentUser();
  const toast = useToast();
  const [rejecting, setRejecting] = useState(false);
  const [reason, setReason] = useState("");
  const own = me.data?.id === item.video.author.userId;
  const reasonError = review.error instanceof ApiError ? review.error.fieldError("reason") : undefined;

  return (
    <article className="card moderation-video">
      <video className="player" src={item.video.videoUrl} poster={item.video.thumbnailUrl ?? undefined} controls playsInline preload="metadata" aria-label={`Video by ${item.video.author.displayName}`} />
      <div className="moderation-video__side">
        <div className="moderation-video__boulder"><BoulderCard boulder={item.boulder} /></div>
        <div className="stack">
          <p className="list__title">@{item.video.author.displayName}</p>
          {item.video.caption && <p className="prose">{item.video.caption}</p>}
          {own ? (
            <p className="field__hint">{t("This is your video — another staff member has to review it.")}</p>
          ) : rejecting ? (
            <form className="stack" onSubmit={(e) => { e.preventDefault(); review.mutate({ id: item.video.id, approve: false, reason }, { onSuccess: () => toast.success("Video rejected"), onError: (er) => { if (!(er instanceof ApiError && er.isValidation)) toast.error(errorMessage(er)); } }); }}>
              <TextField label={t(t("Reason (shown to the climber)"))} value={reason} onChange={(e) => setReason(e.target.value)} error={reasonError} maxLength={300} autoFocus />
              <div className="form__actions">
                <Button type="submit" variant="danger" loading={review.isPending}>{t(t("Reject"))}</Button>
                <Button variant="ghost" onClick={() => setRejecting(false)}>{t(t("Cancel"))}</Button>
              </div>
            </form>
          ) : (
            <div className="form__actions">
              <Button icon={<Check aria-hidden />} loading={review.isPending}
                onClick={() => review.mutate({ id: item.video.id, approve: true }, { onSuccess: () => toast.success(t("Video approved")), onError: (e) => toast.error(errorMessage(e)) })}>
                Approve
              </Button>
              <Button variant="secondary" icon={<X aria-hidden />} onClick={() => setRejecting(true)}>{t(t("Reject"))}</Button>
            </div>
          )}
        </div>
      </div>
    </article>
  );
}
