import { useEffect, useState } from "react";
import { useSaveVideoAllowance, useVideoAllowance } from "@/features/admin/api";
import { Toggle } from "@/features/notifications/NotificationBits";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/**
 * How much video one gym may keep. Set by BoulderTime, not by the gym: storage is shared and a gym cannot grant
 * itself more of it.
 */
export function VideoAllowanceCard({ gymId }: { gymId: string }) {
  const allowance = useVideoAllowance(gymId);
  const save = useSaveVideoAllowance();
  const toast = useToast();
  const [community, setCommunity] = useState(false);
  const [unlimited, setUnlimited] = useState(false);
  const [limit, setLimit] = useState("20");

  useEffect(() => {
    if (!allowance.data) return;
    setCommunity(allowance.data.communityVideosEnabled);
    setUnlimited(allowance.data.officialBetaLimit === null);
    setLimit(String(allowance.data.officialBetaLimit ?? 20));
  }, [allowance.data]);

  if (allowance.isPending) return <LoadingState label={t("Loading")} />;
  const used = allowance.data?.officialBetaUsed ?? 0;

  return (
    <div className="stack">
      <Toggle
        label={t("Climber videos")}
        description={t("Climbers of this gym can upload their own beta. Off by default: video is what fills the storage.")}
        checked={community}
        onChange={setCommunity} />

      <Toggle label={t("No limit")} description={t("Official beta videos")} checked={unlimited} onChange={setUnlimited} />
      {!unlimited && (
        <TextField label={t("Official beta videos")} type="number" inputMode="numeric" value={limit}
          onChange={(e) => setLimit(e.target.value)}
          hint={allowance.data?.officialBetaLimit != null
            ? t("{used} of {limit} used", { used, limit: allowance.data.officialBetaLimit })
            : t("{used} used", { used })} />
      )}

      <Button loading={save.isPending}
        onClick={() => save.mutate(
          { gymId, communityVideosEnabled: community, unlimited, officialBetaLimit: unlimited ? undefined : Number(limit) },
          { onSuccess: () => toast.success(t("Video allowance saved")), onError: (e) => toast.error(errorMessage(e)) })}>
        {t("Save changes")}
      </Button>
    </div>
  );
}
