import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { ChevronLeft, ChevronRight, Clapperboard, Clock, Play, Trash2, Upload, Video as VideoIcon, X, XCircle } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { uploadVideo, useBeta, useSaveBeta, useVideoMutations, useVideos, type UploadedVideo, type Video } from "@/features/community/api";
import { ReportButton } from "@/features/community/ReportButton";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { TextField } from "@/components/TextField";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

function Player({ src, poster, label, autoPlay }: { src: string; poster?: string | null; label: string; autoPlay?: boolean }) {
  return <video className="player" src={src} poster={poster ?? undefined} controls playsInline preload="metadata" autoPlay={autoPlay} aria-label={label} />;
}

/** Picks a video file, uploads it (resumable) with progress, then calls onUploaded. */
function VideoUploader({ boulderId, kind, submitLabel, onUploaded, busy }: {
  boulderId: string; kind: "COMMUNITY" | "BETA"; submitLabel: string; busy?: boolean;
  onUploaded: (video: UploadedVideo, caption: string) => Promise<unknown>;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [caption, setCaption] = useState("");
  const [progress, setProgress] = useState<number | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const toast = useToast();

  // Preview the chosen file locally before it's sent.
  useEffect(() => {
    if (!file) { setPreviewUrl(null); return; }
    const url = URL.createObjectURL(file);
    setPreviewUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);

  async function send() {
    if (!file) return;
    try {
      setProgress(0);
      const uploaded = await uploadVideo(boulderId, file, kind, setProgress);
      await onUploaded(uploaded, caption.trim());
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
        <Button variant="secondary" icon={<Upload aria-hidden />} onClick={() => input.current?.click()}>{t("Choose a video")}</Button>
      ) : (
        <>
          {previewUrl && (
            <video className="player uploader__preview" src={previewUrl} controls playsInline muted preload="metadata" aria-label={t("Preview of the selected video")} />
          )}
          <p className="list__sub">{file.name} · {(file.size / 1024 / 1024).toFixed(1)} MB</p>
          <TextField label={t("Caption")} value={caption} onChange={(e) => setCaption(e.target.value)} maxLength={300} hint={t("Optional")} />
          {progress !== null && (
            <div className="progress" role="progressbar" aria-valuenow={Math.round(progress * 100)} aria-valuemin={0} aria-valuemax={100}>
              <span className="progress__fill" style={{ width: `${progress * 100}%` }} />
            </div>
          )}
          {progress !== null && <p className="field__hint">{t("Keep this screen open until it finishes. Short signal drops resume automatically.")}</p>}
          <div className="form__actions">
            <Button onClick={send} loading={progress !== null || busy}>{progress !== null ? t("Uploading {percent}%", { percent: Math.round(progress * 100) }) : submitLabel}</Button>
            <Button variant="ghost" onClick={() => setFile(null)} disabled={progress !== null}>{t("Cancel")}</Button>
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

  if (beta.isPending) return <LoadingState label={t("Loading beta")} />;
  if (beta.isError) return <ErrorState error={beta.error} onRetry={() => beta.refetch()} />;
  if (!beta.data && !isStaff) return null;

  return (
    <section className="section card" aria-labelledby="beta-title">
      <h2 id="beta-title" className="section__title"><Clapperboard aria-hidden className="title-icon" /> {t("Official beta")}</h2>
      {beta.data ? (
        <>
          <Player src={beta.data.videoUrl} poster={beta.data.thumbnailUrl} label={t("Official beta video")} />
          {beta.data.caption && <p className="prose">{beta.data.caption}</p>}
          <p className="list__sub">{t("By {name}", { name: beta.data.uploadedBy.displayName })}</p>
        </>
      ) : <p className="list__sub">{t("No official beta yet.")}</p>}
      {isStaff && (replacing || !beta.data ? (
        <VideoUploader boulderId={boulderId} kind="BETA" submitLabel={beta.data ? t("Replace beta") : t("Publish beta")} busy={save.isPending}
          onUploaded={(v, caption) => save.mutateAsync({ storagePath: v.path, thumbnailPath: v.thumbnailPath, caption }).then(() => { setReplacing(false); toast.success(t("Official beta published")); })} />
      ) : (
        <div className="form__actions">
          <Button variant="secondary" onClick={() => setReplacing(true)}>{t("Replace")}</Button>
          <ConfirmButton icon={<Trash2 aria-hidden />} confirmLabel={t("Delete beta?")} loading={save.isPending}
            onConfirm={() => save.mutate(null, { onError: (e) => toast.error(errorMessage(e)) })}>{t("Delete")}</ConfirmButton>
        </div>
      ))}
    </section>
  );
}

/**
 * For videos without a captured thumbnail: shows a frame of the video itself. Only metadata and the first frames are
 * fetched, and only once the tile scrolls near the viewport. "#t=0.5" makes iOS Safari paint that frame.
 */
function FramePreview({ src }: { src: string }) {
  const ref = useRef<HTMLSpanElement>(null);
  const [visible, setVisible] = useState(typeof IntersectionObserver === "undefined");
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    if (visible || !ref.current) return;
    const io = new IntersectionObserver(([e]) => { if (e?.isIntersecting) { setVisible(true); io.disconnect(); } }, { rootMargin: "200px" });
    io.observe(ref.current);
    return () => io.disconnect();
  }, [visible]);
  return (
    <span ref={ref} className="video-thumb__live">
      {visible && !failed
        ? <video src={`${src}#t=0.5`} muted playsInline preload="metadata" tabIndex={-1} aria-hidden onError={() => setFailed(true)} />
        : <span className="video-thumb__placeholder" aria-hidden><VideoIcon /></span>}
    </span>
  );
}

function Thumb({ video, onOpen, status }: { video: Video; onOpen: () => void; status?: ReactNode }) {
  return (
    <button type="button" className="video-thumb" onClick={onOpen} aria-label={t("Play video by {name}", { name: video.author.displayName }) + (video.caption ? `: ${video.caption}` : "")}>
      <span className="video-thumb__frame">
        {video.thumbnailUrl
          ? <img src={video.thumbnailUrl} alt="" loading="lazy" />
          : <FramePreview src={video.videoUrl} />}
        <span className="video-thumb__play" aria-hidden><Play /></span>
        {status}
      </span>
      <span className="video-thumb__author">{video.author.displayName}</span>
    </button>
  );
}

export function CommunityVideosSection({ boulderId }: { boulderId: string }) {
  const { session } = useAuth();
  const videos = useVideos(boulderId);
  const m = useVideoMutations(boulderId);
  const toast = useToast();
  const rail = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState<{ list: "approved" | "mine"; index: number } | null>(null);

  const approved = videos.data?.pages.flatMap((p) => p.approved.items) ?? [];
  const total = videos.data?.pages[0]?.approved.total ?? 0;
  const mine = videos.data?.pages[0]?.mineInReview ?? [];
  const list = open?.list === "mine" ? mine : approved;

  const scroll = (dir: -1 | 1) => rail.current?.scrollBy({ left: dir * rail.current.clientWidth * 0.85, behavior: "smooth" });

  return (
    <section className="section" aria-labelledby="videos-title">
      <div className="section__row">
        <h2 id="videos-title" className="section__title">Community videos{total > 0 && ` (${total})`}</h2>
        {approved.length > 2 && (
          <div className="rail-arrows">
            <button type="button" className="icon-btn" onClick={() => scroll(-1)} aria-label={t("Scroll videos left")}><ChevronLeft aria-hidden /></button>
            <button type="button" className="icon-btn" onClick={() => scroll(1)} aria-label={t("Scroll videos right")}><ChevronRight aria-hidden /></button>
          </div>
        )}
      </div>

      {videos.isPending ? <LoadingState label={t("Loading videos")} />
        : videos.isError ? <ErrorState error={videos.error} onRetry={() => videos.refetch()} />
        : (
          <>
            {mine.length > 0 && (
              <div className="stack">
                <p className="section__meta">{t("Your videos in review")}</p>
                <div className="video-rail">
                  {mine.map((v, i) => (
                    <Thumb key={v.id} video={v} onOpen={() => setOpen({ list: "mine", index: i })}
                      status={<span className={`video-thumb__status video-thumb__status--${v.status.toLowerCase()}`}>{v.status === "PENDING" ? "In review" : "Not approved"}</span>} />
                  ))}
                </div>
              </div>
            )}
            {approved.length === 0 ? (
              <EmptyState icon={<VideoIcon />} title={t("No community videos yet.")} body={session ? t("Post your send — it goes live after the gym approves it.") : undefined} />
            ) : (
              <div className="video-rail" ref={rail} aria-label={t("Approved videos")}>
                {approved.map((v, i) => <Thumb key={v.id} video={v} onOpen={() => setOpen({ list: "approved", index: i })} />)}
                {videos.hasNextPage && (
                  <button type="button" className="video-thumb video-thumb--more" onClick={() => videos.fetchNextPage()} disabled={videos.isFetchingNextPage}>
                    <span className="video-thumb__frame"><span className="video-thumb__placeholder">{videos.isFetchingNextPage ? "Loading…" : t("Show {count} more", { count: Math.min(12, total - approved.length) })}</span></span>
                  </button>
                )}
              </div>
            )}
          </>
        )}

      {session && (
        <VideoUploader boulderId={boulderId} kind="COMMUNITY" submitLabel={t("Send for review")} busy={m.submit.isPending}
          onUploaded={(v, caption) => m.submit.mutateAsync({ storagePath: v.path, thumbnailPath: v.thumbnailPath, caption }).then(() => toast.success(t("Sent! It appears once the gym approves it.")))} />
      )}

      {open && list[open.index] && (
        <VideoViewer videos={list} index={open.index} signedIn={!!session}
          canLoadMore={open.list === "approved" && !!videos.hasNextPage}
          onLoadMore={() => videos.fetchNextPage()}
          onIndex={(index) => setOpen({ ...open, index })}
          onClose={() => setOpen(null)}
          onDelete={(id) => m.remove.mutate(id, { onSuccess: () => setOpen(null), onError: (e) => toast.error(errorMessage(e)) })} />
      )}
    </section>
  );
}

/** Full-screen viewer: one video at a time with previous/next. Escape closes, arrow keys navigate. */
function VideoViewer({ videos, index, signedIn, canLoadMore, onLoadMore, onIndex, onClose, onDelete }: {
  videos: Video[]; index: number; signedIn: boolean; canLoadMore: boolean;
  onLoadMore: () => void; onIndex: (i: number) => void; onClose: () => void; onDelete: (id: string) => void;
}) {
  const v = videos[index]!;
  const closeRef = useRef<HTMLButtonElement>(null);
  const hasPrev = index > 0;
  const hasNext = index < videos.length - 1 || canLoadMore;

  // Tapping "next" on the last loaded video fetches the next page, then advances once it arrives.
  const advanceAfterLoad = useRef(false);
  useEffect(() => {
    if (advanceAfterLoad.current && index < videos.length - 1) {
      advanceAfterLoad.current = false;
      onIndex(index + 1);
    }
  }, [videos.length, index, onIndex]);

  const next = useCallback(() => {
    if (index < videos.length - 1) onIndex(index + 1);
    else if (canLoadMore) { advanceAfterLoad.current = true; onLoadMore(); }
  }, [index, videos.length, canLoadMore, onIndex, onLoadMore]);

  const dialogRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null;
    closeRef.current?.focus();
    return () => opener?.focus?.(); // return focus to the thumbnail that opened the viewer
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Tab" && dialogRef.current) {
        // Keep keyboard focus inside the viewer while it's open.
        const focusable = Array.from(dialogRef.current.querySelectorAll<HTMLElement>("button:not([disabled]), video, a[href], select, textarea, input"));
        const first = focusable[0], last = focusable[focusable.length - 1];
        if (first && last) {
          if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
          else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
        }
      }
      if (e.key === "Escape") onClose();
      if (e.key === "ArrowLeft" && index > 0) onIndex(index - 1);
      if (e.key === "ArrowRight") next();
    };
    document.addEventListener("keydown", onKey);
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { document.removeEventListener("keydown", onKey); document.body.style.overflow = overflow; };
  }, [index, next, onClose, onIndex]);

  return (
    <div ref={dialogRef} className="viewer" role="dialog" aria-modal="true" aria-label={t("Video {index} of {total}", { index: index + 1, total: videos.length })}>
      <div className="viewer__top">
        <span className="viewer__count">{index + 1} / {videos.length}{canLoadMore ? "+" : ""}</span>
        <button ref={closeRef} type="button" className="viewer__close" onClick={onClose} aria-label={t("Close video")}><X aria-hidden /></button>
      </div>
      <div className="viewer__stage">
        <button type="button" className="viewer__nav viewer__nav--prev" onClick={() => onIndex(index - 1)} disabled={!hasPrev} aria-label={t("Previous video")}><ChevronLeft aria-hidden /></button>
        <Player key={v.id} src={v.videoUrl} poster={v.thumbnailUrl} label={t("Video by {name}", { name: v.author.displayName })} autoPlay />
        <button type="button" className="viewer__nav viewer__nav--next" onClick={next} disabled={!hasNext} aria-label={t("Next video")}><ChevronRight aria-hidden /></button>
      </div>
      <div className="viewer__info">
        <p className="list__title">{v.author.displayName}</p>
        {v.caption && <p className="viewer__caption">{v.caption}</p>}
        {v.isMine && v.status === "PENDING" && <p className="status-line"><Clock aria-hidden /> {t("Waiting for the gym's approval. Only you can see it.")}</p>}
        {v.isMine && v.status === "REJECTED" && <p className="status-line status-line--bad"><XCircle aria-hidden /> Not approved: {v.rejectionReason}</p>}
        <div className="comment__actions">
          {v.isMine && <ConfirmButton icon={<Trash2 aria-hidden />} confirmLabel={t("Delete video?")} onConfirm={() => onDelete(v.id)}>{t("Delete")}</ConfirmButton>}
          {signedIn && !v.isMine && <ReportButton entityType="VIDEO" entityId={v.id} />}
        </div>
      </div>
    </div>
  );
}
