import { useState } from "react";
import { ArrowDown, ArrowUp, RotateCcw } from "lucide-react";
import { DEFAULT_PROFILE_SECTIONS, type ProfileSection, type ProfileSectionKey } from "@/features/climbing/api";
import { useSaveProfileSections } from "@/features/users/api";
import { Toggle } from "@/features/notifications/NotificationBits";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

export function sectionLabel(key: ProfileSectionKey): string {
  switch (key) {
    case "stats": return t("Statistics");
    case "favoriteSector": return t("Favourite sector");
    case "gyms": return t("Gyms");
    case "weekly": return t("Last 12 weeks");
    case "highestGrades": return t("Highest grades");
    case "history": return t("Recent activity");
  }
}

/**
 * Which sections the profile shows and in what order (ADR-061). Saved with the account, so every device shows the
 * same; "Default" goes back to the standard arrangement. Hiding a section only leaves it out of the page: who may see
 * the content is the privacy setting.
 */
export function CustomizeProfile({ sections, onClose }: { sections: ProfileSection[]; onClose: () => void }) {
  const [draft, setDraft] = useState<ProfileSection[]>(sections);
  const save = useSaveProfileSections();
  const toast = useToast();

  // The gyms are a tab of their own, never in the overview: their switch shows or hides the tab, and has no place in
  // the order.
  const ordered = draft.filter((s) => s.key !== "gyms");
  const gyms = draft.find((s) => s.key === "gyms");
  const move = (i: number, by: -1 | 1) => setDraft((d) => {
    const next = d.filter((s) => s.key !== "gyms");
    const [item] = next.splice(i, 1);
    next.splice(i + by, 0, item!);
    return [...next, ...d.filter((s) => s.key === "gyms")];
  });
  const toggle = (key: ProfileSectionKey, visible: boolean) => setDraft((d) => d.map((s) => (s.key === key ? { ...s, visible } : s)));
  const done = (next: ProfileSection[] | null, message: string) =>
    save.mutate(next, { onSuccess: () => { toast.success(message); onClose(); }, onError: (e) => toast.error(errorMessage(e)) });

  return (
    <section className="card stack profile-panel" aria-labelledby="customize-title">
      <h2 id="customize-title" className="section__title">{t("Customise profile")}</h2>
      <p className="field__hint">{t("Show, hide and reorder the sections of your profile. Hiding one doesn't make it private: that is the privacy setting.")}</p>
      <ol className="section-order">
        {ordered.map((s, i) => (
          <li key={s.key} className="section-order__row">
            <div className="section-order__moves">
              <button type="button" className="icon-btn icon-btn--sm" disabled={i === 0} onClick={() => move(i, -1)}
                aria-label={t("Move {name} up", { name: sectionLabel(s.key) })}><ArrowUp aria-hidden /></button>
              <button type="button" className="icon-btn icon-btn--sm" disabled={i === ordered.length - 1} onClick={() => move(i, 1)}
                aria-label={t("Move {name} down", { name: sectionLabel(s.key) })}><ArrowDown aria-hidden /></button>
            </div>
            <Toggle label={sectionLabel(s.key)} checked={s.visible} onChange={(v) => toggle(s.key, v)} />
          </li>
        ))}
      </ol>
      {gyms && <Toggle label={t("Gyms tab")} description={t("The gyms where you climbed, in a tab of their own.")} checked={gyms.visible} onChange={(v) => toggle("gyms", v)} />}
      <div className="row">
        <Button loading={save.isPending} onClick={() => done(draft, t("Profile saved"))}>{t("Save")}</Button>
        <Button variant="ghost" onClick={onClose}>{t("Cancel")}</Button>
        <Button variant="ghost" icon={<RotateCcw aria-hidden />} disabled={save.isPending}
          onClick={() => { setDraft(DEFAULT_PROFILE_SECTIONS); done(null, t("Default sections restored")); }}>{t("Default")}</Button>
      </div>
    </section>
  );
}
