import { useState } from "react";
import { Avatar } from "@/components/Avatar";
import { PhotoViewer } from "@/features/boulders/PhotoViewer";
import { t } from "@/i18n/i18n";

/**
 * A profile picture that opens full screen when tapped, in the same viewer as a boulder's photo. Without a picture it
 * is just the initials, and there is nothing to open.
 */
export function EnlargeableAvatar({ name, url, size = 72 }: { name: string; url: string | null; size?: number }) {
  const [open, setOpen] = useState(false);
  if (!url) return <Avatar name={name} url={null} size={size} />;
  return (
    <>
      <button type="button" className="avatar-btn" onClick={() => setOpen(true)} aria-label={t("Enlarge {name}'s photo", { name })}>
        <Avatar name={name} url={url} size={size} />
      </button>
      {open && <PhotoViewer src={url} alt={t("{name}'s photo", { name })} onClose={() => setOpen(false)} />}
    </>
  );
}
