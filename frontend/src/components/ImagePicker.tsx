import { useRef, type ReactNode } from "react";
import { ImagePlus, Trash2 } from "lucide-react";
import { Button } from "@/components/Button";

interface ImagePickerProps {
  label: string;
  hint?: string;
  preview: ReactNode;
  hasImage: boolean;
  busy?: boolean;
  onPick: (file: File) => void;
  onRemove: () => void;
}

/** Preview + "Change"/"Remove" controls for a single image. */
export function ImagePicker({ label, hint, preview, hasImage, busy, onPick, onRemove }: ImagePickerProps) {
  const input = useRef<HTMLInputElement>(null);
  return (
    <div className="image-picker">
      <div className="image-picker__preview">{preview}</div>
      <div className="image-picker__body">
        <span className="field__label">{label}</span>
        {hint && <span className="field__hint">{hint}</span>}
        <input ref={input} type="file" accept="image/jpeg,image/png,image/webp,image/*" hidden
          onChange={(e) => { const f = e.target.files?.[0]; if (f) onPick(f); e.target.value = ""; }} />
        <div className="form__actions">
          <Button variant="secondary" icon={<ImagePlus aria-hidden />} loading={busy} onClick={() => input.current?.click()}>
            {hasImage ? "Change" : "Upload"}
          </Button>
          {hasImage && <Button variant="ghost" icon={<Trash2 aria-hidden />} disabled={busy} onClick={onRemove}>Remove</Button>}
        </div>
      </div>
    </div>
  );
}
