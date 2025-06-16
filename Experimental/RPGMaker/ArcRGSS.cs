//! \file       ArcRGSS.cs
//! \date       2025 June 8
//! \brief      RPG Maker resource archive implementation.
//
// Copyright (C) 2025 by morkt and others
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

namespace GameRes.Formats.RPGMaker
{
    [Export(typeof(ArchiveFormat))]
    public class RgssArchive : ArchiveFormat
    {
        public override string   Tag { get { return "RGSSAD"; } }
        public override string   Description { get { return "RPG Maker XP/VX/ACE engine resource archive"; } }
        public override uint     Signature { get { return 0x53534752; } } // 'RGSS'
        public override bool     IsHierarchic { get { return true; } }
        public override bool     CanWrite { get { return true; } }

        public RgssArchive ()
        {
            Extensions = new string[] { "rgss3a", "rgss2a", "rgssad" };
        }

        public override ArcFile TryOpen (ArcView file)
        {
            if (!file.View.AsciiEqual (4, "AD\0"))
                return null;
            int version = file.View.ReadByte (7);
            using (var index = file.CreateStream())
            {
                List<Entry> dir = null;
                if (3 == version)
                    dir = ReadIndexV3 (index);
                else if (1 == version)
                    dir = ReadIndexV1 (index);
                if (null == dir || 0 == dir.Count)
                    return null;
                return new ArcFile (file, this, dir);
            }
        }

        List<Entry> ReadIndexV1 (IBinaryStream file)
        {
            var max_offset = file.Length;
            file.Position = 8;
            var key_gen = new KeyGenerator (0xDEADCAFE);
            var dir = new List<Entry>();
            while (file.PeekByte() != -1)
            {
                uint name_length = file.ReadUInt32() ^ key_gen.GetNext();
                var name_bytes   = file.ReadBytes((int)name_length);
                var name         = DecryptName(name_bytes, key_gen);

                var entry = FormatCatalog.Instance.Create<RgssEntry>(name);
                entry.Size   = file.ReadUInt32() ^ key_gen.GetNext();
                entry.Offset = file.Position;
                entry.Key    = key_gen.Current;
                if (!entry.CheckPlacement (max_offset))
                    return null;
                dir.Add (entry);
                file.Seek (entry.Size, SeekOrigin.Current);
            }
            return dir;
        }

        List<Entry> ReadIndexV3 (IBinaryStream file)
        {
            var max_offset = file.Length;
            file.Position = 8;
            uint key = file.ReadUInt32() * 9 + 3;
            var dir = new List<Entry>();
            while (file.PeekByte() != -1)
            {
                uint offset = file.ReadUInt32() ^ key;
                if (0 == offset)
                    break;
                uint size        = file.ReadUInt32() ^ key;
                uint entry_key   = file.ReadUInt32() ^ key;
                uint name_length = file.ReadUInt32() ^ key;
                var name_bytes   = file.ReadBytes ((int)name_length);
                var name         = DecryptName (name_bytes, key);

                var entry = FormatCatalog.Instance.Create<RgssEntry>(name);
                entry.Offset = offset;
                entry.Size   = size;
                entry.Key    = entry_key;
                if (!entry.CheckPlacement (max_offset))
                    return null;
                dir.Add (entry);
            }
            return dir;
        }

        public override Stream OpenEntry (ArcFile arc, Entry entry)
        {
            var rent = (RgssEntry)entry;
            var data = arc.File.View.ReadBytes (rent.Offset, rent.Size);
            XORDataWithPRKey (data, new KeyGenerator (rent.Key));
            return new BinMemoryStream (data);
        }

        public override void Create (
          Stream output, IEnumerable<Entry> list,
          ResourceOptions options, EntryCallback callback)
        {
            var rgss_options = GetOptions<RgssOptions>(options);
            int version = rgss_options.Version;

            /*if (version == 0 && options.Widget != null)
            {
                var widget = options.Widget as GUI.CreateRGSSWidget;
                if (widget != null && widget.Version.SelectedItem != null)
                {
                    var selected = widget.Version.SelectedItem as ComboBoxItem;
                    if (selected != null && selected.Tag != null)
                        version = (int)selected.Tag;
                }
            }*/

            if (version != 1 && version != 3)
                version = 3;

            var encoding = Encoding.UTF8;
            var entries = list.ToArray ();

            using (var writer = new BinaryWriter (output, encoding, true))
            {
                writer.Write (RgssArchive.DefaultHeader);
                writer.Write ((byte)version);

                if (version == 1)
                    WriteV1Archive (writer, entries, encoding, callback);
                else
                    WriteV3Archive (writer, entries, encoding, callback);
            }
        }

        void WriteV1Archive (BinaryWriter output, Entry[] entries, Encoding encoding, EntryCallback callback)
        {
            var key_gen = new KeyGenerator (0xDEADCAFE);
            long current_offset = output.BaseStream.Position;

            var output_dir = Environment.CurrentDirectory;
            foreach (var entry in entries)
            {
                if (null != callback)
                    callback (entries.Length, entry, null);

                string relativePath = GetRelativePath (entry.Name, output_dir);
                var name_bytes      = encoding.GetBytes (relativePath);
                var encrypted_name  = new byte[name_bytes.Length];
                Array.Copy (name_bytes, encrypted_name, name_bytes.Length);

                uint name_length_key = key_gen.GetNext();
                output.Write ((uint)encrypted_name.Length ^ name_length_key);

                EncryptName (name_bytes, key_gen);
                output.Write (encrypted_name, 0, encrypted_name.Length);

                using (var input = File.OpenRead (entry.Name))
                {
                    uint file_size = (uint)input.Length;
                    uint size_key  = key_gen.GetNext();
                    output.Write (file_size ^ size_key);
                    EncryptAndCopyStream (input, output, key_gen.Current);
                }
            }
        }

