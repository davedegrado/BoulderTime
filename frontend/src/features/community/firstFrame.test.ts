import { describe, expect, it } from "vitest";
import type { SyntheticEvent } from "react";
import { showFirstFrame } from "./firstFrame";

const event = (v: Partial<HTMLVideoElement>) => ({ currentTarget: v } as unknown as SyntheticEvent<HTMLVideoElement>);

describe("showFirstFrame", () => {
  it("moves a paused video at the start to half a second in, or the middle of a shorter one", () => {
    const long = { paused: true, currentTime: 0, duration: 30 };
    showFirstFrame(event(long));
    expect(long.currentTime).toBe(0.5);
    const short = { paused: true, currentTime: 0, duration: 0.6 };
    showFirstFrame(event(short));
    expect(short.currentTime).toBeCloseTo(0.3);
  });

  it("leaves alone a video that is playing or already showing a frame", () => {
    const playing = { paused: false, currentTime: 0, duration: 30 };
    showFirstFrame(event(playing));
    expect(playing.currentTime).toBe(0);
    const fragment = { paused: true, currentTime: 0.5, duration: 30 };
    showFirstFrame(event(fragment));
    expect(fragment.currentTime).toBe(0.5);
  });
});
