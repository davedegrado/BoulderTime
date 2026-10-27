import { useState } from "react";
import { useParams } from "react-router-dom";
import { Check, Footprints, Plus, Star, X } from "lucide-react";
import { DetailHeader } from "@/components/DetailHeader";
import { SearchField } from "@/components/SearchField";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { useProfile } from "@/features/climbing/api";
import { ordered, SHOE_BRANDS, useEditShoes, useShoeCatalog, useUserShoes } from "@/features/shoes/api";
import { ShoeRow } from "@/features/shoes/ShoeBits";
import { useDebounced } from "@/lib/useDebounced";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/**
 * A person's shoes. On one's own: the pairs chosen, each to mark as the main pair or take away, and the catalogue to
 * search by name and brand and add from. Someone else's: the same list, read only. Nothing is shown as saved before
 * the server says so.
 */
export function ShoesPage() {
  const { id } = useParams();
  const profile = useProfile(id);
  const shoes = useUserShoes(id);
  const isMe = profile.data?.isMe ?? false;
  const head = <DetailHeader title={isMe ? t("My shoes") : profile.data ? t("{name}'s shoes", { name: profile.data.displayName }) : t("Shoes")} titleAlways backTo={`/users/${id}`} />;

  if (shoes.isPending || profile.isPending) return <div className="page page--flush">{head}<LoadingState label={t("Loading shoes")} /></div>;
  if (shoes.isError) return <div className="page page--flush">{head}<div className="page__pad"><ErrorState error={shoes.error} onRetry={() => shoes.refetch()} /></div></div>;
  const mine = ordered(shoes.data.shoes);

  return (
    <div className="page page--flush shoes-page">
      {head}
      <div className="page__pad stack">
        {isMe ? <MyShoes userId={id!} /> : mine.length === 0
          ? <EmptyState icon={<Footprints />} title={t("No shoes to show")} />
          : <ul className="shoe-list">{mine.map((s) => <ShoeRow key={s.id} shoe={s} primary={s.isPrimary} />)}</ul>}
      </div>
    </div>
  );
}

function MyShoes({ userId }: { userId: string }) {
  const shoes = ordered(useUserShoes(userId).data?.shoes ?? []);
  const edit = useEditShoes(userId);
  const toast = useToast();
  const fail = (e: unknown) => toast.error(errorMessage(e));
  const chosen = new Set(shoes.map((s) => s.id));
  const busy = edit.add.isPending || edit.remove.isPending || edit.primary.isPending;

  return (
    <>
      <section className="profile-block" aria-labelledby="my-shoes-title">
        <h2 id="my-shoes-title" className="profile-block__title">{t("Your pairs")}</h2>
        {shoes.length === 0 ? (
          <p className="field__hint">{t("None yet: find yours in the catalogue below.")}</p>
        ) : (
          <ul className="shoe-list">
            {shoes.map((s) => (
              <ShoeRow key={s.id} shoe={s} primary={s.isPrimary} action={(
                <span className="shoe-row__actions">
                  <button type="button" className={`icon-btn shoe-star ${s.isPrimary ? "is-on" : ""}`} aria-pressed={s.isPrimary} disabled={busy}
                    aria-label={s.isPrimary ? t("{name}: no longer the main pair", { name: s.name }) : t("Make {name} the main pair", { name: s.name })}
                    onClick={() => edit.primary.mutate({ shoeId: s.id, on: !s.isPrimary }, { onError: fail })}>
                    <Star aria-hidden />
                  </button>
                  <button type="button" className="icon-btn" disabled={busy} aria-label={t("Remove {name}", { name: s.name })}
                    onClick={() => edit.remove.mutate(s.id, { onError: fail })}>
                    <X aria-hidden />
                  </button>
                </span>
              )} />
            ))}
          </ul>
        )}
        {shoes.length > 0 && !shoes.some((s) => s.isPrimary) && <p className="field__hint">{t("Tap the star to mark your main pair.")}</p>}
      </section>
      <Catalog chosen={chosen} busy={busy} onAdd={(shoeId) => edit.add.mutate(shoeId, { onError: fail })} onRemove={(shoeId) => edit.remove.mutate(shoeId, { onError: fail })} />
    </>
  );
}

/** Search by name and brand, case and extra spaces ignored by the server; a page at a time, never the whole catalogue. */
function Catalog({ chosen, busy, onAdd, onRemove }: { chosen: Set<string>; busy: boolean; onAdd: (id: string) => void; onRemove: (id: string) => void }) {
  const [text, setText] = useState("");
  const [brand, setBrand] = useState("");
  const q = useDebounced(text.trim().replace(/\s+/g, " "), 250);
  const catalog = useShoeCatalog(q, brand);
  const items = catalog.data?.items ?? [];

  return (
    <section className="profile-block" aria-labelledby="catalog-title">
      <h2 id="catalog-title" className="profile-block__title">{t("Add from the catalogue")}</h2>
      <SearchField value={text} onChange={setText} label={t("Search shoes")} placeholder={t("Model or brand")} />
      <div className="chips chips--light" role="radiogroup" aria-label={t("Brand")}>
        {["", ...SHOE_BRANDS].map((b) => (
          <button key={b || "all"} type="button" role="radio" aria-checked={brand === b} className="chip" onClick={() => setBrand(b)}>{b || t("All")}</button>
        ))}
      </div>
      {catalog.isPending ? <LoadingState label={t("Loading catalogue")} />
        : catalog.isError ? <ErrorState error={catalog.error} onRetry={() => catalog.refetch()} />
        : items.length === 0 ? <EmptyState icon={<Footprints />} title={q ? t("No model matches “{q}”", { q }) : t("No models for this brand yet")} />
        : (
          <ul className="shoe-list shoe-list--catalog">
            {items.map((s) => {
              const on = chosen.has(s.id);
              return (
                <ShoeRow key={s.id} shoe={s} action={(
                  <button type="button" className={`shoe-add ${on ? "is-on" : ""}`} aria-pressed={on} disabled={busy}
                    aria-label={on ? t("Remove {name}", { name: s.name }) : t("Add {name}", { name: s.name })}
                    onClick={() => (on ? onRemove(s.id) : onAdd(s.id))}>
                    {on ? <><Check aria-hidden />{t("Added")}</> : <><Plus aria-hidden />{t("Add")}</>}
                  </button>
                )} />
              );
            })}
          </ul>
        )}
      {catalog.data && catalog.data.total > items.length && <p className="field__hint">{t("Showing {shown} of {total}: search to narrow them down.", { shown: items.length, total: catalog.data.total })}</p>}
    </section>
  );
}
