import { useState, type FormEvent } from "react";
import { ArrowDown, ArrowUp, Check, Eye, EyeOff, Layers, Pencil, Plus, X } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useCreateSector, useReorderSectors, useSectors, useUpdateSector, type Sector } from "@/features/gyms/api";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { Badge } from "@/components/Badge";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { ApiError, errorMessage } from "@/lib/apiError";

export function ManageSectors() {
  const { gym } = useManagedGym();
  const sectors = useSectors(gym.id);
  const create = useCreateSector(gym.id);
  const reorder = useReorderSectors(gym.id);
  const toast = useToast();
  const [name, setName] = useState("");

  function onCreate(e: FormEvent) {
    e.preventDefault();
    create.mutate({ name: name.trim() }, {
      onSuccess: (s) => { setName(""); toast.success(`Sector "${s.name}" added`); },
      onError: (err) => { if (!(err instanceof ApiError && err.isValidation)) toast.error(errorMessage(err)); },
    });
  }

  function move(index: number, delta: -1 | 1) {
    const list = sectors.data ?? [];
    const target = index + delta;
    if (target < 0 || target >= list.length) return;
    const ids = list.map((s) => s.id);
    [ids[index], ids[target]] = [ids[target]!, ids[index]!];
    reorder.mutate(ids, { onError: (err) => toast.error(errorMessage(err)) });
  }

  const createError = create.error instanceof ApiError ? create.error.fieldError("name") : undefined;

  return (
    <div className="stack">
      <form className="inline-form" onSubmit={onCreate} noValidate>
        <TextField label="New sector" placeholder="e.g. Cave, Slab, Room 2" value={name} onChange={(e) => setName(e.target.value)} error={createError} maxLength={60} />
        <Button type="submit" icon={<Plus aria-hidden />} loading={create.isPending} disabled={!name.trim()}>Add</Button>
      </form>

      {sectors.isPending ? <LoadingState label="Loading sectors" />
        : sectors.isError ? <ErrorState error={sectors.error} onRetry={() => sectors.refetch()} />
        : sectors.data.length === 0 ? <EmptyState icon={<Layers />} title="No sectors yet" body="Add the areas of your gym so boulders can be organised by sector." />
        : (
          <ul className="list">
            {sectors.data.map((s, i) => (
              <SectorRow key={s.id} sector={s} gymId={gym.id} first={i === 0} last={i === sectors.data.length - 1}
                onUp={() => move(i, -1)} onDown={() => move(i, 1)} reordering={reorder.isPending} />
            ))}
          </ul>
        )}
      <p className="section__footnote">Hidden sectors aren't shown to climbers. Sectors are hidden rather than deleted so climbing history stays intact.</p>
    </div>
  );
}

interface RowProps { sector: Sector; gymId: string; first: boolean; last: boolean; onUp: () => void; onDown: () => void; reordering: boolean }

function SectorRow({ sector, gymId, first, last, onUp, onDown, reordering }: RowProps) {
  const update = useUpdateSector(gymId);
  const toast = useToast();
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(sector.name);

  function save(e: FormEvent) {
    e.preventDefault();
    update.mutate({ id: sector.id, name: name.trim() }, {
      onSuccess: () => setEditing(false),
      onError: (err) => { if (!(err instanceof ApiError && err.isValidation)) toast.error(errorMessage(err)); },
    });
  }

  if (editing) {
    const error = update.error instanceof ApiError ? update.error.fieldError("name") : undefined;
    return (
      <li className="list__row list__row--editing">
        <form className="inline-form inline-form--row" onSubmit={save} noValidate>
          <TextField label={`Rename ${sector.name}`} value={name} onChange={(e) => setName(e.target.value)} error={error} autoFocus maxLength={60} />
          <Button type="submit" icon={<Check aria-hidden />} loading={update.isPending} aria-label="Save name">Save</Button>
          <Button variant="ghost" icon={<X aria-hidden />} onClick={() => { setEditing(false); setName(sector.name); update.reset(); }} aria-label="Cancel">Cancel</Button>
        </form>
      </li>
    );
  }

  return (
    <li className={`list__row ${sector.isActive ? "" : "list__row--muted"}`}>
      <div className="reorder">
        <button type="button" className="icon-btn" onClick={onUp} disabled={first || reordering} aria-label={`Move ${sector.name} up`}><ArrowUp aria-hidden /></button>
        <button type="button" className="icon-btn" onClick={onDown} disabled={last || reordering} aria-label={`Move ${sector.name} down`}><ArrowDown aria-hidden /></button>
      </div>
      <div className="list__main">
        <p className="list__title">{sector.name}</p>
        {!sector.isActive && <Badge>Hidden</Badge>}
      </div>
      <button type="button" className="icon-btn" onClick={() => setEditing(true)} aria-label={`Rename ${sector.name}`}><Pencil aria-hidden /></button>
      <button
        type="button"
        className="icon-btn"
        disabled={update.isPending}
        onClick={() => update.mutate({ id: sector.id, isActive: !sector.isActive }, { onError: (err) => toast.error(errorMessage(err)) })}
        aria-label={sector.isActive ? `Hide ${sector.name}` : `Show ${sector.name}`}
      >
        {sector.isActive ? <EyeOff aria-hidden /> : <Eye aria-hidden />}
      </button>
    </li>
  );
}
