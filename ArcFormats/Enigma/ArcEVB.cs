//! \file       ArcEVB.cs
//! \date       2025-12-27
//! \brief      Enigma Virtual Box resource archive.
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
using System.Linq;
using System.Text;
using GameRes.Utility;

namespace GameRes.Formats.Enigma {
    public enum NodeTypes {
        File = 2,
        Folder = 3,
    }

    [Export(typeof(ArchiveFormat))]
    public class EvbPackOpener : ArchiveFormat {
        public override string         Tag { get { return "EVB"; } }
        public override string Description { get { return "Enigma Virtual Box resource archive"; } }
        public override uint     Signature { get { return 0x425645; } } // 'EVB'
        public override bool  IsHierarchic { get { return true; } }
        public override bool      CanWrite { get { return false; } }

        public override ArcFile TryOpen(ArcView file) {
            uint index_size = file.View.ReadUInt32(0x40) + 68;
            uint index_offset = 0x4F;
            uint file_offset = index_size;

            var dir = new List<Entry>();
            var name_buffer = new StringBuilder();
            var counts = new List<uint> { file.View.ReadUInt32(0x4C) };
            var names = new List<string> { "" };

            while (index_offset < index_size - 4) {
                uint item_count = file.View.ReadUInt32(index_offset + 12);
                index_offset += 16;
                name_buffer.Clear();
                while (true) {
                    char c = (char)file.View.ReadUInt16(index_offset);
                    index_offset += 2;
                    if (c == 0)
                        break;
                    name_buffer.Append(c);
                }
                if (name_buffer.Length == 0)
                    return null;
                var name = name_buffer.ToString();
                var type = (NodeTypes)file.View.ReadByte(index_offset);
                index_offset++;
                counts[counts.Count - 1]--;
                if (type == NodeTypes.File) {
                    var entry = Create<Entry>(Path.Combine(names.Append(name).ToArray()));
                    uint unpacked_size = file.View.ReadUInt32(index_offset + 2);
                    uint size = file.View.ReadUInt32(index_offset + 49);
                    if (unpacked_size != size)
                        return null; // packed entry not implemented
                    entry.Offset = file_offset;
                    entry.Size = size;
                    file_offset += size;
                    if (!entry.CheckPlacement(file.MaxOffset))
                        return null;
                    dir.Add(entry);
                    while (counts.Count > 0 && counts[counts.Count - 1] == 0) {
                        counts.RemoveAt(counts.Count - 1);
                        names.RemoveAt(names.Count - 1);
                    }
                    index_offset += 53;
                }
                else if (type == NodeTypes.Folder) {
                    counts.Add(item_count);
                    names.Add(name);
                    index_offset += 25;
                }
                else
                    return null;
            }

            return new ArcFile(file, this, dir);
        }
    }
}
