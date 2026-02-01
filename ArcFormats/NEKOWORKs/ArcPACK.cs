//! \file       ArcPACK.cs
//! \date       2026 Feb 01
//! \brief      NEKO WORKs Unity PACK resource archive.
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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using GameRes.Formats;

namespace GameRes.Formats.NEKOWORKs
{
    [Export(typeof(ArchiveFormat))]
    public class NekoWorksPackOpener : ArchiveFormat
    {
        public override string Tag { get { return "PACK/EXFS"; } }
        public override string Description { get { return "NEKO WORKs resource archive"; } }
        public override uint Signature { get { return 0x53465845; } } // 'EXFS'
        public override bool IsHierarchic { get { return true; } }
        public override bool CanWrite { get { return false; } }

        public override ArcFile TryOpen(ArcView file)
        {
            if (!file.View.AsciiEqual(0, "EXFS"))
                return null;

            int fileCount = file.View.ReadInt32(0x0C);
            uint infoOffset = file.View.ReadUInt32(0x10);
            uint infoSize = file.View.ReadUInt32(0x18);
            uint nameBlockSize = file.View.ReadUInt32(0x20);
            uint baseDataOffset = file.View.ReadUInt32(0x28);

            if (!IsSaneCount(fileCount))
                return null;

            long namesOffset = infoOffset + infoSize;
            if (namesOffset + nameBlockSize > file.MaxOffset)
                return null;

            byte[] nameData = file.View.ReadBytes(namesOffset, nameBlockSize);
            var nameList = new List<string>(fileCount);
            int start = 0;
            for (int i = 0; i < nameData.Length; ++i)
            {
                if (nameData[i] == (byte)'\n')
                {
                    if (i > start)
                        nameList.Add(System.Text.Encoding.UTF8.GetString(nameData, start, i - start));
                    start = i + 1;
                }
            }

            if (nameList.Count < fileCount)
                return null;

            var dir = new List<Entry>(fileCount);
            long indexOffset = infoOffset;

            for (int i = 0; i < fileCount; ++i)
            {
                long entryPos = indexOffset + i * 0x20;

                uint dataOffset = file.View.ReadUInt32(entryPos + 0x10);
                uint dataSize = file.View.ReadUInt32(entryPos + 0x18);

                var entry = Create<Entry>(nameList[i]);
                entry.Offset = baseDataOffset + dataOffset;
                entry.Size = dataSize;

                if (!entry.CheckPlacement(file.MaxOffset))
                    return null;

                dir.Add(entry);
            }

            return new ArcFile(file, this, dir);
        }
    }
}
