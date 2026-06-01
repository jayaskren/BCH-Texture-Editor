using FE3D.IO;
using FE3D;
using SPICA.Formats.CtrH3D;
using System.IO;
using System.Linq;

namespace BCH.TextureCore
{
    public enum SaveFormat { Bch, Lz, Arc }

    public static class FileOperations
    {
        public static TextureSession? OpenFile(byte[] fileBytes, string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (ext == ".lz" && fileBytes[0] == 0x13 && fileBytes[4] == 0x11)
                fileBytes = FEIO.LZ11Decompress(fileBytes.Skip(4).ToArray());

            if (FEIO.GetMagic(fileBytes) != "BCH")
                return null;

            var scene = H3D.Open(fileBytes);
            if (scene.Models.Count > 0)
                return null;

            return new TextureSession(scene, Path.GetFileName(fileName));
        }

        public static TextureSession OpenArc(byte[] arcBytes, string fileName)
        {
            var session = new TextureSession(new H3D(), Path.GetFileName(fileName));

            var files = FEArcOld.ExtractArcToMemory(arcBytes);
            var names = FEArcOld.ExtractArcNames(arcBytes);

            for (int i = 0; i < files.Count; i++)
            {
                byte[] bch = FEIO.LZ11Decompress(files[i].Skip(4).ToArray());
                if (FEIO.GetMagic(bch) != "BCH") continue;

                var bchScene = H3D.Open(bch);
                bchScene.Textures[0].Name = names[i].Replace(".bch.lz", "");
                session.Scene.Textures.Add(bchScene.Textures[0]);
            }

            return session;
        }

        public static byte[] Save(TextureSession session, SaveFormat format)
        {
            byte[] bch = H3D.Save(session.Scene);

            return format switch
            {
                SaveFormat.Lz => FEIO.LZ13Compress(bch),
                SaveFormat.Bch => bch,
                _ => throw new System.ArgumentException("Use SaveArc for .arc files")
            };
        }

        public static byte[] SaveArc(TextureSession session)
        {
            var arcFiles = new System.Collections.Generic.List<byte[]>();
            var arcNames = new System.Collections.Generic.List<string>();

            foreach (var texture in session.Scene.Textures)
            {
                var newBch = new H3D();
                newBch.Textures.Add(texture);

                // newBch shares the texture reference with the live session, so the
                // name must be restored after saving — otherwise saving an .arc renames
                // every open texture to "tmp" and a second save corrupts the archive.
                string originalName = texture.Name;
                string fileName = $"{originalName}.bch.lz";
                texture.Name = "tmp";
                newBch.ConverterVersion = 44139;
                newBch.BackwardCompatibility = 34;
                newBch.ForwardCompatibility = 35;

                arcFiles.Add(CompressArcEntry(H3D.Save(newBch)));
                arcNames.Add(fileName);

                texture.Name = originalName;
            }

            return FEArcOld.CreateArc(arcFiles, arcNames.ToArray());
        }

        // Builds an ARC LZ entry as a 0x13-wrapped LZ11 stream:
        // [0x13 + 24-bit size][full LZ11 file]. OpenArc reads this back via
        // LZ11Decompress(Skip(4)). We must NOT use FEIO.LZ13Compress here: FE3D's
        // LZ13 compressor produces truncated/corrupt output for data containing long
        // runs of repeated bytes (e.g. large transparent regions in portraits), which
        // made saved .arc files unreadable. The LZ11 compressor handles such data
        // correctly, and prepending the 0x13 header yields the exact format the game
        // (and OpenArc) expects.
        private static byte[] CompressArcEntry(byte[] bch)
        {
            byte[] lz11 = FEIO.LZ11Compress(bch);   // [0x11 + 24-bit size][body]
            byte[] entry = new byte[lz11.Length + 4];
            entry[0] = 0x13;
            entry[1] = lz11[1];                     // copy the 24-bit decompressed size
            entry[2] = lz11[2];
            entry[3] = lz11[3];
            System.Array.Copy(lz11, 0, entry, 4, lz11.Length);
            return entry;
        }
    }
}
