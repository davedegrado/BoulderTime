import { activeLanguage } from "@/i18n/i18n";

/**
 * Italian for the names BoulderTime itself generates in gym data: the preset grading system names and the labels of
 * the colour grade preset. Anything a gym typed in stays exactly as typed — a system called "Parete Rossa" is shown
 * as "Parete Rossa".
 *
 * Kept apart from the interface dictionary because the same word needs a different form there: a grade label is the
 * colour itself ("Rosso"), while hold colours are adjectives agreeing with "prese" ("prese rosse").
 */
const ITALIAN: Record<string, string> = {
  // Preset grading system names
  "Colour": "Colori",
  "Color": "Colori",
  "V-scale": "Scala V",
  // Colour grade preset labels
  "White": "Bianco",
  "Yellow": "Giallo",
  "Green": "Verde",
  "Blue": "Blu",
  "Red": "Rosso",
  "Black": "Nero",
  "Orange": "Arancione",
  "Purple": "Viola",
  "Pink": "Rosa",
  "Grey": "Grigio",
  "Brown": "Marrone",
};

/** Translates a name that came from the database when BoulderTime generated it; leaves anything else untouched. */
export function dataLabel(value: string): string {
  if (activeLanguage() !== "it") return value;
  return ITALIAN[value] ?? ITALIAN[value.trim()] ?? value;
}
