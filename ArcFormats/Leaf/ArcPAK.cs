//! \file       ArcPAK.cs
//! \date       2018 Nov 04
//! \brief      Leaf resource archive.
//
// Copyright (C) 2018-2019 by morkt
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using GameRes.Compression;
using GameRes.Utility;
using GameRes.Utility.Serialization;

namespace GameRes.Formats.Leaf
{
    [Export(typeof(ArchiveFormat))]
    public class KcapOpener : ArchiveFormat
    {
        public override string         Tag { get { return "PAK/KCAP"; } }
        public override string Description { get { return "Leaf resource archive"; } }
        public override uint     Signature { get { return 0x5041434B; } } // 'KCAP'
        public override bool  IsHierarchic { get { return false; } }
        public override bool      CanWrite { get { return true; } }

        public KcapOpener ()
        {
            ContainedFormats = new[] { "TGA", "BJR", "BMP", "OGG", "WAV", "AMP/LEAF", "SCR" };
        }

        public override ArcFile TryOpen (ArcView file)
        {
            int version = -1;
            int count = file.View.ReadInt32 (4);
            uint index_offset = 8;
            uint first_offset = file.View.ReadUInt32 (0x20);
            if (IsSaneCount (count))
            {
                if (count * 0x20 + 8 == first_offset)
                {
                    version = 0;
                }
                else
                {
                    first_offset = file.View.ReadUInt32 (0x24);
                    if (count * 0x24 + 8 == first_offset)
                        version = 1;
                }
            }
            if (version < 0)
            {
                count = file.View.ReadInt32 (8);
                first_offset = file.View.ReadUInt32 (0x28);
                if (IsSaneCount (count) && count * 0x24 + 0xC == first_offset)
                {
                    version = 1;
                    index_offset = 0xC;
                }
                else
                {
                    count = file.View.ReadInt32 (12);
                    first_offset = file.View.ReadUInt32 (0x34);
                    if (IsSaneCount (count) && count * 0x2C + 0x10 == first_offset)
                    {
                        version = 2;
                        index_offset = 0x10;
                    }
                }
            }
            List<Entry> dir = null;
            switch (version)
            {
            case 0: dir = ReadIndex<EntryDefV0> (file, count, index_offset); break;
            case 1: dir = ReadIndex<EntryDefV1> (file, count, index_offset); break;
            case 2: dir = ReadIndex<EntryDefV2> (file, count, index_offset); break;
            default: return null;
            }
            if (null == dir)
                return null;
            return new ArcFile (file, this, dir);
        }

        List<Entry> ReadIndex<EntryDef> (ArcView file, int count, uint index_offset)
            where EntryDef : IEntryDefinition, new()
        {
            using (var input = file.CreateStream())
            {
                input.Position = index_offset;
                var dir = new List<Entry> (count);
                var def = new EntryDef();
                for (int i = 0; i < count; ++i)
                {
                    input.ReadStruct (out def);
                    if (def.Size != 0)
                    {
                        var entry = Create<PackedEntry> (def.Name);
                        entry.Offset = def.Offset;
                        entry.Size   = def.Size;
                        if (!entry.CheckPlacement (file.MaxOffset))
                            return null;
                        entry.IsPacked = def.IsPacked;
                        dir.Add (entry);
                    }
                }
                return dir;
            }
        }

        public override Stream OpenEntry (ArcFile arc, Entry entry)
        {
            var pent = entry as PackedEntry;
            if (null == pent || !pent.IsPacked)
                return base.OpenEntry (arc, entry);
            if (0 == pent.UnpackedSize)
                pent.UnpackedSize = arc.File.View.ReadUInt32 (entry.Offset+4);
            var input = arc.File.CreateStream (entry.Offset+8, entry.Size-8);
            return new LzssStream (input);
        }

        public override IImageDecoder OpenImage (ArcFile arc, Entry entry)
        {
            if (!entry.Name.HasExtension (".tga"))
                return base.OpenImage (arc, entry);
            var input = arc.OpenBinaryEntry (entry);
            try
            {
                var header = input.ReadHeader (18);
                if (0 == header[16])
                    header[16] = 32;
                if (0 == header[17] && 32 == header[16])
                    header[17] = 8;
                Stream tga_input = new StreamRegion (input.AsStream, 18);
                tga_input = new PrefixStream (header.ToArray(), tga_input);
                var tga = new BinaryStream (tga_input, entry.Name);
                var info = ImageFormat.Tga.ReadMetaData (tga);
                if (info != null)
                {
                    tga.Position = 0;
                    return new ImageFormatDecoder (tga, ImageFormat.Tga, info);
                }
            }
            catch { /* ignore errors */ }
            input.Position = 0;
            return ImageFormatDecoder.Create (input);
        }

        // --- REPACK IMPLEMENTATION ---

