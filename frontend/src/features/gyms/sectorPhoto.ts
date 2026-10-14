import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { gymKeys, type Sector } from "@/features/gyms/api";
import { putToTicket, type UploadTicket } from "@/features/boulders/api";
import { prepareImage } from "@/features/boulders/imageResize";

/**
 * A sector's wall photo: uploaded once, then every boulder set on that wall uses it as its photo and cuts its own
 * card picture out of it. Replacing or removing it deletes the old file, and the boulders set on it lose their photo
 * (they keep their card picture).
 */

/** The boulders that would lose their photo if the wall photo were replaced or removed now. */
export interface SectorPhotoUsage { onTheWall: number; removed: number }

export const fetchSectorPhotoUsage = (sectorId: string): Promise<SectorPhotoUsage> =>
  api.get<SectorPhotoUsage>(`/api/sectors/${sectorId}/photo/usage`);

/** The wall photo through the API, so the device may read its pixels to cut a card picture out of it. */
export const fetchSectorPhoto = (sectorId: string): Promise<Blob> => api.blob(`/api/sectors/${sectorId}/photo`);

/** Prepares the photo on the device (≤1600 px, like a boulder's) and uploads it. Returns its storage path. */
export async function uploadSectorPhoto(sectorId: string, file: File): Promise<string> {
  const blob = await prepareImage(file);
  const ticket = await api.post<UploadTicket>(`/api/sectors/${sectorId}/photo-uploads`, { contentType: blob.type || file.type, sizeBytes: blob.size });
  await putToTicket(ticket, blob);
  return ticket.path;
}

/** Sets the wall photo to an uploaded path, or removes it with null. */
export function useSetSectorPhoto(gymId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ sectorId, path }: { sectorId: string; path: string | null }) => api.put<Sector>(`/api/sectors/${sectorId}/photo`, { path }),
    onSuccess: () => Promise.all([
      qc.invalidateQueries({ queryKey: gymKeys.sectors(gymId) }),
      // The boulders set on the old photo have just lost it.
      qc.invalidateQueries({ queryKey: ["boulders"] }),
    ]),
  });
}
