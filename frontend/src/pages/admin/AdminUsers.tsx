import { useState } from "react";
import { ShieldCheck, UserRound } from "lucide-react";
import { useAccountSuspension, useAdminUsers } from "@/features/admin/api";
import { ConfirmButton } from "@/components/ConfirmButton";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { SearchField } from "@/components/SearchField";
import { Avatar } from "@/components/Avatar";
import { Badge } from "@/components/Badge";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useDebounced } from "@/lib/useDebounced";
import { formatDate } from "@/lib/format";
import { t } from "@/i18n/i18n";

export function AdminUsers() {
  const [text, setText] = useState("");
  const q = useDebounced(text.trim());
  const users = useAdminUsers(q);
  const { suspend, reinstate } = useAccountSuspension();
  const toast = useToast();
  const onError = (e: unknown) => toast.error(errorMessage(e));

  return (
    <div className="stack">
      <SearchField label={t("Search users")} placeholder={t("Name or email")} value={text} onChange={setText} />
      {users.isPending ? <LoadingState label={t("Loading users")} />
        : users.isError ? <ErrorState error={users.error} onRetry={() => users.refetch()} />
        : users.data.items.length === 0 ? <EmptyState icon={<UserRound />} title={t("No users found")} />
        : (
          <>
            <p className="section__meta">{users.data.total} {users.data.total === 1 ? "user" : "users"}</p>
            <ul className="list">
              {users.data.items.map((u) => (
                <li key={u.id} className="list__row">
                  <Avatar name={u.displayName} url={null} size={40} />
                  <div className="list__main">
                    <p className="list__title">{u.displayName}</p>
                    <p className="list__sub">{u.email} · {t("joined {date}", { date: formatDate(u.createdAt) })}{u.staffGyms > 0 && ` · ${t("staff at {count}", { count: u.staffGyms })}`}</p>
                  </div>
                  {u.isPlatformAdmin && <Badge tone="dark"><ShieldCheck aria-hidden /> {t("Admin")}</Badge>}
                  {u.isSuspended && <Badge tone="danger">{t("Suspended")}</Badge>}
                  {u.isSuspended ? (
                    <ConfirmButton variant="secondary" confirmLabel={t("Reinstate?")} loading={reinstate.isPending}
                      onConfirm={() => reinstate.mutate(u.id, {
                        onSuccess: () => toast.success(t("{name}'s account is active again", { name: u.displayName })), onError,
                      })}>
                      {t("Reinstate")}
                    </ConfirmButton>
                  ) : !u.isPlatformAdmin && (
                    <ConfirmButton variant="danger" confirmLabel={t("Suspend?")} loading={suspend.isPending}
                      onConfirm={() => suspend.mutate(u.id, {
                        onSuccess: () => toast.success(t("{name}'s account is suspended", { name: u.displayName })), onError,
                      })}>
                      {t("Suspend")}
                    </ConfirmButton>
                  )}
                </li>
              ))}
            </ul>
          </>
        )}
      <p className="section__footnote">{t("Platform admins are granted from the server command line, never from this screen.")}</p>
    </div>
  );
}
