import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { LogOut, MailPlus, Trash2, UserMinus } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useChangeRole, useGymInvitations, useInvite, useRemoveStaff, useRevokeInvitation, useStaff, type StaffMember } from "@/features/staff/api";
import { useCurrentUser } from "@/features/users/api";
import { atLeast, canGrant, canManage, grantableRoles } from "@/features/staff/roles";
import { TextField } from "@/components/TextField";
import { SelectField } from "@/components/Fields";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { Avatar } from "@/components/Avatar";
import { Badge } from "@/components/Badge";
import { ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { relativeDays, roleLabel, type GymRole } from "@/lib/format";
import { t } from "@/i18n/i18n";

export function ManageStaff() {
  const { gym, role } = useManagedGym();
  const staff = useStaff(gym.id);
  const invitations = useGymInvitations(gym.id);
  const me = useCurrentUser();
  const canInvite = atLeast(role, "ADMIN");

  return (
    <div className="stack">
      {canInvite && <InviteForm gymId={gym.id} actorRole={role} />}

      <section className="section" aria-labelledby="members-title">
        <h2 id="members-title" className="section__title">{t("Team")}</h2>
        {staff.isPending ? <LoadingState label={t(t("Loading staff"))} />
          : staff.isError ? <ErrorState error={staff.error} onRetry={() => staff.refetch()} />
          : (
            <ul className="list">
              {staff.data.map((m) => <MemberRow key={m.userId} member={m} gymId={gym.id} gymSlug={gym.slug} actorRole={role} isMe={m.userId === me.data?.id} />)}
            </ul>
          )}
      </section>

      {invitations.data && invitations.data.length > 0 && (
        <section className="section" aria-labelledby="invites-title">
          <h2 id="invites-title" className="section__title">{t("Open invitations")}</h2>
          <ul className="list">
            {invitations.data.map((inv) => <InvitationRow key={inv.id} gymId={gym.id} id={inv.id} email={inv.email} role={inv.role} expiresAt={inv.expiresAt} invitedBy={inv.invitedBy} canRevoke={canGrant(role, inv.role)} />)}
          </ul>
        </section>
      )}
    </div>
  );
}

function InviteForm({ gymId, actorRole }: { gymId: string; actorRole: GymRole }) {
  const invite = useInvite(gymId);
  const toast = useToast();
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<GymRole>("STAFF");
  const err = invite.error instanceof ApiError ? invite.error : null;

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    invite.mutate({ email: email.trim(), role }, {
      onSuccess: (inv) => { setEmail(""); toast.success(t("Invitation sent to {email}", { email: inv.email })); },
      onError: (error) => { if (!(error instanceof ApiError && error.isValidation)) toast.error(errorMessage(error)); },
    });
  }

  return (
    <form className="card form" onSubmit={onSubmit} noValidate>
      <h2 className="section__title">{t("Invite someone")}</h2>
      <p className="field__hint">{t("They'll see the invitation in BoulderTime after signing in with this email. It expires in 7 days.")}</p>
      <TextField label={t(t("Email"))} type="email" inputMode="email" autoComplete="off" value={email} onChange={(e) => setEmail(e.target.value)} error={err?.fieldError("email")} />
      <SelectField label={t(t("Role"))} value={role} onChange={(e) => setRole(e.target.value as GymRole)} options={grantableRoles(actorRole).map((r) => ({ value: r, label: roleLabel[r] }))} />
      <Button type="submit" icon={<MailPlus aria-hidden />} loading={invite.isPending} disabled={!email.trim()}>{t(t("Send invitation"))}</Button>
    </form>
  );
}

function MemberRow({ member, gymId, gymSlug, actorRole, isMe }: { member: StaffMember; gymId: string; gymSlug: string; actorRole: GymRole; isMe: boolean }) {
  const changeRole = useChangeRole(gymId);
  const remove = useRemoveStaff(gymId);
  const toast = useToast();
  const navigate = useNavigate();
  const manageable = canManage(actorRole, member.role);
  const options = grantableRoles(actorRole);

  return (
    <li className="list__row list__row--wrap">
      <Avatar name={member.displayName} url={member.avatarUrl} size={40} />
      <div className="list__main">
        <p className="list__title">{member.displayName}{isMe && <span className="list__you"> · you</span>}</p>
        <p className="list__sub">{member.email}</p>
      </div>
      <div className="list__actions">
        {manageable && options.includes(member.role) ? (
          <SelectField
            label={t("Role for {name}", { name: member.displayName })}
            hideLabel
            value={member.role}
            disabled={changeRole.isPending}
            onChange={(e) => changeRole.mutate({ userId: member.userId, role: e.target.value as GymRole }, {
              onSuccess: (m) => toast.success(t("{name} is now {role}", { name: m.displayName, role: roleLabel[m.role] })),
              onError: (err) => toast.error(errorMessage(err)),
            })}
            options={options.map((r) => ({ value: r, label: roleLabel[r] }))}
          />
        ) : (
          <Badge tone={member.role === "OWNER" ? "orange" : "neutral"}>{roleLabel[member.role]}</Badge>
        )}
        {isMe ? (
          <ConfirmButton icon={<LogOut aria-hidden />} confirmLabel={t(t("Tap to leave"))} loading={remove.isPending}
            onConfirm={() => remove.mutate(member.userId, {
              onSuccess: () => { toast.success(t("You left the staff")); navigate(`/gyms/${gymSlug}`); },
              onError: (err) => toast.error(errorMessage(err)),
            })}>{t(t("Leave"))}</ConfirmButton>
        ) : manageable && (
          <ConfirmButton icon={<UserMinus aria-hidden />} confirmLabel={t(t("Tap to remove"))} loading={remove.isPending}
            onConfirm={() => remove.mutate(member.userId, {
              onSuccess: () => toast.success(t("{name} removed", { name: member.displayName })),
              onError: (err) => toast.error(errorMessage(err)),
            })}>{t(t("Remove"))}</ConfirmButton>
        )}
      </div>
    </li>
  );
}

interface InvitationRowProps { gymId: string; id: string; email: string; role: GymRole; expiresAt: string; invitedBy: string; canRevoke: boolean }

function InvitationRow({ gymId, id, email, role, expiresAt, invitedBy, canRevoke }: InvitationRowProps) {
  const revoke = useRevokeInvitation(gymId);
  const toast = useToast();
  return (
    <li className="list__row list__row--wrap">
      <MailPlus className="list__icon" aria-hidden />
      <div className="list__main">
        <p className="list__title">{email}</p>
        <p className="list__sub">{roleLabel[role]} · by {invitedBy} · expires {relativeDays(expiresAt)}</p>
      </div>
      {canRevoke && (
        <ConfirmButton icon={<Trash2 aria-hidden />} confirmLabel={t(t("Tap to revoke"))} loading={revoke.isPending}
          onConfirm={() => revoke.mutate(id, { onSuccess: () => toast.success(t("Invitation revoked")), onError: (err) => toast.error(errorMessage(err)) })}>
          Revoke
        </ConfirmButton>
      )}
    </li>
  );
}
