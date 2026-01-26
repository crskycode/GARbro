using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using GameRes.Utility;

namespace GameRes.Formats.Leaf
{
    [Export(typeof(ArchiveFormat))]
    public class VideoOpener : ArchiveFormat
    {
        public override string         Tag { get { return "VIDEO/LEAF"; } }
        public override string Description { get { return "Leaf/Aquaplus Video Container"; } }
        public override uint     Signature { get { return 0; } } // Dynamic verification
        public override bool  IsHierarchic { get { return false; } }
        public override bool      CanWrite { get { return false; } }

        public override ArcFile TryOpen(ArcView file)
        {
            // Read the first 16 bytes to check the signature
            if (file.MaxOffset < 16)
                return null;

            string ext = null;
            uint head = file.View.ReadUInt32(0);

            // 1. Check if it is WMV/ASF (Leaf standard)
            // GUID: 30 26 B2 75 8E 66 CF 11 ...
            // In Little Endian UInt64: 0x11CF668E75B22630
            if (head == 0x75B22630)
            {
                ulong asfGuid = file.View.ReadUInt64(0);
                if (asfGuid == 0x11CF668E75B22630)
                {
                    ext = ".wmv";
                }
            }
            // 2. Check if it is AVI (RIFF ... AVI )
            else if (head == 0x46464952) // "RIFF"
            {
                // Check if the type at offset 8 is "AVI "
                if (file.View.ReadUInt32(8) == 0x20495641) 
                {
                    ext = ".avi";
                }
            }
            // 3. Check if it is MPEG (Reusing logic, in case an old game uses it)
            else if (head == 0xBA010000)
            {
                ext = ".mpg";
            }

            if (ext == null)
                return null;

            // Create the virtual entry with the correct extension
            var entry = new Entry
            {
                Name = Path.GetFileNameWithoutExtension(file.Name) + ext,
                Type = "video",
                Offset = 0,
                Size = (uint)file.MaxOffset
            };

            return new ArcFile(file, this, new List<Entry> { entry });
        }
    }
}