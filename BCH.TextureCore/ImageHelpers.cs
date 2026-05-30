using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Drawing;
using System.IO;

namespace BCH.TextureCore
{
    public static class ImageHelpers
    {
        /// <summary>
        /// Converts a System.Drawing.Bitmap (from SPICA) to PNG bytes.
        /// </summary>
        public static byte[] BitmapToPng(System.Drawing.Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }

        /// <summary>
        /// Converts PNG bytes to a System.Drawing.Bitmap for SPICA constructors.
        /// </summary>
        public static System.Drawing.Bitmap PngToBitmap(byte[] pngBytes)
        {
            using var ms = new MemoryStream(pngBytes);
            var bmp = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromStream(ms);
            // Ensure alpha channel exists
            if (bmp.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            {
                var argb = new System.Drawing.Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = System.Drawing.Graphics.FromImage(argb);
                g.DrawImage(bmp, 0, 0);
                bmp.Dispose();
                return argb;
            }
            return bmp;
        }

        /// <summary>
        /// Returns true if the texture bitmap has any non-opaque pixels.
        /// </summary>
        public static bool BitmapHasAlpha(System.Drawing.Bitmap bmp)
        {
            return System.Drawing.Image.IsAlphaPixelFormat(bmp.PixelFormat);
        }

        /// <summary>
        /// Splits RGBA PNG bytes into separate RGB and grayscale-alpha PNG bytes.
        /// RGB output has alpha stripped (fully opaque). Alpha output is grayscale.
        /// </summary>
        public static (byte[] rgb, byte[] alpha) SplitAlpha(byte[] pngBytes)
        {
            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(pngBytes);

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
        /// Merges separate RGB and grayscale-alpha PNGs back into a single RGBA PNG.
        /// </summary>
        public static byte[] MergeAlpha(byte[] rgbPng, byte[] alphaPng)
        {
            using var rgbImage = SixLabors.ImageSharp.Image.Load<Rgb24>(rgbPng);
            using var alphaImage = SixLabors.ImageSharp.Image.Load<L8>(alphaPng);
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
