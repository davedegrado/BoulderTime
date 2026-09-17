/**
 * Captures a poster frame from a local video file in the browser (no server-side video processing needed).
 * Returns null when the browser can't decode the file (e.g. some HEVC .mov on desktop) or it takes too long;
 * the video is still uploaded, just without a thumbnail.
 */
export async function captureVideoThumbnail(file: Blob, maxSide = 480, timeoutMs = 8000): Promise<Blob | null> {
  if (typeof document === "undefined" || typeof URL.createObjectURL !== "function") return null;
  const url = URL.createObjectURL(file);
  const video = document.createElement("video");
  video.muted = true;
  video.playsInline = true;
  video.preload = "auto";
  video.src = url;

  const cleanup = () => { video.removeAttribute("src"); video.load(); URL.revokeObjectURL(url); };
  const once = (event: string) => new Promise<void>((resolve, reject) => {
    video.addEventListener(event, () => resolve(), { once: true });
    video.addEventListener("error", () => reject(new Error("decode")), { once: true });
  });

  const work = (async () => {
    await once("loadedmetadata");
    // A frame about a second in avoids black first frames; short clips use a third of their length.
    const target = Number.isFinite(video.duration) && video.duration > 0 ? Math.min(1, video.duration / 3) : 0;
    video.currentTime = target;
    await once("seeked");
    const scale = Math.min(1, maxSide / Math.max(video.videoWidth, video.videoHeight));
    if (!video.videoWidth || !video.videoHeight) return null;
    const canvas = document.createElement("canvas");
    canvas.width = Math.round(video.videoWidth * scale);
    canvas.height = Math.round(video.videoHeight * scale);
    canvas.getContext("2d")!.drawImage(video, 0, 0, canvas.width, canvas.height);
    return await new Promise<Blob | null>((resolve) => canvas.toBlob((b) => resolve(b), "image/jpeg", 0.75));
  })();

  const timeout = new Promise<null>((resolve) => window.setTimeout(() => resolve(null), timeoutMs));
  try {
    return await Promise.race([work, timeout]);
  } catch {
    return null;
  } finally {
    cleanup();
  }
}
