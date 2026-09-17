import { useRef, useState, type FormEvent } from "react";
import { useSearchParams } from "react-router-dom";
import { ImagePlus, Megaphone, Pencil, Plus, Trash2, X } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useSectors } from "@/features/gyms/api";
import {
  announcementTypeLabel, needsEventDate, uploadAnnouncementImage, useAnnouncementMutations, useAnnouncements,
  type Announcement, type AnnouncementType, type SaveAnnouncementInput,
} from "@/features/notifications/api";
import { AnnouncementCard } from "@/features/notifications/NotificationBits";
import { SelectField, TextAreaField } from "@/components/Fields";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";

export function ManageAnnouncements() {
  const { gym } = useManagedGym();
  const [params, setParams] = useSearchParams();
  const list = useAnnouncements(gym.id);
  const m = useAnnouncementMutations(gym.id);
  const toast = useToast();
  const [editing, setEditing] = useState<Announcement | null>(null);
  const creating = params.get("new") === "1";
  const items = list.data?.pages.flatMap((p) => p.items) ?? [];

  const close = () => { setEditing(null); setParams({}, { replace: true }); };

  return (
    <div className="stack">
      {creating || editing ? (
        <AnnouncementForm key={editing?.id ?? "new"} gymId={gym.id} initial={editing}
          prefill={{ title: params.get("title") ?? "", sectorId: params.get("sectorId") }} onDone={close} />
      ) : (
        <Button icon={<Plus aria-hidden />} onClick={() => setParams({ new: "1" }, { replace: true })}>New update</Button>
      )}

      {list.isPending ? <LoadingState label="Loading updates" />
        : list.isError ? <ErrorState error={list.error} onRetry={() => list.refetch()} />
        : items.length === 0 ? <EmptyState icon={<Megaphone />} title="No updates published" body="Tell climbers about new circuits, events, competitions and schedule changes." />
        : items.map((a) => (
          <AnnouncementCard key={a.id} a={a} actions={<>
            <span className="list__sub">{a.notifiedFollowers ? "Followers were notified" : "Published without notifying"}</span>
            <Button variant="secondary" icon={<Pencil aria-hidden />} onClick={() => { setEditing(a); window.scrollTo({ top: 0, behavior: "smooth" }); }}>Edit</Button>
            <ConfirmButton icon={<Trash2 aria-hidden />} confirmLabel="Delete?" loading={m.remove.isPending && m.remove.variables === a.id}
              onConfirm={() => m.remove.mutate(a.id, { onSuccess: () => toast.success("Update deleted"), onError: (e) => toast.error(errorMessage(e)) })}>Delete</ConfirmButton>
          </>} />
        ))}
    </div>
  );
}

