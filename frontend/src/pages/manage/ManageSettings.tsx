import { useEffect, useState, type FormEvent } from "react";
import { Lock, Save } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useUpdateGym, type UpdateGymInput } from "@/features/gyms/api";
import { atLeast } from "@/features/staff/roles";
import { TextField } from "@/components/TextField";
import { TextAreaField } from "@/components/Fields";
import { Button } from "@/components/Button";
import { EmptyState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";

export function ManageSettings() {
  const { gym, role } = useManagedGym();
  const update = useUpdateGym(gym.id, gym.slug);
  const toast = useToast();
  const fromGym = (): UpdateGymInput => ({
    name: gym.name, city: gym.city, description: gym.description ?? "", address: gym.address ?? "",
    website: gym.website ?? "", email: gym.email ?? "", phone: gym.phone ?? "",
  });
  const [form, setForm] = useState<UpdateGymInput>(fromGym);
  useEffect(() => setForm(fromGym()), [gym]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!atLeast(role, "ADMIN")) {
    return <EmptyState icon={<Lock />} title="Admins only" body="Ask a gym admin or owner to change the gym's profile." />;
  }

  const err = update.error instanceof ApiError ? update.error : null;
  const set = (k: keyof UpdateGymInput) => (e: { target: { value: string } }) => setForm((f) => ({ ...f, [k]: e.target.value }));

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    update.mutate(form, {
      onSuccess: () => toast.success("Gym profile saved"),
      onError: (error) => { if (!(error instanceof ApiError && error.isValidation)) toast.error(errorMessage(error)); },
    });
  }

  return (
    <form className="card form form--wide" onSubmit={onSubmit} noValidate>
      <h2 className="section__title">Gym profile</h2>
      <div className="form__grid">
        <TextField label="Name" value={form.name} onChange={set("name")} error={err?.fieldError("name")} />
        <TextField label="City" value={form.city} onChange={set("city")} error={err?.fieldError("city")} />
        <TextField label="Address" value={form.address} onChange={set("address")} error={err?.fieldError("address")} className="form__span" />
        <TextField label="Website" type="url" inputMode="url" placeholder="https://" value={form.website} onChange={set("website")} error={err?.fieldError("website")} />
        <TextField label="Email" type="email" inputMode="email" value={form.email} onChange={set("email")} error={err?.fieldError("email")} />
        <TextField label="Phone" type="tel" inputMode="tel" value={form.phone} onChange={set("phone")} error={err?.fieldError("phone")} />
      </div>
      <TextAreaField label="Description" rows={5} value={form.description} onChange={set("description")} error={err?.fieldError("description")} hint="What makes your gym special? Shown on your public page." />
      <p className="field__hint">Logo and cover photo uploads arrive with image storage in the next phase.</p>
      <Button type="submit" icon={<Save aria-hidden />} loading={update.isPending}>Save changes</Button>
    </form>
  );
}
