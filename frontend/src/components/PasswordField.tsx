import { forwardRef, useState, type ComponentProps } from "react";
import { Eye, EyeOff } from "lucide-react";
import { TextField } from "@/components/TextField";
import { t } from "@/i18n/i18n";

/**
 * A password field with a button to show what was typed: on a phone keyboard a typo in a hidden password is the
 * usual reason a sign-up or a sign-in fails. The type is switched in place, so password managers keep working.
 */
export const PasswordField = forwardRef<HTMLInputElement, Omit<ComponentProps<typeof TextField>, "type" | "trailing">>(
  function PasswordField(props, ref) {
    const [visible, setVisible] = useState(false);
    return (
      <TextField ref={ref} {...props} type={visible ? "text" : "password"} autoCapitalize="none" autoCorrect="off" spellCheck={false}
        trailing={
          <button type="button" className="field__reveal" onClick={() => setVisible((v) => !v)} aria-pressed={visible}
            aria-label={visible ? t("Hide password") : t("Show password")}>
            {visible ? <EyeOff aria-hidden /> : <Eye aria-hidden />}
          </button>
        } />
    );
  },
);
