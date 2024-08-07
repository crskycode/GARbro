//! \file       ArcDX8.cs
//! \date       2022 Jun 05
//! \brief      DxLib archive version 8.
//
// Copyright (C) 2022 by morkt
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

using GameRes.Formats.Strings;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Windows.Navigation;




namespace GameRes.Formats.DxLib
{

    internal class DXA8PackedEntry : PackedEntry {
        public bool HuffmanCompressed { get; set; }
        public uint HuffmanSize { get; set; }
    }

    

        [Export(typeof(ArchiveFormat))]
    public class Dx8Opener : DxOpener
    {
        public override string         Tag { get { return "BIN/DXLIB"; } }
        public override string Description { get { return "DxLib archive version 8"; } }
        public override uint     Signature { get { return 0x00085844; } }
        public override bool  IsHierarchic { get { return true; } }
        public override bool      CanWrite { get { return false; } }

        public Dx8Opener ()
        {
            Extensions = new string[] { "dxa", "hud", "usi", "med", "dat", "bin", "bcx", "wolf" };
            Signatures = new[] { 0x00085844u };
        }

        //static readonly byte[] DefaultKey = new byte[] { 0xBE, 0xC8, 0x8A, 0xF5, 0x28, 0x50, 0xC9 };


        DxScheme DefaultScheme = new DxScheme { KnownKeys = new List<IDxKey>() };


        internal class DxHeaderV8 :DxHeader
        {
            public DXA8Flags Flags;
            public byte HuffmanKB; // oddly used only in Compression process not in decompression.
            //15 bytes of padding.
        }

        internal enum DXA8Flags : UInt32
        {
            DXA_FLAG_NO_KEY=1, //file is not encrypted
            DXA_FLAG_NO_HEAD_PRESS=1<<1, //do not compress the header after compressing individual entries
        }

        [Serializable]
        public class DXAOpts : ResourceOptions
        {
            public string Keyword;
        }

        public override ResourceOptions GetDefaultOptions()
        {
            return new DXAOpts
            {
                Keyword = Properties.Settings.Default.DXAPassword
            };
        }

        
        

        public override ResourceOptions GetOptions(object widget)
        {
            if (widget is GUI.WidgetDXA)
            {
                return new DXAOpts
                {
                    Keyword = ((GUI.WidgetDXA)widget).Password.Text
                };
            }
            return GetDefaultOptions();
        }

        public override object GetAccessWidget()
        {
            return new GUI.WidgetDXA();
        }

        public override ArcFile TryOpen (ArcView file)
        {
            var dx = new DxHeaderV8 {
                IndexSize  = file.View.ReadUInt32 (4),
                BaseOffset = file.View.ReadInt64 (8),
                IndexOffset = file.View.ReadInt64 (0x10),
                FileTable  = file.View.ReadInt64 (0x18),
                DirTable   = file.View.ReadInt64 (0x20),
                CodePage   = file.View.ReadInt32 (0x28),
                Flags      = (DXA8Flags)file.View.ReadUInt32(0x2C),
                HuffmanKB = file.View.ReadByte(0x30)
            };
            if (dx.DirTable >= dx.IndexSize || dx.FileTable >= dx.IndexSize)
                return null;
            DxKey8 key = null;
            
            //FIXME: ReadBytes sets hard cap of filesize to 4GB.
            var headerBuffer = file.View.ReadBytes(dx.IndexOffset, (uint)(file.MaxOffset-dx.IndexOffset));
            bool isencrypted = (dx.Flags & DXA8Flags.DXA_FLAG_NO_KEY) == 0;
           
            if (isencrypted)
            {
                var keyStr = Query<DXAOpts>(arcStrings.ZIPEncryptedNotice).Keyword;
                key = new DxKey8(keyStr);

                
            }

            Decrypt(headerBuffer, 0, headerBuffer.Length, 0, key.Key);
            //Decrypted but might be compressed
            if ((dx.Flags & DXA8Flags.DXA_FLAG_NO_HEAD_PRESS) == 0)
            {
                byte[] huffmanBuffer = new byte[headerBuffer.Length]; 
                byte[] lzBuffer;
                headerBuffer.CopyTo(huffmanBuffer, 0);
                //huffmanBuffer = headerBuffer;
                HuffmanDecoder decoder = new HuffmanDecoder(huffmanBuffer, (ulong)huffmanBuffer.LongLength);
                lzBuffer = decoder.Unpack();
                MemoryStream lzStream = new MemoryStream(lzBuffer);
                headerBuffer = Unpack(lzStream);
                
            }

            
            var readyStr = new MemoryStream(headerBuffer);
            ArcView arcView = new ArcView(readyStr, "hdr",(uint)headerBuffer.LongLength);
            List<Entry> entries;
            //There MAY be the case where the singular file is over 4GB, but it's very rare. 
            using (var indexStr = arcView.CreateStream(0, (uint)dx.IndexSize))
            using (var reader = IndexReader.Create(dx, 8, indexStr))
            {
                 entries = reader.Read();
            }
            return new DxArchive(arcView, this,entries ,key, 8);
            //return null;
        }
    }