        void WriteV3Archive(BinaryWriter output, Entry[] entries, Encoding encoding, EntryCallback callback)
        {
            uint base_key = 0x55555555; // NOTE: This produces 0 key
            output.Write (base_key);
            uint key = base_key * 9 + 3;

            long index_size = 0;
            var entryKeys = new Dictionary<Entry, uint>();
            var relativePaths = new Dictionary<Entry, string>();

            var output_dir = Environment.CurrentDirectory;
            foreach (var entry in entries)
            {
                index_size += 16; // offset + size + entry_key + name_length
                string relativePath  = GetRelativePath (entry.Name, output_dir);
                relativePaths[entry] = relativePath;
                index_size += encoding.GetByteCount (relativePath);
            }
            index_size += 4; // for terminating zero

            long data_offset = RgssArchive.DefaultHeader.Length + 1 + 4 + index_size;

            // Write index
            foreach (var entry in entries)
            {
                using (var input = File.OpenRead (entry.Name))
                {
                    uint file_size = (uint)input.Length;

                    output.Write ((uint)data_offset ^ key);
                    output.Write (file_size ^ key);

                    uint entry_key = 0;
                    entryKeys[entry] = entry_key;
                    output.Write (entry_key ^ key);

                    // Use the stored relative path
                    string relativePath = relativePaths[entry];
                    var name_bytes      = encoding.GetBytes(relativePath);
                    output.Write ((uint)name_bytes.Length ^ key);

                    EncryptName (name_bytes, key);
                    output.Write (name_bytes, 0, name_bytes.Length);

                    data_offset += file_size;
                }
            }

            // Write terminator 0 ^ key = key
            output.Write((uint)key);

            // Write file data
            foreach (var entry in entries)
            {
                // callback is here because it's the slowest part
                if (null != callback)
                    callback (entries.Length, entry, null);
                using (var input = File.OpenRead (entry.Name))
                {
                    EncryptAndCopyStream (input, output, entryKeys[entry]);
                }
            }
        }

        private string GetRelativePath (string fullPath, string basePath)
        {
            // converts full path into a realtive one in the dir we choose to pack
            string relativePath = fullPath;
            if (relativePath.StartsWith(basePath))
            {
                relativePath = relativePath.Substring (basePath.Length);
                if (relativePath.StartsWith (@"\"))
                    relativePath = relativePath.Substring (1);
                var pos = relativePath.IndexOf (@"\");
                if (pos != -1)
                    relativePath = relativePath.Substring (pos+1);
            }

            return relativePath;//.Replace(@"\", "/");
        }


        void EncryptAndCopyStream (Stream input, BinaryWriter output, uint data_key)
        {
            var buffer = new byte[input.Length];
            int bytes_read;
            int position = 0;

            var key_gen = new KeyGenerator (data_key);
            while ((bytes_read = input.Read (buffer, 0, (int)input.Length)) > 0)
            {
                XORDataWithPRKey (buffer, key_gen);
                position += bytes_read;
                output.Write (buffer, 0, bytes_read);
            }
        }

        void XORDataWithPRKey (byte[] data, KeyGenerator key_gen, int position = 0)
        {
            uint key = key_gen.GetNext();
            for (int i = 0; i < data.Length;)
            {
                data[i] ^= (byte)(key >> (i << 3));
                ++i;
                if (0 == (i & 3))
                {
                    key = key_gen.GetNext();
                }
            }
        }

        string DecryptName (byte[] name, KeyGenerator key_gen)
        {
            EncryptName (name, key_gen);
            return Encoding.UTF8.GetString (name);
        }

        string DecryptName (byte[] name, uint key)
        {
            EncryptName (name, key);
            return Encoding.UTF8.GetString (name);
        }

        void EncryptName (byte[] name, KeyGenerator key_gen)
        {
            for (int i = 0; i < name.Length; ++i)
            {
                name[i] ^= (byte)key_gen.GetNext();
            }
        }

        void EncryptName (byte[] name, uint key)
        {
            for (int i = 0; i < name.Length; ++i)
            {
                name[i] ^= (byte)(key >> (i << 3));
            }
        }

        public override ResourceOptions GetDefaultOptions ()
        {
            return new RgssOptions { Version = 3 };
        }

        /*public override object GetCreationWidget ()
        {
            return new GUI.CreateRGSSWidget ();
        }*/

        internal static readonly byte[] DefaultHeader = { 0x52, 0x47, 0x53, 0x53, 0x41, 0x44, 0x00 };
    }

    internal class RgssEntry : Entry
    {
        public uint Key;
    }

    internal class KeyGenerator
    {
        uint    m_seed;
        public KeyGenerator (uint seed) { m_seed = seed; }
        public uint Current { get { return m_seed; } }
        public uint GetNext ()
        {
            uint key = m_seed;
            m_seed = m_seed * 7 + 3;
            return key;
        }
    }

    public class RgssOptions : ResourceOptions
    {
        public int Version { get; set; }
    }
}
