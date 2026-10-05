import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { GymSummary } from "@/features/gyms/api";
import type { PagedResult } from "@/lib/paging";
import type { CandidateStatus, GymStatus } from "@/lib/format";
import type { GymDetail } from "@/features/gyms/api";
import type { GymCandidate } from "@/features/candidates/api";

export interface AdminDashboard { totalGyms: number; activeGyms: number; pendingGymCandidates: number; users: number; pendingReports: number }
export interface AdminGym { id: string; slug: string; name: string; city: string; status: GymStatus; staffCount: number; ownerCount: number; pendingInvitations: number; createdAt: string }
export interface AdminUser { id: string; displayName: string; email: string; isPlatformAdmin: boolean; staffGyms: number; createdAt: string }
export interface CreateGymInput { name: string; city: string; address?: string; website?: string; email?: string; phone?: string; description?: string; candidateId?: string }

export const adminKeys = {
  partners: ["admin", "partners"] as const,
  dashboard: ["admin", "dashboard"] as const,
  gyms: (q: string, status: string) => ["admin", "gyms", q, status] as const,
  candidates: (status: string) => ["admin", "candidates", status] as const,
  users: (q: string) => ["admin", "users", q] as const,
};

const qs = (params: Record<string, string | undefined>) =>
  Object.entries(params).filter(([, v]) => v).map(([k, v]) => `${k}=${encodeURIComponent(v!)}`).join("&");

export function useAdminDashboard() {
  return useQuery({ queryKey: adminKeys.dashboard, queryFn: ({ signal }) => api.get<AdminDashboard>("/api/admin/dashboard", { signal }) });
}

export function useAdminGyms(q: string, status: GymStatus | "") {
  return useQuery({
    queryKey: adminKeys.gyms(q, status),
    queryFn: ({ signal }) => api.get<PagedResult<AdminGym>>(`/api/admin/gyms?${qs({ q, status, pageSize: "50" })}`, { signal }),
    placeholderData: keepPreviousData,
  });
}

export function useAdminCandidates(status: CandidateStatus | "") {
  return useQuery({
    queryKey: adminKeys.candidates(status),
    queryFn: ({ signal }) => api.get<PagedResult<GymCandidate>>(`/api/admin/gym-candidates?${qs({ status, pageSize: "50" })}`, { signal }),
    placeholderData: keepPreviousData,
  });
}

export function useAdminUsers(q: string) {
  return useQuery({
    queryKey: adminKeys.users(q),
    queryFn: ({ signal }) => api.get<PagedResult<AdminUser>>(`/api/admin/users?${qs({ q, pageSize: "50" })}`, { signal }),
    placeholderData: keepPreviousData,
  });
}

function useInvalidateAdmin() {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: ["admin"] });
    qc.invalidateQueries({ queryKey: ["gyms"] });
  };
}

export function useCreateGym() {
  const invalidate = useInvalidateAdmin();
  return useMutation({ mutationFn: (input: CreateGymInput) => api.post<GymDetail>("/api/admin/gyms", input), onSuccess: invalidate });
}

export function useSetGymStatus() {
  const invalidate = useInvalidateAdmin();
  return useMutation({
    mutationFn: ({ gymId, status }: { gymId: string; status: GymStatus }) => api.put<GymDetail>(`/api/admin/gyms/${gymId}/status`, { status }),
    onSuccess: invalidate,
  });
}

export function useInviteOwner() {
  const invalidate = useInvalidateAdmin();
  return useMutation({
    mutationFn: ({ gymId, email }: { gymId: string; email: string }) => api.post(`/api/admin/gyms/${gymId}/owner-invitations`, { email }),
    onSuccess: invalidate,
  });
}

export function useSetCandidateStatus() {
  const invalidate = useInvalidateAdmin();
  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: CandidateStatus }) => api.put<GymCandidate>(`/api/admin/gym-candidates/${id}/status`, { status }),
    onSuccess: invalidate,
  });
}


export interface EarlyPartner { id: string; gymId: string; startedAt: string; endedAt: string | null; note: string | null; isActive: boolean }
export interface PartnerGym { gym: GymSummary; earlyPartner: EarlyPartner | null }

/** The founding gym and the current early partners (platform admins only). */
export function usePartners() {
  return useQuery({ queryKey: adminKeys.partners, queryFn: ({ signal }) => api.get<PartnerGym[]>("/api/admin/partners", { signal }) });
}

export function usePartnerMutations() {
  const qc = useQueryClient();
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ["admin"] });
    qc.invalidateQueries({ queryKey: ["gyms"] });
    qc.invalidateQueries({ queryKey: ["climbing"] });
  };
  return {
    setFounding: useMutation({
      mutationFn: ({ gymId, isFoundingGym }: { gymId: string; isFoundingGym: boolean }) =>
        api.put(`/api/admin/gyms/${gymId}/founding`, { isFoundingGym }),
      onSuccess: invalidate,
    }),
    startEarlyPartner: useMutation({
      mutationFn: ({ gymId, note }: { gymId: string; note?: string }) => api.post(`/api/admin/gyms/${gymId}/early-partner`, { note }),
      onSuccess: invalidate,
    }),
    endEarlyPartner: useMutation({
      mutationFn: (gymId: string) => api.delete(`/api/admin/gyms/${gymId}/early-partner`),
      onSuccess: invalidate,
    }),
  };
}


export interface VideoAllowance {
  gymId: string; gymName: string; communityVideosEnabled: boolean; officialBetaLimit: number | null; officialBetaUsed: number;
}

/** What a gym may keep in video. Platform admins only: storage is a shared cost. */
export function useVideoAllowance(gymId: string | null) {
  return useQuery({
    queryKey: ["admin", "video-allowance", gymId],
    queryFn: ({ signal }) => api.get<VideoAllowance>(`/api/admin/gyms/${gymId}/video-allowance`, { signal }),
    enabled: Boolean(gymId),
  });
}

export function useSaveVideoAllowance() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ gymId, communityVideosEnabled, officialBetaLimit, unlimited }:
      { gymId: string; communityVideosEnabled: boolean; officialBetaLimit?: number; unlimited: boolean }) =>
      api.put<VideoAllowance>(`/api/admin/gyms/${gymId}/video-allowance`, { communityVideosEnabled, officialBetaLimit, unlimited }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin"] });
      qc.invalidateQueries({ queryKey: ["boulders"] });
    },
  });
}
