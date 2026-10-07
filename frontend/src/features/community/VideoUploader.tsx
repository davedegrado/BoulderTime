import { useEffect, useRef, useState } from "react";
import { Upload } from "lucide-react";
import { uploadVideo, type UploadedVideo } from "@/features/community/api";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";
import { MediaInput, type MediaInputHandle } from "@/components/MediaInput";

export function Player({ src, poster, label, autoPlay }: { src: string; poster?: string | null; label: string; autoPlay?: boolean }) {
  return <video className="player" src={src} poster={poster ?? undefined} controls playsInline preload="metadata" autoPlay={autoPlay} aria-label={label} />;
}

/** Picks a video file, uploads it (resumable) with progress, then calls onUploaded. */
export function VideoUploader({ boulderId, kind, submitLabel, onUploaded, busy }: {
  boulderId: string; kind: "COMMUNITY" | "BETA"; submitLabel: string; busy?: boolean;
  onUploaded: (video: UploadedVideo, caption: string) => Promise<unknown>;
}) {
  const input = useRef<MediaInputHandle>(null);
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
      <MediaInput ref={input} kind="video" accept="video/mp4,video/quicktime,video/webm,video/*" onFile={(f) => setFile(f ?? null)} />
      {!file ? (
        <Button variant="secondary" icon={<Upload aria-hidden />} onClick={() => input.current?.open()}>{t("Choose a video")}</Button>
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