        public override void Create (Stream output, IEnumerable<Entry> entries, ResourceOptions options, EntryCallback callback)
        {
            int count = entries.Count();
            using (var writer = new BinaryWriter (output, Encoding.ASCII, true))
            {
                // 1. Write Header (KCAP v2)
                writer.Write (0x5041434B); // "KCAP"
                writer.Write (0xFFFFFFFF);
                writer.Write (0x0000FFFF);
                writer.Write (count);

                long tableOffset = 16;
                long dataOffset = tableOffset + (count * 44);

                writer.BaseStream.Position = dataOffset;

                var entryInfos = new List<EntryInfo>();
                int i = 0;

                foreach (var entry in entries)
                {
                    if (callback != null) 
                        callback (i + 1, entry, null);

                    long currentOffset = writer.BaseStream.Position;
                    
                    using (var input = File.OpenRead(entry.Name))
                    {
                        byte[] rawData = new byte[input.Length];
                        input.Read (rawData, 0, (int)input.Length);

                        // Compress
                        byte[] compressedData = LeafLzss.Compress (rawData);

                        // Write Data (Prefix + LZSS)
                        writer.Write ((uint)compressedData.Length);
                        writer.Write ((uint)rawData.Length);
                        writer.Write (compressedData);

                        uint totalSize = (uint)(compressedData.Length + 8);

                        entryInfos.Add (new EntryInfo
                        {
                            Name = Path.GetFileName (entry.Name),
                            UnpackedSize = (uint)rawData.Length,
                            Offset = (uint)currentOffset,
                            PackedSize = totalSize
                        });
                    }
                    i++;
                }

                // Write File Table
                writer.BaseStream.Position = tableOffset;
                foreach (var info in entryInfos)
                {
                    writer.Write (1); // Type
                    
                    byte[] nameBytes = Encodings.cp932.GetBytes (info.Name);
                    if (nameBytes.Length > 23) Array.Resize (ref nameBytes, 23);
                    writer.Write (nameBytes);
                    for (int k = nameBytes.Length; k < 24; k++) writer.Write ((byte)0);

                    writer.Write (0xFFFFFFFF); // CRC
                    writer.Write (info.UnpackedSize);
                    writer.Write (info.Offset);
                    writer.Write (info.PackedSize);
                }
            }
        }

        struct EntryInfo
        {
            public string Name;
            public uint UnpackedSize;
            public uint Offset;
            public uint PackedSize;
        }
    }

    [Export(typeof(ScriptFormat))]
    public class AmpFormat : GenericScriptFormat
    {
        public override string        Type { get { return ""; } }
        public override string         Tag { get { return "AMP/LEAF"; } }
        public override string Description { get { return "Leaf engine internal file"; } }
        public override uint     Signature { get { return 0; } }
    }

    [Export(typeof(ResourceAlias))]
    [ExportMetadata("Extension", "SDT")]
    [ExportMetadata("Target", "SCR")]
    public class SdtFormat : ResourceAlias { }

    internal interface IEntryDefinition
    {
        string   Name { get; }
        long   Offset { get; }
        uint     Size { get; }
        bool IsPacked { get; }
    }

    #pragma warning disable 649,169
    internal struct EntryDefV0 : IEntryDefinition
    {
        [CString(Length = 0x18)]
        string  _name;
        uint    _offset;
        uint    _size;

        public string   Name { get { return _name; } }
        public long   Offset { get { return _offset; } }
        public uint     Size { get { return _size; } }
        public bool IsPacked { get { return true; } }
    }

    internal struct EntryDefV1 : IEntryDefinition
    {
        int     _is_packed;
        [CString(Length = 0x18)]
        string  _name;
        uint    _offset;
        uint    _size;

        public string   Name { get { return _name; } }
        public long   Offset { get { return _offset; } }
        public uint     Size { get { return _size; } }
        public bool IsPacked { get { return _is_packed != 0; } }
    }

    internal struct EntryDefV2 : IEntryDefinition
    {
        int     _is_packed;
        [CString(Length = 0x18)]
        string  _name;
        uint    _crc;
        uint    _unpacked_size;
        uint    _offset;
        uint    _size;

        public string   Name { get { return _name; } }
        public long   Offset { get { return _offset; } }
        public uint     Size { get { return _size; } }
        public bool IsPacked { get { return _is_packed != 0; } }
    }
    #pragma warning restore 649,169

    // --- LEAF LZSS COMPRESSION ---
    internal static class LeafLzss
    {
        const int N = 4096;
        const int F = 18;
        const int THR = 2;
        const int NIL = N;