    internal sealed class IndexReaderV8 : IndexReader
    {
        readonly int m_entry_size;
        public IndexReaderV8(DxHeader header, int version, Stream input) : base(header, version, input)
        {
            m_entry_size = 0x48;
        }
        private class DxDirectory
        {
            public long DirOffset;
            public long ParentDirOffset;
            public int FileCount;
            public long FileTable;
        }

        DxDirectory ReadDirEntry()
        {
            var dir = new DxDirectory
            {
                DirOffset = m_input.ReadInt64(),
                ParentDirOffset = m_input.ReadInt64(),
                FileCount = (int)m_input.ReadInt64(),
                FileTable = m_input.ReadInt64()
            };
            return dir;
        }

        protected override void ReadFileTable(string root, long table_offset)
        {
            m_input.Position = m_header.DirTable + table_offset;
            var dir = ReadDirEntry();
            if (dir.DirOffset != -1 && dir.ParentDirOffset != -1)
            {
                m_input.Position = m_header.FileTable + dir.DirOffset;
                root = Path.Combine(root, ExtractFileName(m_input.ReadInt64()));
            }
            long current_pos = m_header.FileTable + dir.FileTable;
            for (int i = 0; i < dir.FileCount; ++i)
            {
                m_input.Position = current_pos;
                var name_offset = m_input.ReadInt64();
                uint attr = (uint)m_input.ReadInt64();
                m_input.Seek(0x18, SeekOrigin.Current);
                var offset = m_input.ReadInt64();
                if (0 != (attr & 0x10)) // FILE_ATTRIBUTE_DIRECTORY
                {
                    if (0 == offset || table_offset == offset)
                        throw new InvalidFormatException("Infinite recursion in DXA directory index");
                    ReadFileTable(root, offset);
                }
                else
                {
                    var size = m_input.ReadInt64();
                    var packed_size = m_input.ReadInt64();
                    var huffman_packed_size = m_input.ReadInt64();
                    var entry = FormatCatalog.Instance.Create<DXA8PackedEntry>(Path.Combine(root, ExtractFileName(name_offset)));
                    entry.Offset = m_header.BaseOffset + offset;
                    entry.UnpackedSize = (uint)size;
                    entry.IsPacked = -1 != packed_size;
                    entry.HuffmanCompressed = -1 != huffman_packed_size;
                    entry.HuffmanSize = (uint)huffman_packed_size;
                    if (entry.IsPacked)
                        entry.Size = (uint)(huffman_packed_size!=-1 ? huffman_packed_size:packed_size);
                    else
                        entry.Size = (uint)size;
                    m_dir.Add(entry);
                }
                current_pos += m_entry_size;
            }
        }
    }
}
