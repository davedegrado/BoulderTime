import { Search, X } from "lucide-react";

interface SearchFieldProps {
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  label: string;
}

export function SearchField({ value, onChange, placeholder, label }: SearchFieldProps) {
  return (
    <div className="search">
      <Search className="search__icon" aria-hidden />
      <input
        className="search__input"
        type="search"
        inputMode="search"
        enterKeyHint="search"
        aria-label={label}
        placeholder={placeholder}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      {value && (
        <button type="button" className="search__clear" onClick={() => onChange("")} aria-label="Clear search">
          <X aria-hidden />
        </button>
      )}
    </div>
  );
}
