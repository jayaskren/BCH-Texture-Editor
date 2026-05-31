using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;

namespace BCH.TextureCore
{
    /// <summary>
    /// All image operations using SixLabors.ImageSharp — no System.Drawing dependency.
    /// </summary>
    public static class ImageHelpers
    {
        // Morton (Z-order) swizzle table for 8×8 tile encoding used by PICA200.
        private static readonly int[] SwizzleLUT =
        {
             0,  1,  8,  9,  2,  3, 10, 11,
            16, 17, 24, 25, 18, 19, 26, 27,
             4,  5, 12, 13,  6,  7, 14, 15,
            20, 21, 28, 29, 22, 23, 30, 31,
            32, 33, 40, 41, 34, 35, 42, 43,
            48, 49, 56, 57, 50, 51, 58, 59,
            36, 37, 44, 45, 38, 39, 46, 47,
            52, 53, 60, 61, 54, 55, 62, 63
        };

        private static int Pow2RoundDown(int n)
        {
            int p = 1;
            while (p * 2 <= n) p *= 2;
            return p;
        }

        private static int Rgba8BufferLength(int w, int h)
        {
            int len = w * h * 4;
            if ((len & 0x7f) != 0) len = (len & ~0x7f) + 0x80;
            return len;
        }

        /// <summary>
        /// Encodes a PNG into a PICA200 RGBA8 Morton-swizzled raw buffer.
        /// Returns (rawBuffer, width, height) with dimensions rounded to power-of-2.
        /// Replaces new H3DTexture(name, bitmap, RGBA8) on Linux.
        /// </summary>
        public static (byte[] rawBuffer, int width, int height) PngToRgba8(byte[] pngBytes)
        {
            using var img = Image.Load<Rgba32>(pngBytes);

            int w = Pow2RoundDown(img.Width);
            int h = Pow2RoundDown(img.Height);

            if (img.Width != w || img.Height != h)
                img.Mutate(x => x.Resize(w, h));

            // Flatten to linear RGBA byte array (top-left origin, row-major).
            byte[] pixels = new byte[w * h * 4];
            img.CopyPixelDataTo(pixels);

            byte[] output = new byte[Rgba8BufferLength(w, h)];
            int oOffs = 0;

            for (int tY = 0; tY < h; tY += 8)
            {
                for (int tX = 0; tX < w; tX += 8)
                {
                    for (int px = 0; px < 64; px++)
                    {
                        int x = SwizzleLUT[px] & 7;
                        int y = (SwizzleLUT[px] - x) >> 3;
                        int iOffs = (tX + x + (tY + y) * w) * 4;

                        // SPICA RGBA8 stores [A, R, G, B] per pixel.
                        output[oOffs + 0] = pixels[iOffs + 3]; // A
                        output[oOffs + 1] = pixels[iOffs + 0]; // R
                        output[oOffs + 2] = pixels[iOffs + 1]; // G
                        output[oOffs + 3] = pixels[iOffs + 2]; // B
                        oOffs += 4;
                    }
                }
            }

            return (output, w, h);
        }

        /// <summary>
        /// Decodes a PICA200 RGBA8 Morton-swizzled raw buffer to PNG bytes.
        /// Replaces H3DTexture.ToBitmap() on Linux.
        /// </summary>
        public static byte[] Rgba8ToPng(byte[] rawBuffer, int width, int height)
        {
            byte[] pixels = new byte[width * height * 4];
            int iOffs = 0;

            for (int tY = 0; tY < height; tY += 8)
            {
                for (int tX = 0; tX < width; tX += 8)
                {
                    for (int px = 0; px < 64; px++)
                    {
                        int x = SwizzleLUT[px] & 7;
                        int y = (SwizzleLUT[px] - x) >> 3;
                        int oOffs = (tX + x + (height - 1 - (tY + y)) * width) * 4;

                        // Stored [A, R, G, B] → output [R, G, B, A] (Rgba32).
                        pixels[oOffs + 0] = rawBuffer[iOffs + 1]; // R
                        pixels[oOffs + 1] = rawBuffer[iOffs + 2]; // G
                        pixels[oOffs + 2] = rawBuffer[iOffs + 3]; // B
                        pixels[oOffs + 3] = rawBuffer[iOffs + 0]; // A
                        iOffs += 4;
                    }
                }
            }

            using var img = Image.LoadPixelData<Rgba32>(pixels, width, height);
            // Flip vertically: SPICA stores pixels bottom-to-top.
            img.Mutate(x => x.Flip(FlipMode.Vertical));
            using var ms = new MemoryStream();
            img.SaveAsPng(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Splits RGBA PNG bytes into separate RGB and grayscale-alpha PNG bytes.
        /// </summary>
        public static (byte[] rgb, byte[] alpha) SplitAlpha(byte[] pngBytes)
        {
            using var image = Image.Load<Rgba32>(pngBytes);

            int w = image.Width, h = image.Height;
            using var rgbImage = new Image<Rgb24>(w, h);
            using var alphaImage = new Image<L8>(w, h);

            image.ProcessPixelRows(rgbImage, alphaImage, (srcAcc, rgbAcc, alphaAcc) =>
            {
                for (int y = 0; y < srcAcc.Height; y++)
                {
                    var src = srcAcc.GetRowSpan(y);
                    var rgb = rgbAcc.GetRowSpan(y);
                    var alp = alphaAcc.GetRowSpan(y);
                    for (int x = 0; x < src.Length; x++)
                    {
                        rgb[x] = new Rgb24(src[x].R, src[x].G, src[x].B);
                        alp[x] = new L8(src[x].A);
                    }
                }
            });

            using var rgbMs = new MemoryStream();
            using var alphaMs = new MemoryStream();
            rgbImage.SaveAsPng(rgbMs);
            alphaImage.SaveAsPng(alphaMs);
            return (rgbMs.ToArray(), alphaMs.ToArray());
        }

        /// <summary>
        /// Merges separate RGB and grayscale-alpha PNGs into a single RGBA PNG.
        /// </summary>
        public static byte[] MergeAlpha(byte[] rgbPng, byte[] alphaPng)
        {
            using var rgbImage = Image.Load<Rgb24>(rgbPng);
            using var alphaImage = Image.Load<L8>(alphaPng);
            using var merged = new Image<Rgba32>(rgbImage.Width, rgbImage.Height);

            merged.ProcessPixelRows(rgbImage, alphaImage, (dstAcc, rgbAcc, alphaAcc) =>
            {
                for (int y = 0; y < dstAcc.Height; y++)
                {
                    var dst = dstAcc.GetRowSpan(y);
                    var rgb = rgbAcc.GetRowSpan(y);
                    var alp = alphaAcc.GetRowSpan(y);
                    for (int x = 0; x < dst.Length; x++)
                        dst[x] = new Rgba32(rgb[x].R, rgb[x].G, rgb[x].B, alp[x].PackedValue);
                }
            });

            using var ms = new MemoryStream();
            merged.SaveAsPng(ms);
            return ms.ToArray();
        }
    }
}
