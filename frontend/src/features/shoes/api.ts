import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";

/**
 * Climbing shoes (proposal). A shared catalogue the server keeps — brand, model, picture when there is one, whether
 * still made — and the pairs each person chose from it, one of them at most marked as the main pair. People choose,
 * never add to the catalogue.
 */
export type ShoeStatus = "CURRENT" | "DISCONTINUED";
export interface CatalogShoe { id: string; brand: string; name: string; imageUrl: string | null; status: ShoeStatus }
export interface MyShoe extends CatalogShoe { isPrimary: boolean }
export interface UserShoes { shoes: MyShoe[] }
export interface ShoeCatalogPage { items: CatalogShoe[]; total: number }

export const SHOE_BRANDS = ["La Sportiva", "SCARPA", "OCÚN", "SIMOND"] as const;
const CATALOG_LIMIT = 30;

export const shoeKeys = {
  of: (userId: string) => ["shoes", userId] as const,
  catalog: (q: string, brand: string) => ["shoe-catalog", q, brand] as const,
};

/** A person's shoes, main pair first; nothing for a private profile the viewer may not see. */
export function useUserShoes(userId: string | undefined) {
  return useQuery({
    queryKey: shoeKeys.of(userId ?? ""),
    queryFn: () => api.get<UserShoes>(`/api/users/${userId}/shoes`),
    enabled: Boolean(userId),
  });
}

/** One page of the catalogue for a search and a brand: never the whole catalogue at once. */
export function useShoeCatalog(q: string, brand: string) {
  return useQuery({
    queryKey: shoeKeys.catalog(q, brand),
    queryFn: () => api.get<ShoeCatalogPage>(`/api/climbing-shoes?${new URLSearchParams({ ...(q ? { q } : {}), ...(brand ? { brand } : {}), limit: String(CATALOG_LIMIT) })}`),
    placeholderData: keepPreviousData,
    staleTime: 5 * 60_000,
  });
}

/** Adds, removes and marks the main pair; the server answers with the person's shoes as they now are. */
export function useEditShoes(userId: string) {
  const qc = useQueryClient();
  const done = (data: UserShoes) => qc.setQueryData(shoeKeys.of(userId), data);
  return {
    add: useMutation({ mutationFn: (shoeId: string) => api.put<UserShoes>(`/api/users/me/shoes/${shoeId}`), onSuccess: done }),
    remove: useMutation({ mutationFn: (shoeId: string) => api.delete<UserShoes>(`/api/users/me/shoes/${shoeId}`), onSuccess: done }),
    primary: useMutation({
      mutationFn: ({ shoeId, on }: { shoeId: string; on: boolean }) =>
        on ? api.put<UserShoes>(`/api/users/me/shoes/${shoeId}/primary`) : api.delete<UserShoes>(`/api/users/me/shoes/${shoeId}/primary`),
      onSuccess: done,
    }),
  };
}

/** Main pair first, then as chosen. */
export const ordered = (shoes: MyShoe[]) => [...shoes].sort((a, b) => Number(b.isPrimary) - Number(a.isPrimary));
