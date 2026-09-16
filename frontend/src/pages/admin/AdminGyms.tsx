import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Building2, MailPlus, Plus, Settings2 } from "lucide-react";
import { useAdminGyms, useInviteOwner, useSetGymStatus, type AdminGym } from "@/features/admin/api";
import { CreateGymForm } from "@/pages/admin/CreateGymForm";
import { SearchField } from "@/components/SearchField";
import { SelectField } from "@/components/Fields";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { Badge } from "@/components/Badge";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { useDebounced } from "@/lib/useDebounced";
import { gymStatusLabel, type GymStatus } from "@/lib/format";

const STATUS_OPTIONS = [
  { value: "", label: "All statuses" },
  { value: "DRAFT", label: "Draft" },
  { value: "ACTIVE", label: "Active" },
  { value: "ARCHIVED", label: "Archived" },
];

export function AdminGyms() {
  const [params, setParams] = useSearchParams();
  const [text, setText] = useState(params.get("q") ?? "");
  const status = (params.get("status") ?? "") as GymStatus | "";
  const q = useDebounced(text.trim());
  const gyms = useAdminGyms(q, status);
  const [creating, setCreating] = useState(false);

  return (
    <div className="stack">
      {creating ? <CreateGymForm onCancel={() => setCreating(false)} /> : (
        <Button variant="secondary" icon={<Plus aria-hidden />} onClick={() => setCreating(true)}>New gym</Button>
      )}
      <div className="filters">
        <SearchField label="Search gyms" placeholder="Name or city" value={text} onChange={setText} />
        <SelectField label="Status" hideLabel value={status} options={STATUS_OPTIONS}
          onChange={(e) => setParams((p) => { e.target.value ? p.set("status", e.target.value) : p.delete("status"); return p; }, { replace: true })} />
      </div>
      {gyms.isPending ? <LoadingState label="Loading gyms" />
        : gyms.isError ? <ErrorState error={gyms.error} onRetry={() => gyms.refetch()} />
        : gyms.data.items.length === 0 ? <EmptyState icon={<Building2 />} title="No gyms found" />
        : <div className="stack">{gyms.data.items.map((g) => <AdminGymCard key={g.id} gym={g} />)}</div>}
    </div>
  );
}

function AdminGymCard({ gym }: { gym: AdminGym }) {
  const setStatus = useSetGymStatus();
  const inviteOwner = useInviteOwner();
  const toast = useToast();
  const [inviting, setInviting] = useState(false);
  const [email, setEmail] = useState("");
  const inviteError = inviteOwner.error instanceof ApiError ? inviteOwner.error.fieldError("email") : undefined;

  function onInvite(e: FormEvent) {
    e.preventDefault();
    inviteOwner.mutate({ gymId: gym.id, email: email.trim() }, {
      onSuccess: () => { toast.success(`Owner invitation sent to ${email.trim()}`); setEmail(""); setInviting(false); },
      onError: (err) => { if (!(err instanceof ApiError && err.isValidation)) toast.error(errorMessage(err)); },
    });
  }

  return (
    <article className="card admin-gym">
      <header className="candidate__head">
        <div>
          <h3 className="list__title">{gym.name}</h3>
          <p className="list__sub">{gym.city} · {gym.staffCount} staff · {gym.pendingInvitations} open invitations</p>
        </div>
        <Badge tone={gym.status === "ACTIVE" ? "success" : gym.status === "DRAFT" ? "orange" : "neutral"}>{gymStatusLabel[gym.status]}</Badge>
      </header>
      {gym.ownerCount === 0 && <p className="notice notice--inline">No owner yet — invite one so the gym can manage itself.</p>}
      <div className="form__actions">
        <SelectField label={`Status of ${gym.name}`} hideLabel value={gym.status} disabled={setStatus.isPending}
          options={STATUS_OPTIONS.slice(1)}
          onChange={(e) => setStatus.mutate({ gymId: gym.id, status: e.target.value as GymStatus }, {
            onSuccess: (g) => toast.success(`${g.name} is now ${gymStatusLabel[g.status].toLowerCase()}`),
            onError: (err) => toast.error(errorMessage(err)),
          })} />
        <Button variant="secondary" icon={<MailPlus aria-hidden />} onClick={() => setInviting((v) => !v)}>Invite owner</Button>
        <Link to={`/manage/${gym.slug}`} className="btn btn--ghost"><Settings2 aria-hidden /><span>Manage</span></Link>
      </div>
      {inviting && (
        <form className="inline-form" onSubmit={onInvite} noValidate>
          <TextField label="Owner's email" type="email" inputMode="email" value={email} onChange={(e) => setEmail(e.target.value)} error={inviteError} autoFocus />
          <Button type="submit" loading={inviteOwner.isPending} disabled={!email.trim()}>Send</Button>
        </form>
      )}
    </article>
  );
}
