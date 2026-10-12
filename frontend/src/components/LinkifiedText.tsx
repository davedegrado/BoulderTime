import { Fragment } from "react";

/** http(s) addresses in running text. Anything else (javascript:, mailto:, bare domains) stays plain text. */
const URL_IN_TEXT = /\bhttps?:\/\/[^\s<>"']+/gi;
/** Punctuation that ends a sentence rather than the address: "see https://x.it." links to https://x.it. */
const TRAILING = /[.,;:!?)\]}]+$/;

export interface TextPart { text: string; href?: string }

/**
 * Splits text into plain parts and web addresses. Only http and https become links, and only when they parse as a
 * real address: text written by gym staff is never turned into markup, so nothing in it can run as code.
 */
export function linkParts(text: string): TextPart[] {
  const parts: TextPart[] = [];
  let last = 0;
  for (const match of text.matchAll(URL_IN_TEXT)) {
    const start = match.index ?? 0;
    const raw = match[0];
    const address = raw.replace(TRAILING, "");
    let href: string | null = null;
    try {
      const url = new URL(address);
      if (url.protocol === "http:" || url.protocol === "https:") href = url.href;
    } catch { /* not an address after all: left as text */ }
    if (!href) continue;
    if (start > last) parts.push({ text: text.slice(last, start) });
    parts.push({ text: address, href });
    last = start + address.length;
  }
  if (last < text.length) parts.push({ text: text.slice(last) });
  return parts;
}

/** Text with its web addresses tappable, opening outside the app. */
export function LinkifiedText({ text }: { text: string }) {
  return (
    <>
      {linkParts(text).map((p, i) => p.href
        ? <a key={i} href={p.href} target="_blank" rel="noopener noreferrer nofollow" className="text-link">{p.text}</a>
        : <Fragment key={i}>{p.text}</Fragment>)}
    </>
  );
}