function toLocalInput(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function AnnouncementForm({ gymId, initial, prefill, onDone }: { gymId: string; initial: Announcement | null; prefill: { title: string; sectorId: string | null }; onDone: () => void }) {
  const m = useAnnouncementMutations(gymId);
  const sectors = useSectors(gymId);
  const toast = useToast();
  const fileInput = useRef<HTMLInputElement>(null);
  const [type, setType] = useState<AnnouncementType>(initial?.type ?? "ANNOUNCEMENT");
  const [title, setTitle] = useState(initial?.title ?? prefill.title);
  const [content, setContent] = useState(initial?.content ?? "");
  const [sectorId, setSectorId] = useState(initial?.sectorId ?? prefill.sectorId ?? "");
  const [eventDate, setEventDate] = useState(toLocalInput(initial?.eventDate ?? null));
  const [notify, setNotify] = useState(true);
  const [imagePath, setImagePath] = useState<string | null>(initial?.imagePath ?? null);
  const [imagePreview, setImagePreview] = useState<string | null>(initial?.imageUrl ?? null);
  const [uploading, setUploading] = useState(false);
  const mutation = initial ? m.update : m.create;
  const err = mutation.error instanceof ApiError ? mutation.error : null;

  async function pickImage(file: File | undefined) {
    if (!file) return;
    setUploading(true);
    try {
      setImagePreview(URL.createObjectURL(file));
      setImagePath(await uploadAnnouncementImage(gymId, file));
    } catch (e) {
      toast.error(errorMessage(e));
      setImagePreview(initial?.imageUrl ?? null);
    } finally {
      setUploading(false);
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    const input: SaveAnnouncementInput = {
      type, title: title.trim(), content: content.trim(), sectorId: sectorId || null, imagePath,
      eventDate: needsEventDate(type) && eventDate ? new Date(eventDate).toISOString() : null,
    };
    const onError = (error: unknown) => { if (!(error instanceof ApiError && error.isValidation)) toast.error(errorMessage(error)); };
    if (initial) {
      m.update.mutate({ ...input, id: initial.id }, { onSuccess: () => { toast.success("Update saved"); onDone(); }, onError });
    } else {
      m.create.mutate({ ...input, notifyFollowers: notify }, {
        onSuccess: (a) => { toast.success(a.notifiedFollowers ? "Published and followers notified" : "Published"); onDone(); },
        onError,
      });
    }
  }

  return (
    <form className="card form" onSubmit={onSubmit} noValidate>
      <div className="section__row">
        <h2 className="section__title">{initial ? "Edit update" : "New update"}</h2>
        <button type="button" className="icon-btn" onClick={onDone} aria-label="Close"><X aria-hidden /></button>
      </div>
      <SelectField label="Type" value={type} onChange={(e) => setType(e.target.value as AnnouncementType)}
        options={(Object.keys(announcementTypeLabel) as AnnouncementType[]).map((t) => ({ value: t, label: announcementTypeLabel[t] }))} />
      <TextField label="Title" value={title} onChange={(e) => setTitle(e.target.value)} error={err?.fieldError("title")} maxLength={120} />
      {needsEventDate(type) && (
        <TextField label="Date and time" type="datetime-local" value={eventDate} onChange={(e) => setEventDate(e.target.value)} error={err?.fieldError("eventDate")} />
      )}
      <TextAreaField label="Message" rows={5} value={content} onChange={(e) => setContent(e.target.value)} error={err?.fieldError("content")} maxLength={4000} />
      <SelectField label="Sector" value={sectorId} onChange={(e) => setSectorId(e.target.value)} error={err?.fieldError("sectorId")}
        options={[{ value: "", label: "Whole gym" }, ...(sectors.data ?? []).filter((s) => s.isActive).map((s) => ({ value: s.id, label: s.name }))]} />

      <div className="editor__section">
        <span className="field__label">Image</span>
        {imagePreview && <img className="announcement__image" src={imagePreview} alt="" />}
        <input ref={fileInput} type="file" accept="image/*" hidden onChange={(e) => pickImage(e.target.files?.[0])} />
        <div className="form__actions">
          <Button variant="secondary" icon={<ImagePlus aria-hidden />} onClick={() => fileInput.current?.click()} loading={uploading}>{imagePreview ? "Change image" : "Add image"}</Button>
          {imagePreview && <Button variant="ghost" onClick={() => { setImagePath(null); setImagePreview(null); }}>Remove image</Button>}
        </div>
        {err?.fieldError("imagePath") && <p className="field__error">{err.fieldError("imagePath")}</p>}
      </div>

      {!initial && (
        <label className="check">
          <input type="checkbox" checked={notify} onChange={(e) => setNotify(e.target.checked)} />
          <span>
            <span className="list__title">Notify followers</span>
            <span className="list__sub">{sectorId ? "People following the gym or this sector get one notification." : "People following the gym get one notification."} Edits never notify again.</span>
          </span>
        </label>
      )}
      <Button type="submit" loading={mutation.isPending} disabled={uploading}>{initial ? "Save changes" : "Publish"}</Button>
    </form>
  );
}
