import { lazy, Suspense, useEffect, useState, type FormEvent } from "react";
import { Lock, MapPin, Save, Search } from "lucide-react";
import { geocodeGym } from "@/features/gyms/api";

const LocationPicker = lazy(() => import("@/features/map/GymMap").then((m) => ({ default: m.LocationPicker })));
import { ImagePicker } from "@/components/ImagePicker";
import { GymAvatar } from "@/components/GymAvatar";
import { useSetGymImage } from "@/features/images/api";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useUpdateGym, type UpdateGymInput } from "@/features/gyms/api";
import { atLeast } from "@/features/staff/roles";
import { TextField } from "@/components/TextField";
import { TextAreaField } from "@/components/Fields";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

export function ManageSettings() {
  const { gym, role } = useManagedGym();
  const update = useUpdateGym(gym.id, gym.slug);
  const setLogo = useSetGymImage(gym, "logo");
  const setCover = useSetGymImage(gym, "cover");
  const toast = useToast();
  const fromGym = (): UpdateGymInput => ({
    name: gym.name, city: gym.city, description: gym.description ?? "", address: gym.address ?? "",
    website: gym.website ?? "", email: gym.email ?? "", phone: gym.phone ?? "",
  });
  const [form, setForm] = useState<UpdateGymInput>(fromGym);
  const [position, setPosition] = useState<{ lat: number; lng: number } | null>(gym.latitude != null && gym.longitude != null ? { lat: gym.latitude, lng: gym.longitude } : null);
  const [geocoding, setGeocoding] = useState(false);
  const [geocodeNote, setGeocodeNote] = useState<string | null>(null);

  async function findFromAddress() {
    setGeocoding(true);
    setGeocodeNote(null);
    try {
      const r = await geocodeGym(gym.id, [form.address, form.city].filter(Boolean).join(", "));
      setPosition({ lat: r.latitude, lng: r.longitude });
      setGeocodeNote(t("Found: {place}. Drag the pin if it isn't exactly on your entrance.", { place: r.displayName }));
    } catch (e) {
      setGeocodeNote(e instanceof ApiError && e.isNotFound ? t("Couldn't find that address. Tap the map to place the pin.") : errorMessage(e));
    } finally {
      setGeocoding(false);
    }
  }
  useEffect(() => setForm(fromGym()), [gym]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!atLeast(role, "ADMIN")) {
    return <EmptyState icon={<Lock />} title={t(t("Admins only"))} body={t(t("Ask a gym admin or owner to change the gym's profile."))} />;
  }

  const err = update.error instanceof ApiError ? update.error : null;
  const set = (k: keyof UpdateGymInput) => (e: { target: { value: string } }) => setForm((f) => ({ ...f, [k]: e.target.value }));

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    const location = position ? { latitude: position.lat, longitude: position.lng } : gym.latitude != null ? { clearLocation: true } : {};
    update.mutate({ ...form, ...location }, {
      onSuccess: () => toast.success(t("Gym profile saved")),
      onError: (error) => { if (!(error instanceof ApiError && error.isValidation)) toast.error(errorMessage(error)); },
    });
  }

  const imageHandlers = (m: typeof setLogo, what: string) => ({
    onPick: (f: File) => m.mutate(f, { onSuccess: () => toast.success(t("{what} updated", { what })), onError: (e) => toast.error(errorMessage(e)) }),
    onRemove: () => m.mutate(null, { onSuccess: () => toast.success(t("{what} removed", { what })), onError: (e) => toast.error(errorMessage(e)) }),
  });

  return (
    <div className="stack">
    <section className="card stack form--wide" aria-labelledby="images-title">
      <h2 id="images-title" className="section__title">{t("Images")}</h2>
      <ImagePicker label={t(t("Logo"))} hint={t(t("Square, at least 256 × 256 px."))} hasImage={!!gym.logoUrl} busy={setLogo.isPending}
        preview={<GymAvatar name={gym.name} logoUrl={gym.logoUrl} size={72} />} {...imageHandlers(setLogo, "Logo")} />
      <ImagePicker label={t(t("Cover photo"))} hint={t(t("Wide photo of your walls, shown at the top of your gym page."))} hasImage={!!gym.coverImageUrl} busy={setCover.isPending}
        preview={gym.coverImageUrl ? <img className="image-picker__cover" src={gym.coverImageUrl} alt="" /> : <span className="image-picker__cover image-picker__cover--empty" aria-hidden />}
        {...imageHandlers(setCover, "Cover")} />
    </section>
    <form className="card form form--wide" onSubmit={onSubmit} noValidate>
      <h2 className="section__title">{t("Gym profile")}</h2>
      <div className="form__grid">
        <TextField label={t(t("Name"))} value={form.name} onChange={set("name")} error={err?.fieldError("name")} />
        <TextField label={t(t("City"))} value={form.city} onChange={set("city")} error={err?.fieldError("city")} />
        <TextField label={t("Address")} value={form.address} onChange={set("address")} error={err?.fieldError("address")} className="form__span" />
        <TextField label={t("Website")} type="url" inputMode="url" placeholder={t("https://")} value={form.website} onChange={set("website")} error={err?.fieldError("website")} />
        <TextField label={t(t("Email"))} type="email" inputMode="email" value={form.email} onChange={set("email")} error={err?.fieldError("email")} />
        <TextField label={t(t("Phone"))} type="tel" inputMode="tel" value={form.phone} onChange={set("phone")} error={err?.fieldError("phone")} />
      </div>
      <div className="editor__section">
        <span className="field__label"><MapPin aria-hidden className="title-icon" /> {t("Location on the map")}</span>
        <span className="field__hint">{t("Climbers find you on the Explore map. Look it up from the address, then drag the pin onto your entrance.")}</span>
        <div className="form__actions">
          <Button variant="secondary" icon={<Search aria-hidden />} loading={geocoding} onClick={findFromAddress} disabled={!form.address && !form.city}>{t(t("Find from address"))}</Button>
          {position && <Button variant="ghost" onClick={() => setPosition(null)}>{t(t("Remove pin"))}</Button>}
        </div>
        {geocodeNote && <p className="field__hint" role="status">{geocodeNote}</p>}
        <Suspense fallback={<div className="gym-map gym-map--picker gym-map--loading" />}>
          <LocationPicker value={position} onChange={setPosition} />
        </Suspense>
        {position && <p className="list__sub">{position.lat.toFixed(5)}, {position.lng.toFixed(5)}</p>}
      </div>
      <TextAreaField label={t(t("Description"))} rows={5} value={form.description} onChange={set("description")} error={err?.fieldError("description")} hint={t(t("What makes your gym special? Shown on your public page."))} />
      <Button type="submit" icon={<Save aria-hidden />} loading={update.isPending}>{t(t("Save changes"))}</Button>
    </form>
    </div>
  );
}
