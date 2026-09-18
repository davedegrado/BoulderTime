import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { ChevronRight, LogOut, Plus, ShieldCheck } from "lucide-react";
import { GymAvatar } from "@/components/GymAvatar";
import { roleLabel } from "@/lib/format";
import { useAuth } from "@/auth/AuthProvider";
import { useCurrentUser, useUpdateProfile } from "@/features/users/api";
import { ApiError, errorMessage } from "@/lib/apiError";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";
import { SelectField } from "@/components/Fields";
import { ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { Avatar } from "@/components/Avatar";
import { ImagePicker } from "@/components/ImagePicker";
import { useSetAvatar } from "@/features/images/api";
import { LANGUAGES, t, useI18n, type Language } from "@/i18n/i18n";

export function ProfilePage() {
  const { signOut } = useAuth();
  const me = useCurrentUser();
  const update = useUpdateProfile();
  const { language, setLanguage } = useI18n();
  const setAvatar = useSetAvatar();
  const toast = useToast();
  const [displayName, setDisplayName] = useState("");

  useEffect(() => {
    if (me.data) setDisplayName(me.data.displayName);
  }, [me.data]);

  if (me.isPending) return <LoadingState label={t("Loading your profile")} />;
  if (me.isError) return <ErrorState error={me.error} onRetry={() => me.refetch()} />;

  const user = me.data;
  const fieldError = update.error instanceof ApiError ? update.error.fieldError("displayName") : undefined;
  const dirty = displayName.trim() !== user.displayName;

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    update.mutate(
      { displayName: displayName.trim() },
      {
        onSuccess: () => toast.success("Profile saved"),
        onError: (err) => {
          if (!(err instanceof ApiError && err.isValidation)) toast.error(errorMessage(err));
        },
      },
    );
  }

  async function onSignOut() {
    try {
      await signOut();
    } catch (err) {
      toast.error(errorMessage(err));
    }
  }

  return (
    <div className="page page--narrow">
      <header className="profile-head">
        <Avatar name={user.displayName} url={user.avatarUrl} size={72} />
        <div>
          <h1 className="page__title">{user.displayName}</h1>
          <p className="page__subtitle">Climbing since {new Date(user.createdAt).toLocaleDateString(undefined, { month: "long", year: "numeric" })}</p>
          {user.isPlatformAdmin && <p className="badge"><ShieldCheck aria-hidden /> {t("BoulderTime admin")}</p>}
        </div>
      </header>

      <section className="section card" aria-labelledby="edit-profile">
        <h2 id="edit-profile" className="section__title">{t("Edit profile")}</h2>
        <ImagePicker label={t("Profile photo")} hint={t("Square crop, shown on comments and leaderboards.")} hasImage={!!user.avatarUrl} busy={setAvatar.isPending}
          preview={<Avatar name={user.displayName} url={user.avatarUrl} size={72} />}
          onPick={(f) => setAvatar.mutate(f, { onSuccess: () => toast.success("Photo updated"), onError: (e) => toast.error(errorMessage(e)) })}
          onRemove={() => setAvatar.mutate(null, { onSuccess: () => toast.success("Photo removed"), onError: (e) => toast.error(errorMessage(e)) })} />
        <form className="form" onSubmit={onSubmit} noValidate>
          <TextField label={t("Display name")} value={displayName} onChange={(e) => setDisplayName(e.target.value)} error={fieldError} maxLength={40} />
          <TextField label={t("Email")} value={user.email} readOnly disabled hint={t("Your sign-in email can't be changed here.")} />
          <SelectField label={t("Language")} value={language}
            options={LANGUAGES.map((l) => ({ value: l.code, label: l.label }))}
            onChange={(e) => {
              const next = e.target.value as Language;
              setLanguage(next); // immediate on this device
              update.mutate({ displayName: user.displayName, language: next }, { onError: (err) => toast.error(errorMessage(err)) });
            }} />
          <p className="field__hint">{t("Also used for the notifications you receive.")}</p>
          <Button type="submit" loading={update.isPending} disabled={!dirty}>{t("Save changes")}</Button>
        </form>
      </section>

      {(user.staffGyms.length > 0 || user.isPlatformAdmin) && (
        <section className="section" aria-labelledby="manage-title">
          <h2 id="manage-title" className="section__title">{t("Manage")}</h2>
          <ul className="list">
            {user.staffGyms.map((g) => (
              <li key={g.gymId}>
                <Link to={`/manage/${g.slug}`} className="list__row list__row--link">
                  <GymAvatar name={g.name} logoUrl={g.logoUrl} size={40} />
                  <div className="list__main">
                    <p className="list__title">{g.name}</p>
                    <p className="list__sub">{roleLabel[g.role]}</p>
                  </div>
                  <ChevronRight className="list__chevron" aria-hidden />
                </Link>
              </li>
            ))}
            {user.isPlatformAdmin && (
              <li>
                <Link to="/admin" className="list__row list__row--link">
                  <span className="list__badge-icon"><ShieldCheck aria-hidden /></span>
                  <div className="list__main"><p className="list__title">{t("BoulderTime admin")}</p></div>
                  <ChevronRight className="list__chevron" aria-hidden />
                </Link>
              </li>
            )}
          </ul>
        </section>
      )}

      <div className="form__actions">
        <Link to={`/users/${user.id}`} className="btn btn--secondary"><span>{t("View public profile")}</span></Link>
        <Link to="/activity" className="btn btn--secondary"><span>{t("Your activity")}</span></Link>
      </div>
      <Link to="/gyms/suggest" className="btn btn--ghost"><Plus aria-hidden /><span>{t("Suggest a gym")}</span></Link>
      <Button variant="ghost" icon={<LogOut aria-hidden />} onClick={onSignOut}>{t("Sign out")}</Button>
    </div>
  );
}
