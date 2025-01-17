using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SchemeTool
{
    class Program
    {
        static void Main(string[] args)
        {
            // Load database
            using (Stream stream = File.OpenRead(".\\GameData\\Formats.dat"))
            {
                GameRes.FormatCatalog.Instance.DeserializeScheme(stream);
            }

            GameRes.Formats.FamilyAdvSystem.CsafOpener format = GameRes.FormatCatalog.Instance.ArcFormats.FirstOrDefault(a => a is GameRes.Formats.FamilyAdvSystem.CsafOpener) as GameRes.Formats.FamilyAdvSystem.CsafOpener;

            if (format != null)
            {
                GameRes.Formats.FamilyAdvSystem.FamilyAdvScheme schmem = format.Scheme as GameRes.Formats.FamilyAdvSystem.FamilyAdvScheme;

                // Add scheme information here

#if false
                byte[] cb = File.ReadAllBytes(@"MEM_10014628_00001000.mem");
                var cb2 = MemoryMarshal.Cast<byte, uint>(cb);
                for (int i = 0; i < cb2.Length; i++)
                    cb2[i] = ~cb2[i];
                var cs = new GameRes.Formats.KiriKiri.CxScheme
                {
                    Mask = 0x000,
                    Offset = 0x000,
                    PrologOrder = new byte[] { 0, 1, 2 },
                    OddBranchOrder = new byte[] { 0, 1, 2, 3, 4, 5 },
                    EvenBranchOrder = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 },
                    ControlBlock = cb2.ToArray()
                };
                GameRes.Formats.KiriKiri.ICrypt crypt = new GameRes.Formats.KiriKiri.CxEncryption(cs);

                GameRes.Formats.KiriKiri.ICrypt crypt = new GameRes.Formats.KiriKiri.XorCrypt(0x00);
#endif

                schmem.KnownKeys.Add("nanairo","招子");   
            }

            var gameMap = typeof(GameRes.FormatCatalog).GetField("m_game_map", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(GameRes.FormatCatalog.Instance) as Dictionary<string, string>;

            if (gameMap != null)
            {
                // Add file name here
                gameMap.Add("nanairo.exe", "nanairo");
            }

            // Save database
            using (Stream stream = File.Create(".\\GameData\\Formats.dat"))
            {
                GameRes.FormatCatalog.Instance.SerializeScheme(stream);
            }
        }
    }
}
