import type { Language } from "@/i18n/i18n";
import { PRIVACY_EN } from "@/pages/legal/privacy.en";
import { PRIVACY_IT } from "@/pages/legal/privacy.it";
import { TERMS_EN } from "@/pages/legal/terms.en";
import { TERMS_IT } from "@/pages/legal/terms.it";

/** The privacy notice or the terms in a language: Italian is the original, English a courtesy translation of it. */
export function legalText(document: "privacy" | "terms", language: Language): string {
  if (document === "privacy") return language === "it" ? PRIVACY_IT : PRIVACY_EN;
  return language === "it" ? TERMS_IT : TERMS_EN;
}
