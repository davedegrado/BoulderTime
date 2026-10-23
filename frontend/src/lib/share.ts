import { publicOrigin } from "@/lib/native";

/** The address to share for a page of the app: always the website's, which opens the app on phones that have it. */
export const shareUrl = (path: string) => `${publicOrigin()}${path}`;

/** Copies text; false when the device refuses (the caller says so). */
export async function copyText(text: string): Promise<boolean> {
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch {
    // fall back below
  }
  try {
    const area = document.createElement("textarea");
    area.value = text;
    area.setAttribute("readonly", "");
    area.style.position = "fixed";
    area.style.opacity = "0";
    document.body.appendChild(area);
    area.select();
    const ok = document.execCommand("copy");
    area.remove();
    return ok;
  } catch {
    return false;
  }
}

/**
 * The phone's share sheet when there is one; otherwise the link is copied. "shared", "copied", or "failed"; a share
 * the person cancels counts as shared (nothing to say about it).
 */
export async function shareLink(title: string, url: string): Promise<"shared" | "copied" | "failed"> {
  if (typeof navigator.share === "function") {
    try {
      await navigator.share({ title, url });
      return "shared";
    } catch (e) {
      if (e instanceof DOMException && e.name === "AbortError") return "shared";
    }
  }
  return (await copyText(url)) ? "copied" : "failed";
}
