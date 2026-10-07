import { useState } from "react";
import { Link2 } from "lucide-react";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/** Sites the API accepts. Shown here so staff know before pasting, not after being refused. */
const ACCEPTED = "YouTube, Instagram, Vimeo, TikTok, Facebook";

/**
 * Points the official beta at a video the gym has already published. No upload, so it costs the gym nothing from its
 * video allowance — which is also the way out when a gym has used all of it.
 */
export function BetaLinkForm({ busy, onSave, onCancel }: {
  busy?: boolean;
  onSave: (externalUrl: string, caption: string | undefined) => Promise<unknown>;
  onCancel?: () => void;
}) {
  const [url, setUrl] = useState("");
  const [caption, setCaption] = useState("");
  const [sending, setSending] = useState(false);
  const toast = useToast();

  async function send() {
    const address = url.trim();
    if (!address) return;
    try {
      setSending(true);
      await onSave(address, caption.trim() || undefined);
      setUrl("");
      setCaption("");
    } catch (e) {
      toast.error(errorMessage(e));
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="stack">
      <TextField
        label={t("Address of the video")}
        value={url}
        onChange={(e) => setUrl(e.target.value)}
        maxLength={500}
        inputMode="url"
        placeholder="https://www.instagram.com/reel/…"
        hint={t("A video already published on {sites}.", { sites: ACCEPTED })}
      />
      <TextField label={t("Caption")} value={caption} onChange={(e) => setCaption(e.target.value)} maxLength={300} hint={t("Optional")} />
      <div className="form__actions">
        <Button icon={<Link2 aria-hidden />} onClick={send} loading={sending || busy} disabled={!url.trim()}>{t("Link this video")}</Button>
        {onCancel && <Button variant="ghost" onClick={onCancel} disabled={sending}>{t("Cancel")}</Button>}
      </div>
    </div>
  );
}
