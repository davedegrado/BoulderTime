import { useState } from "react";
import { ShieldCheck, UserRound } from "lucide-react";
import { useAdminUsers } from "@/features/admin/api";
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

  return (
    <div className="stack">
      <SearchField label={t(t("Search users"))} placeholder={t(t("Name or email"))} value={text} onChange={setText} />
      {users.isPending ? <LoadingState label={t(t("Loading users"))} />
        : users.isError ? <ErrorState error={users.error} onRetry={() => users.refetch()} />
        : users.data.items.length === 0 ? <EmptyState icon={<UserRound />} title={t(t("No users found"))} />
        : (
          <>
            <p className="section__meta">{users.data.total} {users.data.total === 1 ? "user" : "users"}</p>
            <ul className="list">
              {users.data.items.map((u) => (
                <li key={u.id} className="list__row">
                  <Avatar name={u.displayName} url={null} size={40} />
                  <div className="list__main">
                    <p className="list__title">{u.displayName}</p>
                    <p className="list__sub">{u.email} · joined {formatDate(u.createdAt)}{u.staffGyms > 0 && ` · staff at ${u.staffGyms}`}</p>
                  </div>
                  {u.isPlatformAdmin && <Badge tone="dark"><ShieldCheck aria-hidden /> {t(t("Admin"))}</Badge>}
                </li>
              ))}
            </ul>
          </>
        )}
      <p className="section__footnote">{t("Platform admins are granted from the server command line, never from this screen.")}</p>
    </div>
  );
}
