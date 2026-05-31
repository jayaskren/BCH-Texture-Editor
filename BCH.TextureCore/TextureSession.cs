using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.Texture;
using SPICA.PICA.Commands;
using System.Collections.Generic;

namespace BCH.TextureCore
{
    /// <summary>
    /// Holds an open BCH scene and exposes all texture operations as byte[]-based methods.
    /// No System.Drawing or System.Windows.Forms dependency.
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
        /// Returns (rgbPng, alphaPng, width, height) for display.
        /// Both are PNG bytes. Uses ImageSharp — no System.Drawing.
        /// </summary>
        public (byte[] rgb, byte[] alpha, int width, int height) GetPreview(int index)
        {
            var tex = Scene.Textures[index];
            byte[] fullPng = Rgba8ToPng(tex);
            var (rgb, alpha) = ImageHelpers.SplitAlpha(fullPng);
            return (rgb, alpha, tex.Width, tex.Height);
        }

        // ── Export ─────────────────────────────────────────────────────────────

        public byte[] ExportTexturePng(int index)
            => Rgba8ToPng(Scene.Textures[index]);

        public (byte[] rgb, byte[] alpha) ExportTextureSplit(int index)
        {
            byte[] fullPng = Rgba8ToPng(Scene.Textures[index]);
            return ImageHelpers.SplitAlpha(fullPng);
        }

        // ── Import ─────────────────────────────────────────────────────────────

        public string ImportTexture(byte[] pngBytes, string name)
        {
            name = Sanitize(name);
            name = Deduplicate(name);
            Scene.Textures.Add(BuildTexture(name, pngBytes));
            return name;
        }

        public string ImportSplitTexture(byte[] rgbPng, byte[] alphaPng, string name)
        {
            name = Sanitize(name);
            name = Deduplicate(name);
            byte[] merged = ImageHelpers.MergeAlpha(rgbPng, alphaPng);
            Scene.Textures.Add(BuildTexture(name, merged));
            return name;
        }

        public void ReplaceTexture(int index, byte[] pngBytes)
        {
            var replacement = BuildTexture(Scene.Textures[index].Name, pngBytes);
            Scene.Textures[index].ReplaceData(replacement);
        }

        // ── Manage ─────────────────────────────────────────────────────────────

        public void RemoveTexture(int index)
            => Scene.Textures.Remove(Scene.Textures[index]);

        public string RenameTexture(int index, string newName)
        {
            newName = Sanitize(newName);
            if (string.IsNullOrEmpty(newName)) return Scene.Textures[index].Name;
            newName = Deduplicate(newName, excludeIndex: index);
            Scene.Textures[index].Name = newName;
            return newName;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static byte[] Rgba8ToPng(H3DTexture tex)
            => ImageHelpers.Rgba8ToPng(tex.RawBuffer, tex.Width, tex.Height);

        private static H3DTexture BuildTexture(string name, byte[] pngBytes)
        {
            var (rawBuffer, w, h) = ImageHelpers.PngToRgba8(pngBytes);
            return new H3DTexture
            {
                Name       = name,
                Format     = PICATextureFormat.RGBA8,
                MipmapSize = 1,
                Width      = w,
                Height     = h,
                RawBuffer  = rawBuffer
            };
        }

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
                    t = -1;
                }
            }
            return name;
        }
    }
}
