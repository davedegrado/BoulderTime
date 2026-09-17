import { useRef, useState } from "react";
import { Clapperboard, Clock, Trash2, Upload, Video as VideoIcon, XCircle } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { uploadVideo, useBeta, useSaveBeta, useVideoMutations, useVideos, type Video } from "@/features/community/api";
import { ReportButton } from "@/features/community/ReportButton";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { TextField } from "@/components/TextField";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";

function Player({ src, label }: { src: string; label: string }) {
  return <video className="player" src={src} controls playsInline preload="metadata" aria-label={label} />;
}

/** Picks a video file, uploads it with progress, then calls onUploaded(storagePath, caption). */
function VideoUploader({ boulderId, kind, submitLabel, onUploaded, busy }: {
  boulderId: string; kind: "COMMUNITY" | "BETA"; submitLabel: string; busy?: boolean;
  onUploaded: (path: string, caption: string) => Promise<unknown>;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [caption, setCaption] = useState("");
  const [progress, setProgress] = useState<number | null>(null);
  const toast = useToast();

  async function send() {
    if (!file) return;
    try {
      setProgress(0);
      const path = await uploadVideo(boulderId, file, kind, setProgress);
      await onUploaded(path, caption.trim());
      setFile(null);
      setCaption("");
    } catch (e) {
      toast.error(errorMessage(e));
    } finally {
      setProgress(null);
    }
  }

  return (
    <div className="uploader">
      <input ref={input} type="file" accept="video/mp4,video/quicktime,video/webm,video/*" hidden onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
      {!file ? (
        <Button variant="secondary" icon={<Upload aria-hidden />} onClick={() => input.current?.click()}>Choose a video</Button>
      ) : (
        <>
          <p className="list__sub">{file.name} · {(file.size / 1024 / 1024).toFixed(1)} MB</p>
          <TextField label="Caption" value={caption} onChange={(e) => setCaption(e.target.value)} maxLength={300} hint="Optional" />
          {progress !== null && (
            <div className="progress" role="progressbar" aria-valuenow={Math.round(progress * 100)} aria-valuemin={0} aria-valuemax={100}>
              <span className="progress__fill" style={{ width: `${progress * 100}%` }} />
            </div>
          )}
          {progress !== null && <p className="field__hint">Keep this screen open until it finishes. Short signal drops resume automatically.</p>}
          <div className="form__actions">
            <Button onClick={send} loading={progress !== null || busy}>{progress !== null ? `Uploading ${Math.round(progress * 100)}%` : submitLabel}</Button>
            <Button variant="ghost" onClick={() => setFile(null)} disabled={progress !== null}>Cancel</Button>
          </div>
        </>
      )}
    </div>
  );
}

export function BetaSection({ boulderId, isStaff }: { boulderId: string; isStaff: boolean }) {
  const beta = useBeta(boulderId);
  const save = useSaveBeta(boulderId);
  const toast = useToast();
  const [replacing, setReplacing] = useState(false);

  if (beta.isPending) return <LoadingState label="Loading beta" />;
  if (beta.isError) return <ErrorState error={beta.error} onRetry={() => beta.refetch()} />;
  if (!beta.data && !isStaff) return null;

  return (
    <section className="section card" aria-labelledby="beta-title">
      <h2 id="beta-title" className="section__title"><Clapperboard aria-hidden className="title-icon" /> Official beta</h2>
      {beta.data ? (
        <>
          <Player src={beta.data.videoUrl} label="Official beta video" />
          {beta.data.caption && <p className="prose">{beta.data.caption}</p>}
          <p className="list__sub">By {beta.data.uploadedBy.displayName}</p>
        </>
      ) : <p className="list__sub">No official beta yet.</p>}
      {isStaff && (replacing || !beta.data ? (
        <VideoUploader boulderId={boulderId} kind="BETA" submitLabel={beta.data ? "Replace beta" : "Publish beta"} busy={save.isPending}
          onUploaded={(storagePath, caption) => save.mutateAsync({ storagePath, caption }).then(() => { setReplacing(false); toast.success("Official beta published"); })} />
      ) : (
        <div className="form__actions">
          <Button variant="secondary" onClick={() => setReplacing(true)}>Replace</Button>
          <ConfirmButton icon={<Trash2 aria-hidden />} confirmLabel="Delete beta?" loading={save.isPending}
            onConfirm={() => save.mutate(null, { onError: (e) => toast.error(errorMessage(e)) })}>Delete</ConfirmButton>
        </div>
      ))}
    </section>
  );
}

export function CommunityVideosSection({ boulderId }: { boulderId: string }) {
  const { session } = useAuth();
  const videos = useVideos(boulderId);
  const m = useVideoMutations(boulderId);
  const toast = useToast();

  return (
    <section className="section" aria-labelledby="videos-title">
      <h2 id="videos-title" className="section__title">Community videos</h2>
      {videos.isPending ? <LoadingState label="Loading videos" />
        : videos.isError ? <ErrorState error={videos.error} onRetry={() => videos.refetch()} />
        : videos.data.length === 0 ? <EmptyState icon={<VideoIcon />} title="No community videos yet." body={session ? "Post your send — it goes live after the gym approves it." : undefined} />
        : <ul className="videos">{videos.data.map((v) => <VideoItem key={v.id} video={v} onDelete={() => m.remove.mutate(v.id, { onError: (e) => toast.error(errorMessage(e)) })} signedIn={!!session} />)}</ul>}
      {session && (
        <VideoUploader boulderId={boulderId} kind="COMMUNITY" submitLabel="Send for review" busy={m.submit.isPending}
          onUploaded={(storagePath, caption) => m.submit.mutateAsync({ storagePath, caption }).then(() => toast.success("Sent! It appears once the gym approves it."))} />
      )}
    </section>
  );
}

function VideoItem({ video: v, onDelete, signedIn }: { video: Video; onDelete: () => void; signedIn: boolean }) {
  return (
    <li className="video-item">
      <Player src={v.videoUrl} label={`Video by ${v.author.displayName}`} />
      <div className="video-item__meta">
        <p className="list__title">{v.author.displayName}</p>
        {v.caption && <p className="list__sub">{v.caption}</p>}
        {v.isMine && v.status === "PENDING" && <p className="status-line"><Clock aria-hidden /> Waiting for the gym's approval. Only you can see it.</p>}
        {v.isMine && v.status === "REJECTED" && <p className="status-line status-line--bad"><XCircle aria-hidden /> Not approved: {v.rejectionReason}</p>}
        <div className="comment__actions">
          {v.isMine && <ConfirmButton icon={<Trash2 aria-hidden />} confirmLabel="Delete video?" onConfirm={onDelete}>Delete</ConfirmButton>}
          {signedIn && !v.isMine && <ReportButton entityType="VIDEO" entityId={v.id} />}
        </div>
      </div>
    </li>
  );
}
