import type { SyntheticEvent } from "react";

/**
 * Makes a paused video show a frame instead of an empty box. iOS Safari paints the frame named by "#t=0.5" in the
 * address; Android's WebView loads only the metadata and paints nothing until the video plays. Asking for a position
 * once the metadata is in makes it fetch and draw that frame. Harmless where the frame is already there.
 */
export function showFirstFrame(event: SyntheticEvent<HTMLVideoElement>, at = 0.5) {
  const video = event.currentTarget;
  if (!video.paused || video.currentTime > 0) return;
  const duration = Number.isFinite(video.duration) && video.duration > 0 ? video.duration : 1;
  try { video.currentTime = Math.min(at, duration / 2); } catch { /* not seekable yet: the play button still works */ }
}

/** For a video with controls: barely past the start, so playing it doesn't visibly skip anything. */
export const showStartFrame = (event: SyntheticEvent<HTMLVideoElement>) => showFirstFrame(event, 0.05);
