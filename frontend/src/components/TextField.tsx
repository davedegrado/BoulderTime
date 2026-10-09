import { forwardRef, useId, type InputHTMLAttributes, type ReactNode } from "react";

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  hint?: string;
  /** A control inside the field, at its end (e.g. show/hide password). */
  trailing?: ReactNode;
}

export const TextField = forwardRef<HTMLInputElement, TextFieldProps>(function TextField(
  { label, error, hint, trailing, id, className = "", ...rest },
  ref,
) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const describedBy = error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined;
  return (
    <div className={`field ${error ? "field--invalid" : ""} ${className}`}>
      <label className="field__label" htmlFor={inputId}>{label}</label>
      {trailing ? (
        <div className="field__control">
          <input ref={ref} id={inputId} className="field__input field__input--trailing" aria-invalid={!!error || undefined} aria-describedby={describedBy} {...rest} />
          {trailing}
        </div>
      ) : (
        <input ref={ref} id={inputId} className="field__input" aria-invalid={!!error || undefined} aria-describedby={describedBy} {...rest} />
      )}
      {error ? (
        <p id={`${inputId}-error`} className="field__error">{error}</p>
      ) : hint ? (
        <p id={`${inputId}-hint`} className="field__hint">{hint}</p>
      ) : null}
    </div>
  );
});
