import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Smartphone } from "lucide-react";
import { api } from "@/lib/api";
import { ApiError, errorMessage } from "@/lib/apiError";
import type { AppVersions, PlatformVersions } from "@/lib/appVersion";
import { Button } from "@/components/Button";
import { TextField } from "@/components/TextField";
import { useToast } from "@/components/Toast";
import { t } from "@/i18n/i18n";

const KEY = ["app-version"];

/**
 * The store apps' minimum and recommended versions (ADR-059). Raise the minimum only once the new version can be
 * downloaded from that store: whoever is below it can't use the app until they update.
 */
export function AppVersionsCard() {
  const versions = useQuery({ queryKey: KEY, queryFn: ({ signal }) => api.get<AppVersions>("/api/app-version", { signal }) });
  return (
    <section className="card stack" aria-labelledby="app-versions-title">
      <h2 id="app-versions-title" className="section__title"><Smartphone aria-hidden className="title-icon" /> {t("App versions")}</h2>
      <p className="field__hint">
        {t("Below the minimum version the store app asks to be updated and can't be used; below the recommended one it only suggests it. The website never asks. Raise the minimum only once the new version is available on that store.")}
      </p>
      {versions.isError && <p className="field__error" role="alert">{errorMessage(versions.error)}</p>}
      {versions.data && (
        <>
          <PlatformForm platform="ios" title={t("iOS · App Store")} current={versions.data.ios} />
          <PlatformForm platform="android" title={t("Android · Google Play")} current={versions.data.android} />
        </>
      )}
    </section>
  );
}

function PlatformForm({ platform, title, current }: { platform: "ios" | "android"; title: string; current: PlatformVersions }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [minimum, setMinimum] = useState(current.minimum);
  const [recommended, setRecommended] = useState(current.recommended ?? "");
  const [storeUrl, setStoreUrl] = useState(current.storeUrl ?? "");
  useEffect(() => { setMinimum(current.minimum); setRecommended(current.recommended ?? ""); setStoreUrl(current.storeUrl ?? ""); }, [current]);

  const save = useMutation({
    mutationFn: () => api.put<AppVersions>(`/api/admin/app-versions/${platform}`, { minimum, recommended: recommended || null, storeUrl: storeUrl || null }),
    onSuccess: (v) => { qc.setQueryData(KEY, v); toast.success(t("Saved")); },
  });
  const err = (field: string) => (save.error instanceof ApiError ? save.error.fieldError(field) : undefined);

  return (
    <form className="stack" onSubmit={(e) => { e.preventDefault(); save.mutate(); }} noValidate>
      <h3 className="list__title">{title}</h3>
      <TextField label={t("Minimum version")} value={minimum} onChange={(e) => setMinimum(e.target.value)} inputMode="decimal" error={err("minimum")} />
      <TextField label={t("Recommended version (optional)")} value={recommended} onChange={(e) => setRecommended(e.target.value)} inputMode="decimal" error={err("recommended")} />
      <TextField label={t("Store page")} value={storeUrl} onChange={(e) => setStoreUrl(e.target.value)} inputMode="url" error={err("storeUrl")} />
      {save.isError && !err("minimum") && !err("recommended") && !err("storeUrl") && <p className="field__error" role="alert">{errorMessage(save.error)}</p>}
      <Button type="submit" variant="secondary" loading={save.isPending}>{t("Save {platform}", { platform: platform === "ios" ? "iOS" : "Android" })}</Button>
    </form>
  );
}
