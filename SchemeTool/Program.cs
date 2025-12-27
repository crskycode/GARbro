﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
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
#if false
            using (Stream stream = File.Create(".\\GameData\\Formats.json"))
            {
                GameRes.FormatCatalog.Instance.SerializeSchemeJson(stream);
                return;
            }
#endif
            GameRes.Formats.KiriKiri.Xp3Opener format = GameRes.FormatCatalog.Instance.ArcFormats
                .FirstOrDefault(a => a is GameRes.Formats.KiriKiri.Xp3Opener) as GameRes.Formats.KiriKiri.Xp3Opener;

            if (format != null)
            {
                GameRes.Formats.KiriKiri.Xp3Scheme scheme = format.Scheme as GameRes.Formats.KiriKiri.Xp3Scheme;

                // Add scheme information here

#if true
                byte[] cb = File.ReadAllBytes(@"CxdecTable.bin"); //Also called ControlBlock, but this is the file generated from KrKrDump
                var cb2 = MemoryMarshal.Cast<byte, uint>(cb);
                for (int i = 0; i < cb2.Length; i++)
                    cb2[i] = ~cb2[i];
                var cs = new GameRes.Formats.KiriKiri.CxScheme //Fill in this information obtained from the KrKrDump log
                {
                    Mask = 0x28E,
                    Offset = 0x7A,
                    PrologOrder = new byte[] { 2, 1, 0 },
                    OddBranchOrder = new byte[] { 5, 0, 4, 2, 3, 1 },
                    EvenBranchOrder = new byte[] { 7, 5, 4, 6, 3, 1, 2, 0 },
                    ControlBlock = cb2.ToArray()
                };
                var crypt = new GameRes.Formats.KiriKiri.HxCrypt(cs);
                crypt.RandomType = 0; //Information also obtained from the KrKrDump log
                crypt.FilterKey = 0xB60FF5AB08C907DB; //Information also obtained from the KrKrDump log
                crypt.NamesFile = "HxNames.lst";    //Call it something else to ensure that more than 1 game can be stored in the same folder
                var dataKey = SoapHexBinary.Parse("587C3F0F8960A4A4B62CD52B431513B1C166DDD8FD59B343C5F762C3D0345016").Value; //Index key
                var dataNonce = SoapHexBinary.Parse("C89AFF629DE48D3B1BD986A804930119").Value; //Index nonce
                var patchKey = SoapHexBinary.Parse("8B572D62F899E57929421687329AF3DFAFF951A03D87B9D0382C8A1FC3D687DF").Value;
                var patchNonce = SoapHexBinary.Parse("D222041CA4902962BA533FCD495F399E").Value;

                //Put inside this Dictionary all of the files the game contains, and put the correspoding index key and index nonce for each of the files (which is obtained from the KrKrDump log)
                crypt.IndexKeyDict = new Dictionary<string, GameRes.Formats.KiriKiri.HxIndexKey>()
                {
                    { "data.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = dataKey, Key2 = dataNonce } },
                    { "video.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = dataKey, Key2 = dataNonce } },
                    { "others.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = dataKey, Key2 = dataNonce } },
                    { "fgimage.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = dataKey, Key2 = dataNonce } },
                    { "scn.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = dataKey, Key2 = dataNonce } },
                    { "image.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = dataKey, Key2 = dataNonce } },
                    { "voice.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = dataKey, Key2 = dataNonce } },
                    { "evimage.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = dataKey, Key2 = dataNonce } },
                    { "steam.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = dataKey, Key2 = dataNonce } },
                    { "locale_en.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = dataKey, Key2 = dataNonce } },
                    { "locale_cn.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = dataKey, Key2 = dataNonce } },
                    { "patch.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = patchKey, Key2 = patchNonce } },
                    { "patch_uncensored1.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = patchKey, Key2 = patchNonce } },
                    { "patch_locale_cn.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = patchKey, Key2 = patchNonce } },
                    { "uncensored1.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = patchKey, Key2 = patchNonce } },
                    { "uncensored2.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = patchKey, Key2 = patchNonce } },
                    { "uncensored3.xp3", new GameRes.Formats.KiriKiri.HxIndexKey { Key1 = patchKey, Key2 = patchNonce } },
                };
#else
                GameRes.Formats.KiriKiri.ICrypt crypt = new GameRes.Formats.KiriKiri.XorCrypt(0x00);
#endif

                scheme.KnownSchemes.Add("gameTitle", crypt); //Add the game title to the dropdown menu to select the decryption method
            }

            var gameMap = typeof(GameRes.FormatCatalog).GetField("m_game_map", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(GameRes.FormatCatalog.Instance) as Dictionary<string, string>;

            if (gameMap != null)
            {
                //While not mandatory, this makes the program automatically decrypt xp3 files with the names stated above when the same directory contains
                //an executable with the same name, GarBro will automatically attempt to decrypt the loaded file with the scheme titled above (MAKE SURE THAT
                //THE TITLE DO COINCIDE HERE AND ABOVE, OTHERWISE IT WON'T WORK)
                gameMap.Add("gameExecutable.exe", "gameTitle");
            }

            // Save database
            using (Stream stream = File.Create(".\\GameData\\Formats.dat"))
            {
                GameRes.FormatCatalog.Instance.SerializeScheme(stream);
            }
        }
    }
}