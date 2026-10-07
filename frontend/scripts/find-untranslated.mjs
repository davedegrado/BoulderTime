// Finds English that reaches the screen without going through t().
//
// This walks the real TypeScript/JSX syntax tree rather than matching text: a regex cannot tell JSX text from a
// generic like useState<string>, and the version that tried reported 487 things, nearly all of them code. The
// parser knows exactly which strings are JSX text and which are attribute values, so what it reports is real.
//
// Run: node scripts/find-untranslated.mjs   — exits non-zero if it finds any, so a test can call it.
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import ts from "typescript";

const SRC = new URL("../src", import.meta.url).pathname;

/** Attributes whose value a person reads. Everything else may hold ids, classes and urls. */
const SHOWN_ATTRS = new Set([
  "aria-label", "aria-description", "alt", "placeholder", "title",
  "label", "hint", "body", "confirmLabel", "submitLabel", "summary", "emptyLabel",
]);

/** Brand names and attributions, which stay as they are in every language. */
const ALLOWED = new Set(["BoulderTime", "OpenStreetMap", "Instagram", "Facebook", "YouTube", "Vimeo", "TikTok", "Google", "Apple", "GPS", "PWA"]);

function files(dir) {
  return readdirSync(dir).flatMap((name) => {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) return name === "test" ? [] : files(p);
    return /\.tsx$/.test(name) && !/\.test\.tsx$/.test(name) ? [p] : [];
  });
}

/** Real copy: has a word of three letters or more, and is not just a brand name or punctuation. */
function isCopy(text) {
  const clean = text.replace(/\s+/g, " ").trim();
  if (!/[A-Za-z]{3}/.test(clean)) return false;
  if (/^(https?:\/\/|\/|@|#)/.test(clean)) return false;   // a url shown as an example, not copy
  const words = clean.split(/[^A-Za-z]+/).filter((w) => w.length > 2);
  return words.length > 0 && !words.every((w) => ALLOWED.has(w));
}

const found = [];
for (const file of files(SRC)) {
  const source = readFileSync(file, "utf8");
  const sf = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const short = file.replace(SRC, "src");
  const at = (node) => sf.getLineAndCharacterOfPosition(node.getStart(sf)).line + 1;

  const visit = (node) => {
    // Text sitting between tags: <p>Mark as completed</p>, including text split over several lines.
    if (ts.isJsxText(node) && isCopy(node.text)) {
      found.push({ file: short, line: at(node), text: node.text.replace(/\s+/g, " ").trim() });
    }
    // An attribute a person reads, given a plain string: aria-label="Clear search".
    if (ts.isJsxAttribute(node) && node.initializer) {
      const name = node.name.getText(sf);
      const init = node.initializer;
      const literal = ts.isStringLiteral(init) ? init
        : ts.isJsxExpression(init) && init.expression && ts.isStringLiteral(init.expression) ? init.expression
        : null;
      if (literal && SHOWN_ATTRS.has(name) && isCopy(literal.text)) {
        found.push({ file: short, line: at(node), text: literal.text });
      }
    }
    // toast.success("Saved") and friends.
    if (ts.isCallExpression(node) && ts.isPropertyAccessExpression(node.expression)
        && node.expression.expression.getText(sf) === "toast") {
      const arg = node.arguments[0];
      if (arg && ts.isStringLiteral(arg) && isCopy(arg.text)) found.push({ file: short, line: at(node), text: arg.text });
    }
    ts.forEachChild(node, visit);
  };
  visit(sf);
}

found.sort((a, b) => a.file.localeCompare(b.file) || a.line - b.line);
for (const f of found) console.log(`${f.file}:${f.line}  ${JSON.stringify(f.text)}`);
console.log(`\n${found.length} untranslated`);
process.exit(found.length === 0 ? 0 : 1);
