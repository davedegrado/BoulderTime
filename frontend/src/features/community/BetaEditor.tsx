import { useState } from "react";
import { ExternalLink, Link2, Lock, ShieldCheck, Trash2, Upload } from "lucide-react";
import { InstagramIcon } from "@/components/BrandIcons";
import { useBeta, useSaveBeta, type Beta } from "@/features/community/api";
import { BetaLinkForm } from "@/features/community/BetaLinkForm";
import { Player, VideoUploader } from "@/features/community/VideoUploader";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/** The site a linked beta points at, for the button's label. Falls back to the host if we don't recognise it. */
export function siteName(url: string): string {
  let host: string;
  try { host = new URL(url).hostname.replace(/^www\./, ""); } catch { return t("the video"); }
  const known: Record<string, string> = {
    "youtube.com": "YouTube", "youtu.be": "YouTube", "instagram.com": "Instagram",
    "vimeo.com": "Vimeo", "tiktok.com": "TikTok", "facebook.com": "Facebook", "fb.watch": "Facebook",
  };
  return known[host] ?? Object.entries(known).find(([h]) => host.endsWith(`.${h}`))?.[1] ?? host;
}

/**
 * The beta as anyone sees it: its description ("Beta di Matteo" — free text, not necessarily who is in the video),
 * whose beta it is — the gym's — and the way to watch it. Who on the staff published it only reaches the staff
 * (the server leaves it out for everyone else), and is shown to them in small print.
 */
export function BetaView({ beta, gymName }: { beta: Beta; gymName?: string }) {
  const site = beta.externalUrl ? siteName(beta.externalUrl) : null;
  return (
    <div className="beta-card">
      {beta.caption && <p className="beta-card__caption">{beta.caption}</p>}
      {gymName && <p className="beta-card__owner"><ShieldCheck aria-hidden /> {t("Official beta from {gym}", { gym: gymName })}</p>}
      {/* A linked video stays on its own site: embedding it would need their player, their cookies and their consent. */}
      {beta.externalUrl ? (
        <a className="btn btn--secondary btn--block beta-card__watch" href={beta.externalUrl} target="_blank" rel="noreferrer noopener">
          {site === "Instagram" ? <InstagramIcon aria-hidden /> : <ExternalLink aria-hidden />}
          <span>{t("Watch on {site}", { site: site ?? "" })}</span>
        </a>
      ) : (
        <Player src={beta.videoUrl} poster={beta.thumbnailUrl} label={t("Official beta video")} />
      )}
    </div>
  );
}

/**
 * Publishing the gym's beta: upload a video, or link one already published elsewhere. This lives in the boulder
 * editor rather than on the climber's page, because it is part of setting the boulder — the same place as its photo
 * and its grade.
 */
export function BetaEditor({ boulderId, canAdd }: { boulderId: string; canAdd: boolean }) {
  const beta = useBeta(boulderId);
  const save = useSaveBeta(boulderId);
  const toast = useToast();
  const [replacing, setReplacing] = useState(false);
  // A gym out of upload slots can still link, so that is the form it opens on.
  const [linking, setLinking] = useState(!canAdd);

  if (beta.isPending) return <LoadingState label={t("Loading beta")} />;
  if (beta.isError) return <ErrorState error={beta.error} onRetry={() => beta.refetch()} />;

  const published = () => { setReplacing(false); toast.success(t("Official beta published")); };
  const editing = replacing || !beta.data;

  return (
    <div className="stack">
      {beta.data ? <BetaView beta={beta.data} /> : <p className="list__sub">{t("No official beta yet.")}</p>}

      {/* Told before filming, not after uploading: the limit is the gym's, and hitting it at the last step wastes real work. */}
      {editing && !canAdd && (
        <p className="notice notice--inline">
          <Lock aria-hidden />
          {t("This gym has used all its official beta videos. You can still link a video published elsewhere, or remove a beta from another boulder.")}
        </p>
      )}

      {editing ? (
        <>
          {linking ? (
            <BetaLinkForm busy={save.isPending}
              onSave={(externalUrl, caption) => save.mutateAsync({ externalUrl, caption }).then(published)} />
          ) : (
            <VideoUploader boulderId={boulderId} kind="BETA" submitLabel={beta.data ? t("Replace beta") : t("Publish beta")} busy={save.isPending}
              onUploaded={(v, caption) => save.mutateAsync({ storagePath: v.path, thumbnailPath: v.thumbnailPath, caption }).then(published)} />
          )}
          <div className="form__actions">
            {linking
              ? canAdd && <Button variant="ghost" icon={<Upload aria-hidden />} onClick={() => setLinking(false)}>{t("Upload a video instead")}</Button>
              : <Button variant="ghost" icon={<Link2 aria-hidden />} onClick={() => setLinking(true)}>{t("Link a video instead")}</Button>}
            {beta.data && <Button variant="ghost" onClick={() => setReplacing(false)}>{t("Cancel")}</Button>}
          </div>
        </>
      ) : (
        <div className="form__actions">
          <Button variant="secondary" onClick={() => { setLinking(!canAdd); setReplacing(true); }}>{t("Replace")}</Button>
          <ConfirmButton icon={<Trash2 aria-hidden />} confirmLabel={t("Delete beta?")} loading={save.isPending}
            onConfirm={() => save.mutate(null, { onError: (e) => toast.error(errorMessage(e)) })}>{t("Delete")}</ConfirmButton>
        </div>
      )}
    </div>
  );
}
