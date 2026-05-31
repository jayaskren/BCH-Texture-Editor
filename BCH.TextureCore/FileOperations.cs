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

                string fileName = $"{newBch.Textures[0].Name}.bch.lz";
                newBch.Textures[0].Name = "tmp";
                newBch.ConverterVersion = 44139;
                newBch.BackwardCompatibility = 34;
                newBch.ForwardCompatibility = 35;

                arcFiles.Add(FEIO.LZ13Compress(H3D.Save(newBch)));
                arcNames.Add(fileName);
            }

            return FEArcOld.CreateArc(arcFiles, arcNames.ToArray());
        }
    }
}
