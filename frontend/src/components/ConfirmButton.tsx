import { useEffect, useState, type ReactNode } from "react";
import { Button } from "@/components/Button";

interface ConfirmButtonProps {
  children: ReactNode;
  confirmLabel: string;
  onConfirm: () => void;
  loading?: boolean;
  disabled?: boolean;
  icon?: ReactNode;
  variant?: "ghost" | "secondary" | "danger";
}

/**
 * Destructive actions take two taps: the first arms the button, the second confirms.
 * Works one-handed on a phone and avoids blocking browser dialogs. Disarms after 4 seconds.
 */
export function ConfirmButton({ children, confirmLabel, onConfirm, loading, disabled, icon, variant = "ghost" }: ConfirmButtonProps) {
  const [armed, setArmed] = useState(false);
  useEffect(() => {
    if (!armed) return;
    const t = window.setTimeout(() => setArmed(false), 4000);
    return () => window.clearTimeout(t);
  }, [armed]);

  return (
    <Button
      variant={armed ? "danger" : variant}
      icon={icon}
      loading={loading}
      disabled={disabled}
      onClick={() => (armed ? (setArmed(false), onConfirm()) : setArmed(true))}
      aria-live="polite"
    >
      {armed ? confirmLabel : children}
    </Button>
  );
}
