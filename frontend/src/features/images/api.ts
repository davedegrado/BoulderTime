import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { ApiError, defaultMessage } from "@/lib/apiError";
import { prepareImage } from "@/features/boulders/imageResize";
import { userKeys, type CurrentUser } from "@/features/users/api";
import { gymKeys, type GymDetail } from "@/features/gyms/api";
import { t } from "@/i18n/i18n";

interface Ticket { path: string; uploadUrl: string; method: string; headers: Record<string, string> }

async function uploadTo(ticketUrl: string, extra: object, blob: Blob): Promise<string> {
  const ticket = await api.post<Ticket>(ticketUrl, { ...extra, contentType: blob.type || "image/jpeg", sizeBytes: blob.size });
  let res: Response;
  try {
    res = await fetch(ticket.uploadUrl, { method: ticket.method, headers: ticket.headers, body: blob });
  } catch {
    throw new ApiError(0, null, defaultMessage(0));
  }
  if (!res.ok) throw new ApiError(res.status, null, t("The image upload failed. Try again."));
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

/** Reads a picture's size in pixels, as it is shown (orientation applied). */
async function imageSize(blob: Blob): Promise<{ width: number; height: number }> {
  if (typeof createImageBitmap === "function") {
    try {
      const bitmap = await createImageBitmap(blob, { imageOrientation: "from-image" });
      const size = { width: bitmap.width, height: bitmap.height };
      bitmap.close();
      return size;
    } catch {
      // fall through
    }
  }
  const url = URL.createObjectURL(blob);
  try {
    const img = new Image();
    img.src = url;
    await img.decode();
    return { width: img.naturalWidth, height: img.naturalHeight };
  } finally {
    URL.revokeObjectURL(url);
  }
}

/** Upload (up to 2400 px, sharp enough to zoom into) or remove (file = null) the gym's floor plan. */
export function useSetFloorPlan(gym: GymDetail) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (file: File | null) => {
      if (!file) return api.put<GymDetail>(`/api/gyms/${gym.id}/floor-plan`, { path: null });
      const blob = await prepareImage(file, { maxSide: 2400, quality: 0.85, force: true });
      const { width, height } = await imageSize(blob);
      const path = await uploadTo(`/api/gyms/${gym.id}/image-uploads`, { kind: "FLOOR_PLAN" }, blob);
      return api.put<GymDetail>(`/api/gyms/${gym.id}/floor-plan`, { path, width, height });
    },
    onSuccess: (updated) => {
      qc.setQueryData(gymKeys.detail(gym.slug), (old: GymDetail | undefined) => (old
        ? { ...old, floorPlanUrl: updated.floorPlanUrl, floorPlanWidth: updated.floorPlanWidth, floorPlanHeight: updated.floorPlanHeight }
        : updated));
      qc.invalidateQueries({ queryKey: gymKeys.all });
    },
  });
}