        public static byte[] Compress (byte[] input)
        {
            if (input.Length == 0) return new byte[0];

            using (var outStream = new MemoryStream (input.Length))
            {
                // Arrays size = N + 257 to handle Root Nodes (N+1..N+256) safely
                int[] lson = new int[N + 257];
                int[] rson = new int[N + 257];
                int[] dad  = new int[N + 257];
                byte[] text_buf = new byte[N + F - 1];

                for (int j = N + 1; j <= N + 256; j++) rson[j] = NIL;
                for (int j = 0; j < N; j++) dad[j] = NIL;

                int match_position = 0, match_length = 0;

                void InsertNode (int r_node)
                {
                    int i, p, cmp;
                    int key_pos = r_node;
                    
                    // Root for this character
                    p = N + 1 + text_buf[key_pos];
                    
                    rson[r_node] = lson[r_node] = NIL;
                    match_length = 0;
                    
                    cmp = 1; // Initial state

                    for (; ; )
                    {
                        if (cmp >= 0)
                        {
                            if (rson[p] != NIL) p = rson[p];
                            else { rson[p] = r_node; dad[r_node] = p; return; }
                        }
                        else
                        {
                            if (lson[p] != NIL) p = lson[p];
                            else { lson[p] = r_node; dad[r_node] = p; return; }
                        }
                        
                        // Compare bytes only after finding a valid child node 'p' (buffer index)
                        for (i = 1; i < F; i++)
                            if ((cmp = text_buf[key_pos + i] - text_buf[p + i]) != 0) break;
                        
                        if (i > match_length)
                        {
                            match_position = p;
                            match_length = i;
                            if (match_length >= F) break;
                        }
                    }
                    
                    // Replace node logic
                    dad[r_node] = dad[p]; lson[r_node] = lson[p]; rson[r_node] = rson[p];
                    dad[lson[p]] = r_node; dad[rson[p]] = r_node;
                    if (rson[dad[p]] == p) rson[dad[p]] = r_node;
                    else lson[dad[p]] = r_node;
                    dad[p] = NIL;
                }

                void DeleteNode (int p)
                {
                    int q;
                    if (dad[p] == NIL) return;
                    if (rson[p] == NIL) q = lson[p];
                    else if (lson[p] == NIL) q = rson[p];
                    else
                    {
                        q = lson[p];
                        if (rson[q] != NIL)
                        {
                            do { q = rson[q]; } while (rson[q] != NIL);
                            rson[dad[q]] = lson[q]; dad[lson[q]] = dad[q];
                            lson[q] = lson[p]; dad[lson[p]] = q;
                        }
                        rson[q] = rson[p]; dad[rson[p]] = q;
                    }
                    dad[q] = dad[p];
                    if (rson[dad[p]] == p) rson[dad[p]] = q;
                    else lson[dad[p]] = q;
                    dad[p] = NIL;
                }

                int code_buf_ptr = 1;
                byte[] code_buf = new byte[17];
                byte mask = 1;
                int s = 0, r = N - F;
                int len = 0;

                for (int j = 0; j < r; j++) text_buf[j] = 0x20; 

                int bytes_read = 0;
                for (len = 0; len < F && bytes_read < input.Length; len++)
                    text_buf[r + len] = input[bytes_read++];

                if (len == 0) return new byte[0];

                for (int j = 1; j <= F; j++) InsertNode (r - j);
                InsertNode (r);

                do
                {
                    if (match_length > len) match_length = len;
                    if (match_length <= THR)
                    {
                        match_length = 1;
                        code_buf[0] |= mask;
                        code_buf[code_buf_ptr++] = text_buf[r];
                    }
                    else
                    {
                        code_buf[code_buf_ptr++] = (byte)(match_position & 0xFF);
                        code_buf[code_buf_ptr++] = (byte)(((match_position >> 4) & 0xF0) | (match_length - (THR + 1)));
                    }

                    if ((mask <<= 1) == 0)
                    {
                        outStream.Write (code_buf, 0, code_buf_ptr);
                        code_buf[0] = 0; code_buf_ptr = 1; mask = 1;
                    }

                    int last_match_length = match_length;
                    
                    int i;
                    for (i = 0; i < last_match_length && bytes_read < input.Length; i++)
                    {
                        DeleteNode (s);
                        byte c = input[bytes_read++];
                        text_buf[s] = c;
                        if (s < F - 1) text_buf[s + N] = c;
                        s = (s + 1) & (N - 1);
                        r = (r + 1) & (N - 1);
                        InsertNode (r);
                    }
                    
                    while (bytes_read == input.Length && i++ < last_match_length)
                    {
                        DeleteNode (s);
                        s = (s + 1) & (N - 1);
                        r = (r + 1) & (N - 1);
                        if (--len != 0) InsertNode (r);
                    }

                } while (len > 0);

                if (code_buf_ptr > 1)
                    outStream.Write (code_buf, 0, code_buf_ptr);

                return outStream.ToArray();
            }
        }
    }
}