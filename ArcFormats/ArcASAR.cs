//! \file       ArcASAR.cs
//! \date       2025-12-23
//! \brief      Electron/Atom Shell resource archive format.
//
// Copyright (C) 2025 by morkt
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
using System.Text;
using Newtonsoft.Json;

namespace GameRes.Formats.Chromium
{
    class AsarNode
    {
        [JsonProperty("files")]
        public Dictionary<string, AsarNode> Files { get; set; }

        [JsonProperty("size")]
        public uint Size { get; set; }

        [JsonProperty("offset")]
        public string Offset { get; set; }
    }

    [Export(typeof(ArchiveFormat))]
    public class AsarOpener : ArchiveFormat
    {
        public override string         Tag { get { return "ASAR"; } }
        public override string Description { get { return "Electron/Atom Shell archive format"; } }
        public override uint     Signature { get { return 0; } }
        public override bool  IsHierarchic { get { return true; } }
        public override bool      CanWrite { get { return false; } }

        public AsarOpener ()
        {
            Extensions = new string[] { "asar" };
        }

        public override ArcFile TryOpen (ArcView file)
        {
            uint index_size = file.View.ReadUInt32 (0x0C);
            string json = file.View.ReadString (0x10, index_size, Encoding.UTF8);
            var dict = JsonConvert.DeserializeObject<AsarNode> (json);
            var dir = new List<Entry> ();
            ParseIndex (dir, dict, (uint)((index_size + 0x10 + 3) & ~3));
            return new ArcFile (file, this, dir);
        }

        internal void ParseIndex (List<Entry> dir, AsarNode dict, uint pad, string cur = "") {
            if (dict.Files != null)
            {
                foreach (var kv in dict.Files)
                {
                    string k = kv.Key;
                    AsarNode v = kv.Value;
                    ParseIndex (dir, v, pad, cur != "" ? $"{cur}/{k}" : k);
                }
            }
            else
            {
                var entry = new Entry {
                    Name = cur,
                    Size = dict.Size,
                    Offset = uint.Parse (dict.Offset) + pad,
                    Type = FormatCatalog.Instance.GetTypeFromName (cur)
                };
                dir.Add (entry);
            }
        }
    }
}
