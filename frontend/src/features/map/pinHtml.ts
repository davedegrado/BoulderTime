import { initials } from "@/lib/format";

const SAFE_URL = /^(https?:\/\/|\/)[^"'<>\s]*$/;
const escapeText = (text: string) => text.replace(/[<>&"']/g, "");

/**
 * Markup for a gym map pin: an upright circle with the gym's logo (or its initials) and a small pointer below.
 * Built as a string because Leaflet markers are raw HTML, so the logo URL is checked and the text escaped.
 * The initials stay behind the image, which also covers the case of an image that fails to load.
 */
export function buildPinHtml(name: string, logoUrl: string | null): string {
  const logo = logoUrl && SAFE_URL.test(logoUrl) ? logoUrl : null;
  const label = escapeText(initials(name));
  const image = logo ? `<img class="gym-pin__logo" src="${logo}" alt="" onerror="this.remove()">` : "";
  return `<span class="gym-pin__body"><span class="gym-pin__initials">${label}</span>${image}</span><span class="gym-pin__tail"></span>`;
}
