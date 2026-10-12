import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { activeLanguage } from "@/i18n/i18n";

/**
 * `videoUploads`: shown only when video uploads are on (true) or off (false), so the guide never describes a button
 * the app doesn't show; left out, always shown. `{maxVideoMb}` in an answer becomes the server's limit.
 */
export interface FaqItem { q: string; a: string; videoUploads?: boolean | null }
export interface FaqSection { id: string; title: string; items: FaqItem[] }
/** The guide: what climbers ask, and what gym staff ask. `staff` comes back empty for anyone who isn't gym staff. */
export interface FaqGuide { climbers: FaqSection[]; staff: FaqSection[] }
export type FaqAudience = keyof FaqGuide;

/**
 * The guide in the language in use, as the server lets this person read it. The texts live on the server
 * (backend `Help/faq-it.json`, `faq-en.json`), written whole in each language: answers are paragraphs that read best written
 * once per language. Who reads which answer is decided there too, so the staff answers are not in the app at all.
 */
export function useFaqGuide() {
  const language = activeLanguage();
  return useQuery({
    queryKey: ["help", "faq", language],
    queryFn: ({ signal }) => api.get<FaqGuide>(`/api/help/faq?lang=${language}`, { signal }),
    staleTime: 60 * 60 * 1000,
  });
}

/** The guide as this server has it set up: items for the other setting left out, the video limit filled in. */
export function forSettings(guide: FaqGuide, videos: { uploadsEnabled: boolean; maxUploadMegabytes: number }): FaqGuide {
  const adapt = (sections: FaqSection[]) => sections
    .map((s) => ({
      ...s,
      items: s.items
        // null and absent both mean "always": the API sends null for the questions that don't depend on video uploads.
        .filter((i) => i.videoUploads == null || i.videoUploads === videos.uploadsEnabled)
        .map((i) => ({ ...i, a: i.a.split("{maxVideoMb}").join(String(videos.maxUploadMegabytes)) })),
    }))
    .filter((s) => s.items.length > 0);
  return { climbers: adapt(guide.climbers), staff: adapt(guide.staff) };
}

/** Lower case and without accents, so "piu" finds "più" and "Settore" finds "settore". */
const fold = (text: string) => text.normalize("NFD").replace(/\p{Diacritic}/gu, "").toLowerCase();

/** The questions whose wording or answer contains every word typed, section by section; empty sections left out. */
export function searchFaq(sections: FaqSection[], query: string): FaqSection[] {
  const words = fold(query).split(/\s+/).filter((w) => w.length > 1);
  if (words.length === 0) return sections;
  return sections
    .map((s) => ({ ...s, items: s.items.filter((i) => { const text = fold(`${i.q} ${i.a}`); return words.every((w) => text.includes(w)); }) }))
    .filter((s) => s.items.length > 0);
}
