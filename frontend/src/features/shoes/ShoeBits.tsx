import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { Plus, Star } from "lucide-react";
import { ordered, useUserShoes, type CatalogShoe, type MyShoe } from "@/features/shoes/api";
import { t } from "@/i18n/i18n";

/** The brand's initials where a model has no picture: never another model's picture in its place. */
const initials = (brand: string) => brand.replace(/[^A-Za-zÀ-ÿ ]/g, "").split(/\s+/).filter(Boolean).map((w) => w[0]).join("").slice(0, 2).toUpperCase() || "?";

export function ShoeThumb({ shoe, size = 48 }: { shoe: CatalogShoe; size?: number }) {
  return shoe.imageUrl
    ? <img className="shoe-thumb" src={shoe.imageUrl} alt="" loading="lazy" style={{ width: size, height: size }} />
    : <span className="shoe-thumb shoe-thumb--none" aria-hidden style={{ width: size, height: size }}>{initials(shoe.brand)}</span>;
}

/**
 * The maker credited under its picture, "Photo: brand", linking to the model's page on its site when there is one.
 * Nothing under the brand's initials: there is no picture to credit.
 */
export function ShoeCredit({ shoe }: { shoe: CatalogShoe }) {
  if (!shoe.imageUrl) return null;
  const label = t("Photo: {brand}", { brand: shoe.brand });
  return shoe.productUrl
    ? <a className="shoe-credit" href={shoe.productUrl} target="_blank" rel="noopener noreferrer"
        aria-label={t("Photo: {brand}, {name} on the maker's site", { brand: shoe.brand, name: shoe.name })}>{label}</a>
    : <span className="shoe-credit">{label}</span>;
}

/** "Main pair": a word with a star, so it is never said by colour alone. */
export function PrimaryTag() {
  return <span className="shoe-primary"><Star aria-hidden />{t("Main pair")}</span>;
}

/** One pair on one line: picture with its credit, brand, model (cut short, never wrapped), the main pair said, and what can be done. */
export function ShoeRow({ shoe, action, primary }: { shoe: CatalogShoe; action?: ReactNode; primary?: boolean }) {
  return (
    <li className="shoe-row">
      <span className="shoe-row__pic">
        <ShoeThumb shoe={shoe} />
        <ShoeCredit shoe={shoe} />
      </span>
      <span className="shoe-row__text">
        <span className="shoe-row__brand">{shoe.brand}</span>
        <span className="shoe-row__name">{shoe.name}</span>
        {(primary || shoe.status === "DISCONTINUED") && (
          <span className="shoe-row__tags">
            {primary && <PrimaryTag />}
            {shoe.status === "DISCONTINUED" && <span className="shoe-row__old">{t("No longer made")}</span>}
          </span>
        )}
      </span>
      {action}
    </li>
  );
}

/**
 * "My shoes" on a profile's overview, after the badges. On one's own: "Manage", or a quiet invitation when there are
 * none yet; on someone else's: read only, and nothing at all when they have none.
 */
export function ProfileShoes({ userId, isMe }: { userId: string; isMe: boolean }) {
  const shoes = useUserShoes(userId).data?.shoes;
  if (!shoes || (shoes.length === 0 && !isMe)) return null;
  return (
    <section className="profile-block" aria-labelledby="shoes-title">
      <div className="profile-block__head">
        <h2 id="shoes-title" className="profile-block__title">{isMe ? t("My shoes") : t("Shoes")}</h2>
        {isMe && shoes.length > 0 && <Link to={`/users/${userId}/shoes`} className="section__link">{t("Manage")}</Link>}
      </div>
      {shoes.length > 0 ? (
        <ul className="shoe-list">{ordered(shoes).map((s: MyShoe) => <ShoeRow key={s.id} shoe={s} primary={s.isPrimary} />)}</ul>
      ) : (
        <Link to={`/users/${userId}/shoes`} className="shoe-invite">
          <span className="shoe-invite__icon" aria-hidden><Plus /></span>
          <span>{t("Add the shoes you climb in")}</span>
        </Link>
      )}
    </section>
  );
}
