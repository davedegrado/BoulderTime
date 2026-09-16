import { forwardRef, useId, type SelectHTMLAttributes, type TextareaHTMLAttributes } from "react";

interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string;
  options: { value: string; label: string }[];
  error?: string;
  hideLabel?: boolean;
}

export const SelectField = forwardRef<HTMLSelectElement, SelectFieldProps>(function SelectField(
  { label, options, error, hideLabel, id, className = "", ...rest },
  ref,
) {
  const autoId = useId();
  const selectId = id ?? autoId;
  return (
    <div className={`field ${error ? "field--invalid" : ""} ${className}`}>
      <label className={hideLabel ? "sr-only" : "field__label"} htmlFor={selectId}>{label}</label>
      <select ref={ref} id={selectId} className="field__input field__select" aria-invalid={!!error || undefined} {...rest}>
        {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
      {error && <p className="field__error">{error}</p>}
    </div>
  );
});

interface TextAreaFieldProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string;
  error?: string;
  hint?: string;
}

export const TextAreaField = forwardRef<HTMLTextAreaElement, TextAreaFieldProps>(function TextAreaField(
  { label, error, hint, id, className = "", rows = 4, ...rest },
  ref,
) {
  const autoId = useId();
  const areaId = id ?? autoId;
  return (
    <div className={`field ${error ? "field--invalid" : ""} ${className}`}>
      <label className="field__label" htmlFor={areaId}>{label}</label>
      <textarea ref={ref} id={areaId} rows={rows} className="field__input field__textarea" aria-invalid={!!error || undefined} {...rest} />
      {error ? <p className="field__error">{error}</p> : hint ? <p className="field__hint">{hint}</p> : null}
    </div>
  );
});
