//! \file       ArcP00.cs
//! \date       2026-01-08
//! \brief      Broccoli resource archive format.
//
// Copyright (C) 2026 by morkt
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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Compression;
using GameRes.Utility;

namespace GameRes.Formats.Broccoli {
    internal class P00Entry : Entry {
        public string FileName;
    }

    [Export(typeof(ArchiveFormat))]
    public class P00Opener : ArchiveFormat {
        public override string         Tag { get { return "P00"; } }
        public override string Description { get { return "Broccoli multipart archive format"; } }
        public override uint     Signature { get { return 0; } }
        public override bool  IsHierarchic { get { return true; } }
        public override bool      CanWrite { get { return false; } }

        public P00Opener() {
            Extensions = new string[] { "p00" };
        }

        public override ArcFile TryOpen(ArcView file) {
            var index_file_name = Path.ChangeExtension(file.Name, "pak");
            if (!VFS.FileExists(index_file_name))
                return null;
            using (var index_file = VFS.OpenView(index_file_name)) {
                if (!index_file.View.AsciiEqual(0, "IPF "))
                    return null;

                int count = index_file.View.ReadInt32(4);
                if (!IsSaneCount(count))
                    return null;

                long index_offset = 8;
                var dir = new List<Entry>(count);
                for (int i = 0; i < count; i++) {
                    uint hash = index_file.View.ReadUInt32(index_offset);
                    string name = hash.ToString("X8");
                    var entry = Create<P00Entry>(name);
                    entry.Offset = index_file.View.ReadUInt32(index_offset + 4);
                    entry.Size   = index_file.View.ReadUInt32(index_offset + 8);
                    var data_file_name = Path.ChangeExtension(file.Name, string.Format("p{0:00}", entry.Size >> 28));
                    if (!VFS.FileExists(data_file_name))
                        return null;
                    entry.FileName = data_file_name;
                    dir.Add(entry);
                    index_offset += 12;
                }

                return new ArcFile(file, this, dir);
            }
        }

        public override Stream OpenEntry(ArcFile arc, Entry entry) {
            var pent = entry as P00Entry;
            using (var data_file = new ArcView(pent.FileName)) {
                var input = data_file.CreateStream(pent.Offset & 0xFFFFFFF, pent.Size);
                if (input.ReadUInt16() == 0x305A) { // 'Z0'
                    input.Seek(10, SeekOrigin.Begin);
                    return new DeflateStream(input, CompressionMode.Decompress);
                }
                else {
                    input.Seek(0, SeekOrigin.Begin);
                    return input;
                }
            }
        }
    }
}
