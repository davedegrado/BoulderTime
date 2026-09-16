import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { userKeys, type StaffGym } from "@/features/users/api";
import type { GymRole } from "@/lib/format";

export interface StaffMember {
  userId: string;
  displayName: string;
  email: string;
  avatarUrl: string | null;
  role: GymRole;
  since: string;
}

export interface GymInvitation {
  id: string;
  email: string;
  role: GymRole;
  status: string;
  createdAt: string;
  expiresAt: string;
  invitedBy: string;
}

export interface MyInvitation {
  id: string;
  gymId: string;
  gymSlug: string;
  gymName: string;
  gymCity: string;
  role: GymRole;
  invitedBy: string;
  expiresAt: string;
}

export const staffKeys = {
  members: (gymId: string) => ["staff", gymId, "members"] as const,
  invitations: (gymId: string) => ["staff", gymId, "invitations"] as const,
  mine: ["invitations", "mine"] as const,
};

export function useStaff(gymId: string) {
  return useQuery({
    queryKey: staffKeys.members(gymId),
    queryFn: ({ signal }) => api.get<StaffMember[]>(`/api/gyms/${gymId}/staff`, { signal }),
  });
}

export function useGymInvitations(gymId: string) {
  return useQuery({
    queryKey: staffKeys.invitations(gymId),
    queryFn: ({ signal }) => api.get<GymInvitation[]>(`/api/gyms/${gymId}/invitations`, { signal }),
  });
}

export function useInvite(gymId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: { email: string; role: GymRole }) => api.post<GymInvitation>(`/api/gyms/${gymId}/invitations`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: staffKeys.invitations(gymId) }),
  });
}

export function useRevokeInvitation(gymId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (invitationId: string) => api.delete<void>(`/api/gyms/${gymId}/invitations/${invitationId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: staffKeys.invitations(gymId) }),
  });
}

export function useChangeRole(gymId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: GymRole }) => api.patch<StaffMember>(`/api/gyms/${gymId}/staff/${userId}`, { role }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: staffKeys.members(gymId) });
      qc.invalidateQueries({ queryKey: userKeys.me });
    },
  });
}

export function useRemoveStaff(gymId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => api.delete<void>(`/api/gyms/${gymId}/staff/${userId}`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: staffKeys.members(gymId) });
      qc.invalidateQueries({ queryKey: userKeys.me });
    },
  });
}

export function useMyInvitations(enabled: boolean) {
  return useQuery({
    queryKey: staffKeys.mine,
    queryFn: ({ signal }) => api.get<MyInvitation[]>("/api/users/me/invitations", { signal }),
    enabled,
  });
}

export function useRespondToInvitation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, accept }: { id: string; accept: boolean }) =>
      api.post<StaffGym | undefined>(`/api/invitations/${id}/${accept ? "accept" : "decline"}`),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: staffKeys.mine });
      qc.invalidateQueries({ queryKey: userKeys.me });
    },
  });
}
