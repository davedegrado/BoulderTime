import { t } from "@/i18n/i18n";
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
  if (!file.type.startsWith("image/")) throw new Error(t("Choose an image file."));
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
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error(t("Couldn't process the image.")))), "image/jpeg", quality),
  );
}

/**
 * How a boulder's pictures are stored. Phone photos are large and every climber downloads them, so they are kept just
 * sharp enough to tell the holds apart: about half what they weighed at 1600 px. Always encoded again, so a small
 * but heavy file (a screenshot, an edited photo) doesn't go up as it is. Wall photos, avatars and the rest keep the
 * defaults of {@link prepareImage}.
 */
export const BOULDER_PHOTO = { maxSide: 1280, quality: 0.7, force: true } as const;
/** The original a highlight is made from and goes on from: a little finer, as the hold colours are read from it. */
export const HIGHLIGHT_SOURCE = { maxSide: 1280, quality: 0.8, force: true } as const;
/** The card picture cut out of the photo by staff. */
export const CARD_PICTURE = { width: 480, height: 384, quality: 0.7 } as const;
/** The card picture of the whole photo, when staff don't choose a part. */
export const WHOLE_CARD_PICTURE = { maxSide: 480, quality: 0.7, force: true } as const;

/** A boulder's photo, prepared as it is stored. */
export const prepareBoulderPhoto = (file: Blob) => prepareImage(file, BOULDER_PHOTO);

export interface CropRect { x: number; y: number; width: number; height: number }

/**
 * Cuts a part of a photo out as a small JPEG of a fixed size: the card picture staff choose. The rectangle is in the
 * photo's own pixels, as the picture is shown (EXIF orientation applied).
 */
export async function cropImage(source: Blob, rect: CropRect, { width = 600, height = 480, quality = 0.8 } = {}): Promise<Blob> {
  const picture = await decodeImage(source);
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  canvas.getContext("2d")!.drawImage(picture.image, rect.x, rect.y, rect.width, rect.height, 0, 0, width, height);
  picture.release();
  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error(t("Couldn't process the image.")))), "image/jpeg", quality),
  );
}

/**
 * Decodes a picture for drawing. createImageBitmap where the device has it; otherwise (older WebViews, or options it
 * doesn't know) a plain image element from a local address, which the browser lets the app read just the same.
 */
export async function decodeImage(source: Blob): Promise<{ image: CanvasImageSource; release: () => void }> {
  if (typeof createImageBitmap === "function") {
    try {
      const bitmap = await createImageBitmap(source, { imageOrientation: "from-image" });
      return { image: bitmap, release: () => bitmap.close() };
    } catch {
      // fall through to the image element
    }
  }
  const url = URL.createObjectURL(source);
  const img = new Image();
  img.decoding = "async";
  img.src = url;
  try {
    await img.decode();
  } catch (e) {
    URL.revokeObjectURL(url);
    throw e;
  }
  return { image: img, release: () => URL.revokeObjectURL(url) };
}

/** How the card picture is made from the photo (ADR-068); the API's names. */
export type ThumbnailMode = "FILL_CENTER" | "FILL_TOP" | "FILL_BOTTOM" | "FIT" | "CUSTOM";

/**
 * Where a photo of `w`×`h` is drawn on a card of `cw`×`ch` (all in pixels), whole or covering it. Fill covers the card
 * and keeps the photo's centre, top or bottom (sideways it is always centred); Fit shows it whole, centred.
 */
export function cardPlacement(w: number, h: number, cw: number, ch: number, mode: Exclude<ThumbnailMode, "CUSTOM">):
  { x: number; y: number; width: number; height: number } {
  const scale = mode === "FIT" ? Math.min(cw / w, ch / h) : Math.max(cw / w, ch / h);
  const width = w * scale;
  const height = h * scale;
  const x = (cw - width) / 2;
  const y = mode === "FILL_TOP" ? 0 : mode === "FILL_BOTTOM" ? ch - height : (ch - height) / 2;
  return { x, y, width, height };
}

/**
 * The card picture for a choice other than a part chosen by hand: a small 5:4 JPEG, the size of a hand-chosen one.
 * Fit fills the room left with the same photo, covering the card, blurred and darkened: blurred by drawing it tiny and
 * stretching it back, which every device does (the canvas blur filter is missing from older iPhones).
 */
export async function cardPicture(source: Blob, mode: Exclude<ThumbnailMode, "CUSTOM">,
  { width = CARD_PICTURE.width, height = CARD_PICTURE.height, quality = CARD_PICTURE.quality } = {}): Promise<Blob> {
  const picture = await decodeImage(source);
  const w = (picture.image as { width: number }).width;
  const h = (picture.image as { height: number }).height;
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext("2d")!;
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = "high";
  if (mode === "FIT") {
    // The room left over: the same photo covering the card, blurred and darkened. With the canvas blur filter where the
    // device has it; elsewhere (older iPhones) by drawing it small and stretching it back in steps, which ends as soft.
    const cover = cardPlacement(w, h, width, height, "FILL_CENTER");
    if ("filter" in ctx && typeof ctx.filter === "string") {
      ctx.filter = "blur(14px)";
      // A little larger than the card, so the blur doesn't fade to transparent at the edges.
      ctx.drawImage(picture.image, cover.x - 20, cover.y - 20, cover.width + 40, cover.height + 40);
      ctx.filter = "none";
    } else {
      let step: CanvasImageSource = picture.image;
      let sw = w; let sh = h;
      for (const side of [12, 24, 48, 96, 192]) {
        const c = document.createElement("canvas");
        c.width = side;
        c.height = Math.max(1, Math.round(side * height / width));
        const cc = c.getContext("2d")!;
        cc.imageSmoothingEnabled = true;
        const at = cardPlacement(sw, sh, c.width, c.height, "FILL_CENTER");
        cc.drawImage(step, at.x, at.y, at.width, at.height);
        step = c; sw = c.width; sh = c.height;
      }
      ctx.drawImage(step, 0, 0, width, height);
    }
    ctx.fillStyle = "rgba(0, 0, 0, 0.4)";
    ctx.fillRect(0, 0, width, height);
  }
  const at = cardPlacement(w, h, width, height, mode);
  ctx.drawImage(picture.image, at.x, at.y, at.width, at.height);
  picture.release();
  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error(t("Couldn't process the image.")))), "image/jpeg", quality),
  );
}
