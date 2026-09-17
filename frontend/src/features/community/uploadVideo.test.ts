const tusCalls: { file: Blob; options: Record<string, unknown> }[] = [];
let outcome: "success" | "error" = "success";

vi.mock("tus-js-client", () => ({
  Upload: class {
    constructor(public file: Blob, public options: Record<string, (...a: unknown[]) => void> & Record<string, unknown>) { tusCalls.push({ file, options }); }
    start() {
      const o = this.options as unknown as { onProgress: (a: number, b: number) => void; onSuccess: () => void; onError: (e: unknown) => void };
      if (outcome === "success") { o.onProgress(50, 100); o.onSuccess(); }
      else o.onError({ originalResponse: null });
    }
  },
}));

const posts: { path: string; body: unknown }[] = [];
vi.mock("@/lib/api", () => ({
  api: {
    post: async (path: string, body: unknown) => {
      posts.push({ path, body });
      return {
        path: "gyms/g/boulders/b/videos/x.mov", uploadUrl: "/api/storage/upload/t", method: "PUT", headers: {}, maxBytes: 104857600,
        resumable: { endpoint: "/api/storage/tus", headers: { "x-signature": "sig" }, metadata: { bucketName: "community-videos", objectName: "gyms/g/boulders/b/videos/x.mov", contentType: "video/quicktime" }, chunkSize: 6 * 1024 * 1024 },
      };
    },
  },
}));

import { RESUMABLE_RETRY_DELAYS, uploadVideo } from "@/features/community/api";

beforeEach(() => { tusCalls.length = 0; posts.length = 0; outcome = "success"; });

describe("video uploads", () => {
  it("send videos in resumable 6 MB chunks with retries, using the ticket's signature and metadata", async () => {
    const file = new File([new Uint8Array(1000)], "IMG_7258.mov", { type: "video/quicktime" });
    const progress: number[] = [];

    const path = await uploadVideo("b", file, "COMMUNITY", (p) => progress.push(p));

    expect(path).toBe("gyms/g/boulders/b/videos/x.mov");
    expect(posts[0]).toEqual({ path: "/api/boulders/b/video-uploads", body: { kind: "COMMUNITY", contentType: "video/quicktime", sizeBytes: 1000 } });
    const opts = tusCalls[0]!.options;
    expect(opts.chunkSize).toBe(6 * 1024 * 1024);
    expect(opts.headers).toEqual({ "x-signature": "sig" });
    expect(opts.retryDelays).toEqual(RESUMABLE_RETRY_DELAYS);
    expect((opts.metadata as Record<string, string>).objectName).toBe("gyms/g/boulders/b/videos/x.mov");
    expect(String(opts.endpoint)).toMatch(/^http:\/\/.+\/api\/storage\/tus$/);
    expect(progress).toEqual([0.5]);
  });

  it("explains an interrupted upload instead of blaming BoulderTime", async () => {
    outcome = "error";
    const file = new File([new Uint8Array(10)], "v.mp4", { type: "video/mp4" });
    await expect(uploadVideo("b", file, "BETA")).rejects.toThrow("The upload was interrupted. Check your connection and try again.");
  });

  it("rejects videos over 100 MB before uploading anything", async () => {
    const big = { size: 101 * 1024 * 1024, type: "video/mp4", name: "big.mp4" } as File;
    await expect(uploadVideo("b", big, "COMMUNITY")).rejects.toThrow(/under 100 MB/);
    expect(posts).toHaveLength(0);
  });
});
