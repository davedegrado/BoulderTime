import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { ApiError } from "@/lib/apiError";
import { putToTicket, type UploadTicket } from "@/features/boulders/api";
import { BOULDER_PHOTO, WHOLE_CARD_PICTURE, prepareImage } from "@/features/boulders/imageResize";

/**
 * A gym's existing boulder pictures made as light as the app now makes new ones. Each picture is encoded again here,
 * on the administrator's device, uploaded as a new file and swapped in; the old files stay until confirmed, so the
 * whole gym can be put back as it was. Platform administrators only.
 */

export type PictureFile = "PHOTO" | "THUMBNAIL";
export interface LighterCandidate { boulderId: string; file: PictureFile; path: string }
export interface LighterOverview { pending: LighterCandidate[]; replaced: number; bytesBefore: number; bytesAfter: number; confirmed: number }
export interface LighterResult { changed: number; skipped: number; bytesFreed: number }

const base = (gymId: string) => `/api/admin/gyms/${gymId}/lighter-pictures`;
const key = (gymId: string) => ["admin", "lighter-pictures", gymId];

export function useLighterPictures(gymId: string) {
  return useQuery({ queryKey: key(gymId), queryFn: ({ signal }) => api.get<LighterOverview>(base(gymId), { signal }) });
}

export function useRefreshLighterPictures(gymId: string) {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: key(gymId) });
}

/** A copy has to save at least this much to be worth swapping in. */
const WORTH_IT = 0.9;

/**
 * Makes one picture lighter. "kept" when the copy would save next to nothing, staff changed the picture meanwhile, or
 * its file is missing from storage: in every case the boulder keeps what it has.
 */
export async function makeLighter(gymId: string, c: LighterCandidate): Promise<"replaced" | "kept"> {
  let source: Blob;
  try {
    source = await api.blob(`${base(gymId)}/${c.boulderId}/${c.file}`);
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) return "kept";
    throw err;
  }
  const copy = await prepareImage(source, c.file === "PHOTO" ? BOULDER_PHOTO : WHOLE_CARD_PICTURE);
  if (copy.type !== "image/jpeg" || copy.size >= source.size * WORTH_IT) return "kept";
  const ticket = await api.post<UploadTicket>(`${base(gymId)}/uploads`, { file: c.file, sizeBytes: copy.size });
  await putToTicket(ticket, copy);
  try {
    await api.post(base(gymId), { boulderId: c.boulderId, file: c.file, from: c.path, to: ticket.path });
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) return "kept";
    throw err;
  }
  return "replaced";
}

export const undoLighterPictures = (gymId: string, gymSlug: string) => api.post<LighterResult>(`${base(gymId)}/undo`, { gymSlug });
export const confirmLighterPictures = (gymId: string, gymSlug: string) => api.post<LighterResult>(`${base(gymId)}/confirm`, { gymSlug });

export function formatMegabytes(bytes: number) {
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}
