import { useEffect, useState, type FormEvent } from "react";
import { LogOut, ShieldCheck } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { useCurrentUser, useUpdateProfile } from "@/features/users/api";
import { ApiError, errorMessage } from "@/lib/apiError";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";
import { ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { Avatar } from "@/components/Avatar";

export function ProfilePage() {
  const { signOut } = useAuth();
  const me = useCurrentUser();
  const update = useUpdateProfile();
  const toast = useToast();
  const [displayName, setDisplayName] = useState("");

  useEffect(() => {
    if (me.data) setDisplayName(me.data.displayName);
  }, [me.data]);

  if (me.isPending) return <LoadingState label="Loading your profile" />;
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
          {user.isPlatformAdmin && <p className="badge"><ShieldCheck aria-hidden /> BoulderTime admin</p>}
        </div>
      </header>

      <section className="section card" aria-labelledby="edit-profile">
        <h2 id="edit-profile" className="section__title">Edit profile</h2>
        <form className="form" onSubmit={onSubmit} noValidate>
          <TextField label="Display name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} error={fieldError} maxLength={40} />
          <TextField label="Email" value={user.email} readOnly disabled hint="Your sign-in email can't be changed here." />
          <Button type="submit" loading={update.isPending} disabled={!dirty}>Save changes</Button>
        </form>
      </section>

      <Button variant="ghost" icon={<LogOut aria-hidden />} onClick={onSignOut}>Sign out</Button>
    </div>
  );
}
