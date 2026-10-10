import { useState, type FormEvent } from "react";
import { Check, Pencil, Plus, Tag, Trash2, X } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useCreateTag, useDeleteTag, useGymTags, useRenameTag, type GymTag } from "@/features/boulders/tags";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";
import { plural, t } from "@/i18n/i18n";

/**
 * The gym's own tags ("Placca", "Dinamico"…). Climbers filter by them; staff put them on boulders in the editor.
 * "Placca", "placca" and "Placcà" are one tag: the server refuses the second and names the first.
 */
export function ManageTags() {
  const { gym } = useManagedGym();
  const tags = useGymTags(gym.id);
  const create = useCreateTag(gym.id);
  const toast = useToast();
  const [name, setName] = useState("");

  function onCreate(e: FormEvent) {
    e.preventDefault();
    create.mutate(name.trim(), {
      onSuccess: (tag) => { setName(""); toast.success(t("Tag “{name}” added", { name: tag.name })); },
      onError: (err) => { if (!(err instanceof ApiError && err.isValidation)) toast.error(errorMessage(err)); },
    });
  }
  const createError = create.error instanceof ApiError ? create.error.fieldError("name") : undefined;

  return (
    <div className="stack">
      <p className="field__hint">{t("Describe your boulders in your own words: style, wall, effort. Climbers can filter by them.")}</p>
      <form className="inline-form" onSubmit={onCreate} noValidate>
        <TextField label={t("New tag")} placeholder={t("e.g. Slab, Dynamic, Endurance")} value={name} onChange={(e) => setName(e.target.value)} error={createError} maxLength={24} />
        <Button type="submit" icon={<Plus aria-hidden />} loading={create.isPending} disabled={!name.trim()}>{t("Add")}</Button>
      </form>

      {tags.isPending ? <LoadingState label={t("Loading tags")} />
        : tags.isError ? <ErrorState error={tags.error} onRetry={() => tags.refetch()} />
        : tags.data.length === 0 ? <EmptyState icon={<Tag />} title={t("No tags yet")} body={t("Add the words your setters use, then pick them in each boulder's editor.")} />
        : <ul className="list">{tags.data.map((tag) => <TagRow key={tag.id} tag={tag} gymId={gym.id} />)}</ul>}
      <p className="section__footnote">{t("Renaming a tag renames it on every boulder. Deleting it takes it off them.")}</p>
    </div>
  );
}

function TagRow({ tag, gymId }: { tag: GymTag; gymId: string }) {
  const rename = useRenameTag(gymId);
  const remove = useDeleteTag(gymId);
  const toast = useToast();
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(tag.name);

  function save(e: FormEvent) {
    e.preventDefault();
    rename.mutate({ id: tag.id, name: name.trim() }, {
      onSuccess: () => setEditing(false),
      onError: (err) => { if (!(err instanceof ApiError && err.isValidation)) toast.error(errorMessage(err)); },
    });
  }

  if (editing) {
    const error = rename.error instanceof ApiError ? rename.error.fieldError("name") : undefined;
    return (
      <li className="list__row list__row--editing">
        <form className="inline-form inline-form--row" onSubmit={save} noValidate>
          <TextField label={t("Rename {name}", { name: tag.name })} value={name} onChange={(e) => setName(e.target.value)} error={error} autoFocus maxLength={24} />
          <Button type="submit" icon={<Check aria-hidden />} loading={rename.isPending} aria-label={t("Save name")}>{t("Save")}</Button>
          <Button variant="ghost" icon={<X aria-hidden />} onClick={() => { setEditing(false); setName(tag.name); rename.reset(); }} aria-label={t("Cancel")}>{t("Cancel")}</Button>
        </form>
      </li>
    );
  }

  return (
    <li className="list__row">
      <div className="list__main">
        <p className="list__title">{tag.name}</p>
        <p className="list__sub">{plural(tag.boulders, "On 1 boulder on the wall", "On {count} boulders on the wall")}</p>
      </div>
      <button type="button" className="icon-btn" onClick={() => setEditing(true)} aria-label={t("Rename {name}", { name: tag.name })}><Pencil aria-hidden /></button>
      <ConfirmButton variant="ghost" icon={<Trash2 aria-hidden />} loading={remove.isPending}
        confirmLabel={tag.boulders > 0 ? plural(tag.boulders, "Remove from 1 boulder?", "Remove from {count} boulders?") : t("Delete?")}
        onConfirm={() => remove.mutate(tag.id, { onError: (err) => toast.error(errorMessage(err)) })}>
        <span className="sr-only">{t("Delete {name}", { name: tag.name })}</span>
      </ConfirmButton>
    </li>
  );
}
