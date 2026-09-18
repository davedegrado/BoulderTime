import { t } from "@/i18n/i18n";
import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { Plus } from "lucide-react";
import { useCreateGym, type CreateGymInput } from "@/features/admin/api";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";

export function CreateGymForm({ initial, onCancel }: { initial?: Partial<CreateGymInput>; onCancel?: () => void }) {
  const create = useCreateGym();
  const toast = useToast();
  const navigate = useNavigate();
  const [form, setForm] = useState<CreateGymInput>({ name: "", city: "", website: "", email: "", ...initial });
  const err = create.error instanceof ApiError ? create.error : null;
  const set = (k: keyof CreateGymInput) => (e: { target: { value: string } }) => setForm((f) => ({ ...f, [k]: e.target.value }));

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    create.mutate(form, {
      onSuccess: (gym) => {
        toast.success(t("{name} created as a draft", { name: gym.name }));
        navigate(`/admin/gyms?q=${encodeURIComponent(gym.name)}`);
        onCancel?.();
      },
      onError: (error) => { if (!(error instanceof ApiError && error.isValidation)) toast.error(errorMessage(error)); },
    });
  }

  return (
    <form className="card form" onSubmit={onSubmit} noValidate>
      <h2 className="section__title">{initial?.candidateId ? "Create gym from suggestion" : "New gym"}</h2>
      <p className="field__hint">Gyms start as drafts. Invite an owner, then publish when they're ready.</p>
      <TextField label="Name" value={form.name} onChange={set("name")} error={err?.fieldError("name")} />
      <TextField label="City" value={form.city} onChange={set("city")} error={err?.fieldError("city")} />
      <TextField label="Website" type="url" inputMode="url" value={form.website ?? ""} onChange={set("website")} error={err?.fieldError("website")} hint="Optional" />
      <TextField label="Email" type="email" inputMode="email" value={form.email ?? ""} onChange={set("email")} error={err?.fieldError("email")} hint="Optional" />
      <div className="form__actions">
        <Button type="submit" icon={<Plus aria-hidden />} loading={create.isPending}>Create draft gym</Button>
        {onCancel && <Button variant="ghost" onClick={onCancel}>Cancel</Button>}
      </div>
    </form>
  );
}
