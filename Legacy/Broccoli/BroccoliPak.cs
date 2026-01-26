using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using GameRes.Utility;

namespace GameRes.Formats.Broccoli
{
    [Export(typeof(ArchiveFormat))]
    public class PakOpener : ArchiveFormat
    {
        public override string         Tag { get { return "PAK/BROCCOLI"; } }
        public override string Description { get { return "Broccoli/Yuzusoft port resource archive"; } }
        public override uint     Signature { get { return 0; } } 
        public override bool  IsHierarchic { get { return true; } }
        public override bool      CanWrite { get { return false; } } 

        private static readonly byte[] FirstFileSignature = {
            0x73, 0x65, 0x5F, 0x63, 0x31, 0x31, 0x2E, 0x77, 0x61, 0x76 
        };

        public override ArcFile TryOpen(ArcView file)
        {
            int bufferSize = 2048;
            if (file.MaxOffset < bufferSize) 
                bufferSize = (int)file.MaxOffset;

            var buffer = file.View.ReadBytes(0, (uint)bufferSize);

            int signaturePos = FindSignature(buffer, FirstFileSignature);
            
            if (signaturePos == -1)
                return null;

            if (signaturePos < 8)
                return null;

            long countOffset = signaturePos - 8;
            int count = file.View.ReadInt32(countOffset);

            if (!IsSaneCount(count))
                return null;

            long tableStart = signaturePos;
            long entrySize = 64;
            long tableByteSize = count * entrySize;
            
            long dataBlobStart = tableStart + tableByteSize;

            if (dataBlobStart > file.MaxOffset)
                return null;

            var dir = new List<Entry>(count);

            for (int i = 0; i < count; i++)
            {
                long entryPos = tableStart + (i * entrySize);
                
                var nameBytes = file.View.ReadBytes(entryPos, 48);
                
                // --- FIX APPLIED HERE ---
                // Replaced Binary.Ascii with System.Text.Encoding.ASCII
                if (nameBytes.Length >= 4 && 
                   (System.Text.Encoding.ASCII.GetString(nameBytes, 0, 4) == "RIFF" || 
                    System.Text.Encoding.ASCII.GetString(nameBytes, 0, 4) == "OggS"))
                {
                    break;
                }
                // ------------------------

                string name = Binary.GetCString(nameBytes, 0, nameBytes.Length, Encodings.cp932);
                
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                name = name.TrimEnd('\0');

                uint offset = file.View.ReadUInt32(entryPos + 48);
                uint size   = file.View.ReadUInt32(entryPos + 56);

                if (size == 0)
                    continue;

                var entry = FormatCatalog.Instance.Create<Entry>(name);
                
                entry.Offset = dataBlobStart + offset;
                entry.Size = size;

                if (!entry.CheckPlacement(file.MaxOffset))
                    return null;

                dir.Add(entry);
            }

            return new ArcFile(file, this, dir);
        }

        private int FindSignature(byte[] buffer, byte[] signature)
        {
            if (buffer.Length < signature.Length) return -1;

            for (int i = 0; i <= buffer.Length - signature.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < signature.Length; j++)
                {
                    if (buffer[i + j] != signature[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found) return i;
            }
            return -1;
        }
    }
}