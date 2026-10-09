import { Fragment, type ReactNode } from "react";
import { t } from "@/i18n/i18n";

/**
 * Translates a sentence with links or other elements inside it: "I accept the {terms}" → the element in place of
 * {terms}. The sentence stays whole in the dictionary, so each language orders and spaces it its own way.
 */
export function tRich(text: string, parts: Record<string, ReactNode>): ReactNode {
  return t(text).split(/(\{\w+\})/g).map((piece, i) => {
    const name = /^\{(\w+)\}$/.exec(piece)?.[1];
    return <Fragment key={i}>{name && name in parts ? parts[name] : piece}</Fragment>;
  });
}
