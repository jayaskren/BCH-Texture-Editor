using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.Texture;
using SPICA.PICA.Commands;
using System.Collections.Generic;
using System.IO;

namespace BCH.TextureCore
{
    /// <summary>
    /// Holds an open BCH scene and exposes all texture operations as byte[]-based methods.
    /// Neither UI nor System.Windows.Forms is referenced here.
    /// </summary>
    public class TextureSession
    {
        public H3D Scene { get; }
        public string FileName { get; set; }

        public TextureSession(H3D scene, string fileName)
        {
            Scene = scene;
            FileName = fileName;
        }

        public List<string> TextureNames
        {
            get
            {
                var names = new List<string>();
                foreach (var t in Scene.Textures)
                    names.Add(t.Name);
                return names;
            }
        }

        public int Count => Scene.Textures.Count;

        // ── Preview ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns (rgbPng, alphaPng) for display. Both are PNG bytes.
        /// alphaPng is a grayscale image; empty byte[] when no alpha channel.
        /// </summary>
        public (byte[] rgb, byte[] alpha, int width, int height) GetPreview(int index)
        {
            using var bmp = Scene.Textures[index].ToBitmap();
            byte[] fullPng = ImageHelpers.BitmapToPng(bmp);
            var (rgb, alpha) = ImageHelpers.SplitAlpha(fullPng);
            return (rgb, alpha, bmp.Width, bmp.Height);
        }

        // ── Export ─────────────────────────────────────────────────────────────

        public byte[] ExportTexturePng(int index)
        {
            using var bmp = Scene.Textures[index].ToBitmap();
            return ImageHelpers.BitmapToPng(bmp);
        }

        public (byte[] rgb, byte[] alpha) ExportTextureSplit(int index)
        {
            using var bmp = Scene.Textures[index].ToBitmap();
            byte[] fullPng = ImageHelpers.BitmapToPng(bmp);
            return ImageHelpers.SplitAlpha(fullPng);
        }

        // ── Import ─────────────────────────────────────────────────────────────

        public string ImportTexture(byte[] pngBytes, string name)
        {
            name = Sanitize(name);
            name = Deduplicate(name);

            using var bmp = ImageHelpers.PngToBitmap(pngBytes);
            var texture = new H3DTexture(name, bmp, PICATextureFormat.RGBA8);
            Scene.Textures.Add(texture);
            return name;
        }

        public string ImportSplitTexture(byte[] rgbPng, byte[] alphaPng, string name)
        {
            name = Sanitize(name);
            name = Deduplicate(name);

            byte[] merged = ImageHelpers.MergeAlpha(rgbPng, alphaPng);
            using var bmp = ImageHelpers.PngToBitmap(merged);
            var texture = new H3DTexture(name, bmp, PICATextureFormat.RGBA8);
            Scene.Textures.Add(texture);
            return name;
        }

        public void ReplaceTexture(int index, byte[] pngBytes)
        {
            using var bmp = ImageHelpers.PngToBitmap(pngBytes);
            var replacement = new H3DTexture(Scene.Textures[index].Name, bmp, PICATextureFormat.RGBA8);
            Scene.Textures[index].ReplaceData(replacement);
        }

        // ── Manage ─────────────────────────────────────────────────────────────

        public void RemoveTexture(int index)
        {
            Scene.Textures.Remove(Scene.Textures[index]);
        }

        public string RenameTexture(int index, string newName)
        {
            newName = Sanitize(newName);
            if (string.IsNullOrEmpty(newName)) return Scene.Textures[index].Name;

            newName = Deduplicate(newName, excludeIndex: index);
            Scene.Textures[index].Name = newName;
            return newName;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string Sanitize(string name) => name.Replace(" ", "");

        private string Deduplicate(string name, int excludeIndex = -1)
        {
            string baseName = name;
            int i = 0;
            for (int t = 0; t < Scene.Textures.Count; t++)
            {
                if (t == excludeIndex) continue;
                if (Scene.Textures[t].Name == name)
                {
                    i++;
                    name = $"{baseName}_{i}";
                    t = -1; // restart scan
                }
            }
            return name;
        }
    }
}
