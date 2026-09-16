import { Check, MailOpen, X } from "lucide-react";
import { useMyInvitations, useRespondToInvitation } from "@/features/staff/api";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { relativeDays, roleLabel } from "@/lib/format";

/** Shown on Home when the signed-in user has open staff invitations. Renders nothing otherwise. */
export function InvitationsCard({ count }: { count: number }) {
  const invitations = useMyInvitations(count > 0);
  const respond = useRespondToInvitation();
  const toast = useToast();

  if (count === 0 || !invitations.data?.length) return null;

  return (
    <section className="invites" aria-labelledby="invites-title">
      <h2 id="invites-title" className="invites__title"><MailOpen aria-hidden /> You've been invited</h2>
      <ul className="invites__list">
        {invitations.data.map((inv) => {
          const busy = respond.isPending && respond.variables?.id === inv.id;
          return (
            <li key={inv.id} className="invites__item">
              <p className="invites__text">
                <strong>{inv.invitedBy}</strong> invited you to join <strong>{inv.gymName}</strong> ({inv.gymCity}) as <strong>{roleLabel[inv.role]}</strong>.
              </p>
              <p className="invites__meta">Expires {relativeDays(inv.expiresAt)}</p>
              <div className="invites__actions">
                <Button
                  icon={<Check aria-hidden />}
                  loading={busy && respond.variables?.accept}
                  disabled={respond.isPending}
                  onClick={() => respond.mutate({ id: inv.id, accept: true }, {
                    onSuccess: () => toast.success(`You're now ${roleLabel[inv.role].toLowerCase()} at ${inv.gymName}`),
                    onError: (e) => toast.error(errorMessage(e)),
                  })}
                >Accept</Button>
                <Button
                  variant="on-dark"
                  icon={<X aria-hidden />}
                  loading={busy && !respond.variables?.accept}
                  disabled={respond.isPending}
                  onClick={() => respond.mutate({ id: inv.id, accept: false }, { onError: (e) => toast.error(errorMessage(e)) })}
                >Decline</Button>
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
