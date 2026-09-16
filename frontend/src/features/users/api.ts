import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthProvider";

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  isPlatformAdmin: boolean;
  createdAt: string;
}

export interface UpdateProfileInput {
  displayName: string;
}

export const userKeys = {
  me: ["users", "me"] as const,
};

/** The signed-in user's BoulderTime profile. The API provisions it on first call. */
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
