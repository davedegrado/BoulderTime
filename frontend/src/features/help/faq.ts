import { activeLanguage } from "@/i18n/i18n";
import { FAQ_EN } from "@/features/help/faq.en";
import { FAQ_IT } from "@/features/help/faq.it";

/**
 * `videoUploads`: shown only when video uploads are on (true) or off (false), so the guide never describes a button
 * the app doesn't show; left out, always shown. `{maxVideoMb}` in an answer becomes the server's limit.
 */
export interface FaqItem { q: string; a: string; videoUploads?: boolean }
export interface FaqSection { id: string; title: string; items: FaqItem[] }
/** The guide: what climbers ask, and what gym staff ask. */
export interface FaqGuide { climbers: FaqSection[]; staff: FaqSection[] }
export type FaqAudience = keyof FaqGuide;

/**
 * The guide in the language in use. It is kept as whole texts per language, not as strings in the dictionary:
 * answers are paragraphs that read best written once in each language, not assembled from fragments.
 */
export const faqGuide = (): FaqGuide => (activeLanguage() === "en" ? FAQ_EN : FAQ_IT);

/** The guide as this server has it set up: items for the other setting left out, the video limit filled in. */
export function forSettings(guide: FaqGuide, videos: { uploadsEnabled: boolean; maxUploadMegabytes: number }): FaqGuide {
  const adapt = (sections: FaqSection[]) => sections
    .map((s) => ({
      ...s,
      items: s.items
        .filter((i) => i.videoUploads === undefined || i.videoUploads === videos.uploadsEnabled)
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
