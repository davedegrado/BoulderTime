export interface ImagePrepOptions {
  /** Longest edge in pixels (or the side length when square). */
  maxSide?: number;
  quality?: number;
  /** Center-crop to a square (avatars, logos). */
  square?: boolean;
  /** Always re-encode, even if the file is already small (used for thumbnails). */
  force?: boolean;
}

/**
 * Downscales and re-encodes an image on the device before upload, keeping lists fast on gym Wi-Fi.
 * EXIF orientation is applied by the browser. Formats the browser can't decode are returned unchanged.
 */
export async function prepareImage(file: Blob, { maxSide = 1600, quality = 0.82, square = false, force = false }: ImagePrepOptions = {}): Promise<Blob> {
  if (!file.type.startsWith("image/")) throw new Error("Choose an image file.");
  if (typeof createImageBitmap !== "function") return file;

  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
  } catch {
    return file;
  }

  const srcSide = Math.min(bitmap.width, bitmap.height);
  const sx = square ? (bitmap.width - srcSide) / 2 : 0;
  const sy = square ? (bitmap.height - srcSide) / 2 : 0;
  const sw = square ? srcSide : bitmap.width;
  const sh = square ? srcSide : bitmap.height;
  const scale = Math.min(1, maxSide / Math.max(sw, sh));

  if (!force && !square && scale === 1 && file.type === "image/jpeg" && file.size < 1_500_000) {
    bitmap.close();
    return file;
  }
  const canvas = document.createElement("canvas");
  canvas.width = Math.round(sw * scale);
  canvas.height = Math.round(sh * scale);
  canvas.getContext("2d")!.drawImage(bitmap, sx, sy, sw, sh, 0, 0, canvas.width, canvas.height);
  bitmap.close();
  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error("Couldn't process the image."))), "image/jpeg", quality),
  );
}

/** Kept for existing callers: full-size boulder photo. */
export const prepareBoulderPhoto = (file: Blob) => prepareImage(file);
