using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using GameRes.Utility;

namespace GameRes.Formats.Broccoli
{
    [Export(typeof(ArchiveFormat))]
    public class MpegVideoOpener : ArchiveFormat
    {
        public override string         Tag { get { return "MPEG/BROCCOLI"; } }
        public override string Description { get { return "Broccoli/Generic MPEG Video"; } }
        public override uint     Signature { get { return 0xBA010000; } } 
        public override bool  IsHierarchic { get { return false; } }
        public override bool      CanWrite { get { return false; } }

        public override ArcFile TryOpen(ArcView file)
        {
            if (file.View.ReadUInt32(0) != 0xBA010000)
                return null;

            var entry = new Entry
            {
                Name = Path.GetFileNameWithoutExtension(file.Name) + ".mpg",
                Type = "video",
                Offset = 0,
                // CORREÇÃO: Adicionado (uint) para converter o tamanho
                Size = (uint)file.MaxOffset 
            };

            return new ArcFile(file, this, new List<Entry> { entry });
        }
    }
}