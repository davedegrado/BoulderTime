import { createContext, Fragment, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { it } from "@/i18n/it";

export type Language = "it" | "en";
export const LANGUAGES: { code: Language; label: string }[] = [
  { code: "it", label: "Italiano" },
  { code: "en", label: "English" },
];

const STORAGE_KEY = "bt:language";

/** The language in use outside React (API headers, date formatting). Kept in sync by the provider. */
let active: Language = detectLanguage();

export const activeLanguage = () => active;
export const localeTag = () => (active === "it" ? "it-IT" : "en-GB");

export function detectLanguage(): Language {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === "it" || stored === "en") return stored;
  } catch { /* storage disabled */ }
  const browser = typeof navigator === "undefined" ? "" : (navigator.language ?? "");
  return browser.toLowerCase().startsWith("en") ? "en" : "it";
}

type Vars = Record<string, string | number>;

function interpolate(text: string, vars?: Vars) {
  if (!vars) return text;
  return text.replace(/\{(\w+)\}/g, (match, name) => (name in vars ? String(vars[name]) : match));
}

/**
 * Translates an English source string. English text doubles as the key, so an untranslated string still reads
 * correctly instead of showing an identifier. Missing Italian entries are reported in development.
 */
export function translate(language: Language, text: string, vars?: Vars): string {
  if (language === "en") return interpolate(text, vars);
  const italian = it[text];
  if (italian === undefined && import.meta.env.DEV) console.warn(`[i18n] missing Italian for: ${text}`);
  return interpolate(italian ?? text, vars);
}

/**
 * Translates using the language in use. It's a plain function, not a hook, so any file can translate without
 * changing its structure; the provider remounts the tree when the language changes, which is rare.
 */
export const t = (text: string, vars?: Vars) => translate(active, text, vars);

/** Picks the singular or plural form by count, which is also available as {count}. */
export const plural = (count: number, one: string, other: string, vars?: Vars) =>
  translate(active, count === 1 ? one : other, { count, ...vars });

interface I18nValue {
  language: Language;
  setLanguage: (l: Language) => void;
}

const I18nContext = createContext<I18nValue | null>(null);

export function I18nProvider({ children, initial }: { children: ReactNode; initial?: Language }) {
  const [language, setLanguageState] = useState<Language>(initial ?? detectLanguage());
  active = language;

  useEffect(() => {
    active = language;
    document.documentElement.lang = language;
    try { localStorage.setItem(STORAGE_KEY, language); } catch { /* storage disabled */ }
  }, [language]);

  const setLanguage = useCallback((next: Language) => setLanguageState(next), []);
  const value = useMemo<I18nValue>(() => ({ language, setLanguage }), [language, setLanguage]);

  // Remounting on language change re-renders every screen with the new texts.
  return <I18nContext.Provider value={value}><Fragment key={language}>{children}</Fragment></I18nContext.Provider>;
}

export function useI18n(): I18nValue {
  const value = useContext(I18nContext);
  if (!value) throw new Error("useI18n must be used inside I18nProvider");
  return value;
}


