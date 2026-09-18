import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthProvider";
import type { GymRole } from "@/lib/format";

export interface StaffGym {
  gymId: string;
  slug: string;
  name: string;
  city: string;
  logoUrl: string | null;
  role: GymRole;
}

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  isPlatformAdmin: boolean;
  createdAt: string;
  staffGyms: StaffGym[];
  pendingInvitations: number;
  language: "it" | "en";
}

export interface UpdateProfileInput {
  displayName: string;
  language?: "it" | "en";
}

export const userKeys = {
  me: ["users", "me"] as const,
};

/** The signed-in user's BoulderTime profile, staff memberships and pending invitation count. */
export function useCurrentUser() {
  const { session } = useAuth();
  return useQuery({
    queryKey: userKeys.me,
    queryFn: ({ signal }) => api.get<CurrentUser>("/api/users/me", { signal }),
    enabled: !!session,
    staleTime: 60_000,
  });
}

export function useUpdateProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateProfileInput) => api.patch<CurrentUser>("/api/users/me", input),
    onSuccess: (user) => qc.setQueryData(userKeys.me, user),
  });
}
