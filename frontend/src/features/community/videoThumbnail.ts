/**
 * Captures a poster frame from a local video file in the browser (no server-side video processing needed).
 *
 * iOS Safari doesn't decode frames of a video that hasn't started playing, so seeking alone never produces an image
 * there. We briefly play the (muted, inline) video to force decoding, then seek and draw.
 * Returns null when the browser can't decode the file or it takes too long; the video still uploads without one.
 */
export async function captureVideoThumbnail(file: Blob, maxSide = 480, timeoutMs = 10_000): Promise<Blob | null> {
  if (typeof document === "undefined" || typeof URL.createObjectURL !== "function") return null;
  const url = URL.createObjectURL(file);
  const video = document.createElement("video");
  video.muted = true;
  video.defaultMuted = true;
  video.playsInline = true;
  video.setAttribute("playsinline", "");
  video.setAttribute("muted", "");
  video.preload = "auto";
  video.crossOrigin = "anonymous";
  video.src = url;

  const cleanup = () => { try { video.pause(); } catch { /* ignore */ } video.removeAttribute("src"); video.load(); URL.revokeObjectURL(url); };
  const waitFor = (event: string) => new Promise<void>((resolve, reject) => {
    const onError = () => reject(new Error("decode"));
    video.addEventListener(event, () => { video.removeEventListener("error", onError); resolve(); }, { once: true });
    video.addEventListener("error", onError, { once: true });
  });

  const work = (async () => {
    if (video.readyState < 1) await waitFor("loadedmetadata");
    // Force frame decoding (required on iOS). Muted inline playback is allowed without a user gesture.
    try { await video.play(); } catch { /* some browsers refuse; seeking may still work */ }
    video.pause();

    const target = Number.isFinite(video.duration) && video.duration > 0 ? Math.min(1, video.duration / 3) : 0.1;
    const seeked = waitFor("seeked");
    video.currentTime = target;
    await seeked;
    if (video.readyState < 2) await waitFor("loadeddata");
    // Give the compositor one frame to paint the sought frame.
    await new Promise((r) => requestAnimationFrame(() => r(null)));

    if (!video.videoWidth || !video.videoHeight) return null;
    const scale = Math.min(1, maxSide / Math.max(video.videoWidth, video.videoHeight));
    const canvas = document.createElement("canvas");
    canvas.width = Math.round(video.videoWidth * scale);
    canvas.height = Math.round(video.videoHeight * scale);
    const ctx = canvas.getContext("2d")!;
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
    if (isBlank(ctx, canvas.width, canvas.height)) return null; // a black frame is worse than the live-frame fallback
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

/** True when a sample of pixels is (nearly) uniformly black — what failed decodes produce. */
function isBlank(ctx: CanvasRenderingContext2D, w: number, h: number): boolean {
  try {
    const { data } = ctx.getImageData(0, 0, w, h);
    const step = Math.max(4, Math.floor(data.length / 4 / 400)) * 4;
    for (let i = 0; i < data.length; i += step) if (data[i]! > 12 || data[i + 1]! > 12 || data[i + 2]! > 12) return false;
    return true;
  } catch {
    return false;
  }
}
