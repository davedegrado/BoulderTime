import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { ApiError, defaultMessage } from "@/lib/apiError";
import { prepareImage } from "@/features/boulders/imageResize";
import { userKeys, type CurrentUser } from "@/features/users/api";
import { gymKeys, type GymDetail } from "@/features/gyms/api";

interface Ticket { path: string; uploadUrl: string; method: string; headers: Record<string, string> }

async function uploadTo(ticketUrl: string, extra: object, blob: Blob): Promise<string> {
  const ticket = await api.post<Ticket>(ticketUrl, { ...extra, contentType: blob.type || "image/jpeg", sizeBytes: blob.size });
  let res: Response;
  try {
    res = await fetch(ticket.uploadUrl, { method: ticket.method, headers: ticket.headers, body: blob });
  } catch {
    throw new ApiError(0, null, defaultMessage(0));
  }
  if (!res.ok) throw new ApiError(res.status, null, "The image upload failed. Try again.");
  return ticket.path;
}

/** Upload (square, 512 px) or remove (file = null) the signed-in user's avatar. */
export function useSetAvatar() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (file: File | null) => {
      const path = file ? await uploadTo("/api/users/me/avatar-uploads", {}, await prepareImage(file, { maxSide: 512, square: true })) : null;
      return api.put<CurrentUser>("/api/users/me/avatar", { path });
    },
    onSuccess: (user) => {
      qc.setQueryData(userKeys.me, user);
      qc.invalidateQueries({ queryKey: ["climbing"] });
    },
  });
}

/** Upload or remove a gym's logo (square, 512 px) or cover (wide, 1600 px). */
export function useSetGymImage(gym: GymDetail, kind: "logo" | "cover") {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (file: File | null) => {
      const path = file
        ? await uploadTo(`/api/gyms/${gym.id}/image-uploads`, { kind: kind === "logo" ? "LOGO" : "COVER" },
            await prepareImage(file, kind === "logo" ? { maxSide: 512, square: true } : { maxSide: 1600, force: true }))
        : null;
      return api.put<GymDetail>(`/api/gyms/${gym.id}/${kind}`, { path });
    },
    onSuccess: (updated) => {
      qc.setQueryData(gymKeys.detail(gym.slug), (old: GymDetail | undefined) => (old ? { ...old, logoUrl: updated.logoUrl, coverImageUrl: updated.coverImageUrl } : updated));
      qc.invalidateQueries({ queryKey: gymKeys.all });
      qc.invalidateQueries({ queryKey: userKeys.me });
    },
  });
}
