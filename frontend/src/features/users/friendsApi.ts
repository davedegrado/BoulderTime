import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthProvider";
import type { PagedResult } from "@/lib/paging";
import type { FriendshipState, Profile } from "@/features/climbing/api";

/** Friendships (ADR-063). */
export interface FriendPerson { userId: string; displayName: string; avatarUrl: string | null }
export interface FriendRequest { person: FriendPerson; requestedAt: string }
export interface Friend { person: FriendPerson; since: string }

export const friendKeys = {
  requests: ["friends", "requests"] as const,
  of: (userId: string) => ["friends", "of", userId] as const,
};

export type FriendAction = "request" | "accept" | "remove";

/**
 * Ask, accept, or cancel / decline / end. Each answer is the friendship as it now stands; the profile is fetched again
 * too, since a friend sees more of a private one.
 */
export function useFriendAction(userId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (action: FriendAction) =>
      action === "request" ? api.post<{ state: FriendshipState }>(`/api/users/${userId}/friend-request`)
        : action === "accept" ? api.post<{ state: FriendshipState }>(`/api/users/${userId}/friend-request/accept`)
        : api.delete<{ state: FriendshipState }>(`/api/users/${userId}/friendship`),
    onSuccess: (result) => {
      qc.setQueryData<Profile>(["climbing", "profile", userId], (p) => (p ? { ...p, friendship: result.state } : p));
      qc.invalidateQueries({ queryKey: ["climbing", "profile"] });
      qc.invalidateQueries({ queryKey: ["climbing", "history"] });
      qc.invalidateQueries({ queryKey: ["friends"] });
    },
  });
}

/** Who asked you. Quiet against a server without friends: nothing to show, nothing to complain about. */
export function useFriendRequests() {
  const { session } = useAuth();
  return useQuery({
    queryKey: friendKeys.requests,
    queryFn: ({ signal }) => api.get<FriendRequest[]>("/api/users/me/friend-requests", { signal }),
    enabled: !!session,
    retry: false,
    staleTime: 30_000,
  });
}

export function useFriendsOf(userId: string, enabled = true) {
  return useInfiniteQuery({
    queryKey: friendKeys.of(userId),
    queryFn: ({ pageParam, signal }) => api.get<PagedResult<Friend>>(`/api/users/${userId}/friends?page=${pageParam}`, { signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.hasMore ? last.page + 1 : undefined),
    enabled,
  });
}
