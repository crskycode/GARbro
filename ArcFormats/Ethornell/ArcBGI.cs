//! \file       ArcBGI.cs
//! \date       Tue Sep 09 09:29:12 2014
//! \brief      BGI/Ethornell engine archive implementation.
//
// Copyright (C) 2014-2015 by morkt
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
using Newtonsoft.Json;
using GameRes.Utility;
using GameRes.Formats.Ethornell;

namespace GameRes.Formats.BGI
{
    [Export(typeof(ArchiveFormat))]
    public class ArcOpener : ArchiveFormat
    {
        public override string         Tag { get { return "BGI"; } }
        public override string Description { get { return "BGI/Ethornell engine resource archive"; } }
        public override uint     Signature { get { return 0x6b636150; } } // "Pack"
        public override bool  IsHierarchic { get { return false; } }
        public override bool      CanWrite { get { return true; } }

        [NonSerialized]
        private Dictionary<string, uint> m_extracted_keys = new Dictionary<string, uint>();

        [NonSerialized]
        private MissingKeyAction? m_applyMissingKeyAction = null;

        static BgiScheme DefaultScheme = new BgiScheme { knownArchives = new Dictionary<string, BgiArchiveSettings>() };

        public override ResourceScheme Scheme
        {
            get { return DefaultScheme; }
            set { DefaultScheme = (BgiScheme)value; }
        }

        public ArcOpener ()
        {
            Extensions = new string[] { "arc" };
            ContainedFormats = new[] { "BGI", "CompressedBG", "BW", "SCR" };
        }

        public override ResourceOptions GetDefaultOptions()
        {
            return new BgiOptions
            {
                ArchiveVersion = Properties.Settings.Default.BGIArchiveVersion,
                CompressFiles = Properties.Settings.Default.BGICompressFiles,
                KeyFilePath = Properties.Settings.Default.BGIKeyFilePath ?? string.Empty
            };
        }

        public override ResourceOptions GetOptions(object widget)
        {
            return GetDefaultOptions();
        }

        public override object GetCreationWidget()
        {
            return new GUI.CreateBGIWidget();
        }

        public override ArcFile TryOpen (ArcView file)
        {
            if (!file.View.AsciiEqual (4, "File    "))
                return null;
            uint count = file.View.ReadUInt32 (12);
            if (count > 0xfffff)
                return null;
            uint index_size = 0x20 * count;
            if (index_size > file.View.Reserve (0x10, index_size))
                return null;
            var dir = new List<Entry> ((int)count);
            long index_offset = 0x10;
            long base_offset = index_offset + index_size;
            for (uint i = 0; i < count; ++i)
            {
                string name = file.View.ReadString (index_offset, 0x10);
                var entry = Create<PackedEntry> (name);
                entry.Offset = base_offset + file.View.ReadUInt32 (index_offset+0x10);
                entry.Size   = file.View.ReadUInt32 (index_offset+0x14);
                if (!entry.CheckPlacement (file.MaxOffset))
                    return null;
                dir.Add (entry);
                index_offset += 0x20;
            }
            foreach (var entry in dir.Where (e => string.IsNullOrEmpty (e.Type)))
            {
                if (file.View.AsciiEqual (entry.Offset, "CompressedBG"))
                    entry.Type = "image";
                else if (file.View.AsciiEqual (entry.Offset+4, "bw  "))
                    entry.Type = "audio";
            }
            return new ArcFile (file, this, dir);
        }

        public override Stream OpenEntry (ArcFile arc, Entry entry)
        {
            var entry_offset = entry.Offset;
            var input = arc.File.CreateStream (entry_offset, entry.Size);
            var pent = entry as PackedEntry;
            if (null == pent)
                return input;
            if (!pent.IsPacked)
            {
                if (pent.Size <= 0x220 || !arc.File.View.AsciiEqual (entry_offset, "DSC FORMAT 1.00\0"))
                    return input;
                pent.IsPacked = true;
                pent.UnpackedSize = arc.File.View.ReadUInt32 (entry_offset+0x14);
            }
            try
            {
                using (var decoder = new DscDecoder (input))
                {
                    decoder.Unpack();

                    string filename = Path.GetFileNameWithoutExtension(entry.Name);
                    uint key = decoder.Key;

                    lock (m_extracted_keys)
                    {
                        m_extracted_keys[filename] = key;
                    }

                    System.Diagnostics.Trace.WriteLine($"Captured key for {filename}: 0x{key:X8}");

                    return new BinMemoryStream (decoder.Output, entry.Name);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine (ex.Message, "BgiOpener");
                return arc.File.CreateStream (entry_offset, entry.Size);
            }
        }

        public void OnExtractionComplete (string targetDirectory)
        {
            if (m_extracted_keys.Count == 0)
                return;

            try
            {
                string localKeyPath = BgiKeyManager.GetLocalKeyPath(targetDirectory);

                // Load existing keys
                Dictionary<string, uint> existingKeys;
                if (File.Exists(localKeyPath))
                {
                    existingKeys = BgiKeyManager.LoadKeys(localKeyPath);
                    var conflicts = BgiKeyManager.MergeKeys(existingKeys, m_extracted_keys);

                    if (conflicts.Count > 0)
                    {
                        var resolvedKeys = ShowKeyConflictDialog(conflicts, existingKeys);
                        if (resolvedKeys != null)
                        {
                            existingKeys = resolvedKeys;
                        }
                        else
                        {
                            // User canceled
                            System.Diagnostics.Trace.WriteLine("Key conflict resolution canceled by user", "BgiOpener");
                            ClearExtractedKeys();
                            return;
                        }
                    }
                }
                else
                {
                    existingKeys = new Dictionary<string, uint>(m_extracted_keys);
                }

                BgiKeyManager.SaveKeys(localKeyPath, existingKeys);
                System.Diagnostics.Trace.WriteLine($"Saved {existingKeys.Count} keys to {localKeyPath}", "BgiOpener");

                ShowKeyFileWarning(targetDirectory);

                ClearExtractedKeys();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error saving keys: {ex.Message}", "BgiOpener");
            }
        }

        private Dictionary<string, uint> ShowKeyConflictDialog(
            List<(string filename, uint oldKey, uint newKey)> conflicts,
            Dictionary<string, uint> existingKeys)
        {
            try
            {
                if (System.Windows.Application.Current != null)
                {
                    return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dialog = new KeyConflictDialog(conflicts, existingKeys);
                        dialog.Owner = System.Windows.Application.Current.MainWindow;
                        if (dialog.ShowDialog() == true)
                        {
                            return dialog.ResolvedKeys;
                        }
                        return null;
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error showing conflict dialog: {ex.Message}", "BgiOpener");
            }

            // Fallback to overwriting with new keys if dialog fails to show
            System.Diagnostics.Trace.WriteLine("Dialog unavailable, auto-resolving by overwriting", "BgiOpener");
            BgiKeyManager.ResolveConflictsOverwrite(existingKeys, conflicts);
            return existingKeys;
        }

        private void ShowKeyFileWarning (string targetDirectory)
        {
            if (!ShouldShowKeyWarning())
                return;

            try
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        var dialog = new KeyFileWarningDialog();
                        dialog.Owner = System.Windows.Application.Current.MainWindow;
                        dialog.ShowDialog();

                        if (dialog.DontShowAgainChecked)
                        {
                            SaveWarningPreference(false);
                        }
                    });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error showing key file warning: {ex.Message}", "BgiOpener");
            }
        }

        private bool ShouldShowKeyWarning()
        {
            try
            {
                var settings = DefaultScheme;
                return settings?.ShowKeyFileWarning ?? true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error reading BGI scheme: {ex.Message}", "BgiOpener");
            }

            return true;
        }

        private void SaveWarningPreference (bool showWarning)
        {
            try
            {
                var settings = DefaultScheme;

                if (settings != null)
                {
                    settings.ShowKeyFileWarning = showWarning;
                    Scheme = settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error saving BGI scheme: {ex.Message}", "BgiOpener");
            }
        }

        public void ClearExtractedKeys()
        {
            m_extracted_keys.Clear();
        }

        public override void Create(Stream output, IEnumerable<Entry> list, ResourceOptions options,
                                    EntryCallback callback)
        {
            // Reset action for the action to do when a key is missing.
            m_applyMissingKeyAction = null;

            var bgiOptions = GetOptions<BgiOptions>(options);
            var fileList = list.ToList();

            Dictionary<string, uint> keys = null;
            if (bgiOptions.CompressFiles)
            {
                keys = LoadKeysForCompress(fileList, bgiOptions.KeyFilePath);
            }

            fileList = fileList.Where(e => !e.Name.EndsWith("bgi_keys.dat", StringComparison.OrdinalIgnoreCase)).ToList();

            int fileCount = fileList.Count;
            if (callback != null)
                callback(fileCount + 1, null, null);

            if (bgiOptions.ArchiveVersion == 1)
            {
                // PACKFILE header (+ file count)
                using (var writer = new BinaryWriter(output, System.Text.Encoding.ASCII, true))
                {
                    writer.Write(0x6b636150); // "Pack"
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("File    "));
                    writer.Write((uint)fileCount);
                }

                WriteArc(8, 0x10, fileList, keys, output, callback);
            }
            else
            {
                // BURIKO ARC header (+ file count)
                using (var writer = new BinaryWriter(output, System.Text.Encoding.ASCII, true))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("BURIKO ARC20"));
                    writer.Write((uint)fileCount);
                }

                WriteArc(0x18, 0x60, fileList, keys, output, callback);
            }
        }

        /// <summary>
        /// Creation of .arc file
        /// </summary>
        /// <remarks>
        /// <code>
        /// PACKFILE (v1):
        /// Header      : "PACK    " + (File Count)
        /// NameBytes   : 0x10 (16 bytes)
        /// Padding     : (ulong) 0 (8 bytes)
        /// Index Size  : 0x20 per entry (32 bytes)
        /// 
        /// BURIKO ARC (v2):
        /// Header      : "BURIKO ARC20" + (File Count)
        /// NameBytes   : 0x60 (96 bytes)
        /// Padding     : 0x18 (24 bytes)
        /// Index Size  : 0x80 (128 bytes per entry)
        /// </code>
        /// </remarks>
        private void WriteArc (int paddingSize, int nameByteSize, List<Entry> fileList, 
            Dictionary<string, uint> keys, Stream output, EntryCallback callback)
        {
            var index = new List<PackIndexEntry>();
            var dataStream = new MemoryStream();

            int callbackCount = 0;
            foreach (var entry in fileList)
            {
                if (callback != null)
                    callback(callbackCount++, entry, "Adding file");

                var indexEntry = new PackIndexEntry
                {
                    Name = Path.GetFileNameWithoutExtension(entry.Name),
                    Offset = (uint)dataStream.Position
                };

                byte[] fileData = File.ReadAllBytes(entry.Name);

                try
                {
                    var result = TryToCompressFile(
                        fileData,
                        Path.GetFileNameWithoutExtension(entry.Name),
                        keys,
                        dataStream,
                        out uint compressedSize);

                    if (result == PackResult.Skipped)
                        continue;

                    indexEntry.Size = (result == PackResult.Compressed) ? compressedSize : (uint)fileData.Length;

                    if (result == PackResult.PackedUncompressed)
                        dataStream.Write(fileData, 0, fileData.Length);

                    index.Add(indexEntry);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }

            // Write file index, offset and size
            foreach (var entry in index)
            {
                byte[] nameBytes = new byte[nameByteSize];
                var entryNameBytes = System.Text.Encoding.ASCII.GetBytes(entry.Name);

                Array.Copy(entryNameBytes, nameBytes, Math.Min(entryNameBytes.Length, nameByteSize));
                output.Write(nameBytes, 0, nameByteSize);

                using (var writer = new BinaryWriter(output, System.Text.Encoding.ASCII, true))
                {
                    writer.Write(entry.Offset);
                    writer.Write(entry.Size);
                    writer.Write(new byte[paddingSize]);
                }
            }

            // Write all the file contents concatenated
            dataStream.Position = 0;
            dataStream.CopyTo(output);

            if (callback != null)
                callback(callbackCount, null, "Archive created");
        }

        private Dictionary<string, uint> LoadKeysForCompress(List<Entry> fileList, string keyFilePath)
        {
            // If explicit path provided, use it
            if (!string.IsNullOrEmpty(keyFilePath) && File.Exists(keyFilePath))
            {
                System.Diagnostics.Trace.WriteLine($"Loading keys from: {keyFilePath}", "BgiOpener");
                var keys = BgiKeyManager.LoadKeys(keyFilePath);
                System.Diagnostics.Trace.WriteLine($"Loaded {keys.Count} keys", "BgiOpener");
                return keys;
            }

            // Otherwise search in source directory
            System.Diagnostics.Trace.WriteLine("Searching for keys in source directory", "BgiOpener");
            var result = BgiKeyManager.LoadKeysFromSourceDir(fileList);

            if (result.Count == 0)
            {
                System.Diagnostics.Trace.WriteLine("No keys found, showing dialog", "BgiOpener");
                bool packUncompressed = ShowMissingKeyFileDialog();

                if (!packUncompressed)
                {
                    // User pressed "cancel"
                    throw new OperationCanceledException("Archive creation canceled by user");
                }

                // Return to pack all uncompressed
                return new Dictionary<string, uint>();
            }

            System.Diagnostics.Trace.WriteLine($"Loaded {result.Count} keys from source", "BgiOpener");
            return result;
        }

        private bool ShowMissingKeyFileDialog()
        {
            try
            {
                if (System.Windows.Application.Current != null)
                {
                    return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dialog = new MissingKeyFileDialog();
                        dialog.Owner = System.Windows.Application.Current.MainWindow;

                        if (dialog.ShowDialog() == true)
                        {
                            return dialog.PackUncompressed;
                        }

                        return false; // Canceled
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error showing missing key file dialog: {ex.Message}", "BgiOpener");
            }

            return false;
        }

        private PackResult TryToCompressFile(byte[] fileData, string fileName, Dictionary<string, uint> keys,
                                     MemoryStream dataStream, out uint compressedSize)
        {
            compressedSize = 0;

            System.Diagnostics.Trace.WriteLine($"TryToCompressFile: {fileName} ({fileData.Length} bytes)", "BgiOpener");

            if (keys == null || keys.Count == 0)
            {
                return PackResult.PackedUncompressed;
            }

            if (!keys.TryGetValue(fileName, out uint key))
            {
                // Key not found - Check if user already chose an action for all files
                if (m_applyMissingKeyAction.HasValue)
                {
                    System.Diagnostics.Trace.WriteLine($"Applying saved action {m_applyMissingKeyAction.Value} to {fileName}", "BgiOpener");

                    if (m_applyMissingKeyAction.Value == MissingKeyAction.Cancel)
                        throw new OperationCanceledException("Archive creation canceled by user");

                    if (m_applyMissingKeyAction.Value == MissingKeyAction.Skip)
                        return PackResult.Skipped;

                    return PackResult.PackedUncompressed;
                }

                var (action, applyToAll) = ShowMissingKeyDialog(fileName);

                if (applyToAll)
                {
                    m_applyMissingKeyAction = action;
                    System.Diagnostics.Trace.WriteLine($"User chose to apply {action} to all remaining files", "BgiOpener");
                }

                if (action == MissingKeyAction.Cancel)
                    throw new OperationCanceledException("Archive creation canceled by user");

                if (action == MissingKeyAction.Skip)
                    return PackResult.Skipped;

                return PackResult.PackedUncompressed;
            }

            try
            {
                System.Diagnostics.Trace.WriteLine($"Compressing {fileName} with key 0x{key:X8}", "BgiOpener");
                var encoder = new DscEncoder(fileData, key);

                System.Diagnostics.Trace.WriteLine($"Calling Pack() for {fileName}...", "BgiOpener");
                encoder.Pack();
                System.Diagnostics.Trace.WriteLine($"Pack() completed for {fileName}", "BgiOpener");

                byte[] compressedData = encoder.Output;

                dataStream.Write(compressedData, 0, compressedData.Length);
                compressedSize = (uint)compressedData.Length;

                System.Diagnostics.Trace.WriteLine($"Compressed {fileName}: {fileData.Length} -> {compressedData.Length} bytes", "BgiOpener");
                return PackResult.Compressed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Compression failed for {fileName}: {ex.Message}\n{ex.StackTrace}", "BgiOpener");
                return PackResult.PackedUncompressed;
            }
        }

        private (MissingKeyAction action, bool applyToAll) ShowMissingKeyDialog(string fileName)
        {
            try
            {
                if (System.Windows.Application.Current != null)
                {
                    return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dialog = new MissingKeyDialog(fileName);
                        dialog.Owner = System.Windows.Application.Current.MainWindow;

                        if (dialog.ShowDialog() == true)
                        {
                            return (dialog.Action, dialog.ApplyToAllChecked);
                        }

                        return (MissingKeyAction.Cancel, false);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to show missing key dialog: {ex.Message}", "BgiOpener");
            }

            return (MissingKeyAction.Cancel, false);
        }

        private class PackIndexEntry
        {
            public string Name;
            public uint Offset;
            public uint Size;
        }

        private enum PackResult
        {
            Compressed,
            PackedUncompressed,
            Skipped
        }
    }

    [Export(typeof(ArchiveFormat))]
    public class Arc2Opener : ArcOpener
    {
        public override string         Tag { get { return "BURIKO ARC"; } }
        public override string Description { get { return "BGI/Ethornell engine resource archive v2"; } }
        public override uint     Signature { get { return 0x49525542; } } // "BURI"
        public override bool  IsHierarchic { get { return false; } }
        public override bool      CanWrite { get { return false; } }

        public override ArcFile TryOpen (ArcView file)
        {
            if (!file.View.AsciiEqual(4, "KO ARC20"))
                return null;
            int count = file.View.ReadInt32 (12);
            if (!IsSaneCount(count))
                return null;
            uint index_size = 0x80 * (uint)count;
            if (index_size > file.View.Reserve (0x10, index_size))
                return null;
            var dir = new List<Entry> (count);
            long index_offset = 0x10;
            long base_offset = index_offset + index_size;
            for (uint i = 0; i < count; ++i)
            {
                string name = file.View.ReadString(index_offset, 0x60);
                var offset = base_offset + file.View.ReadUInt32(index_offset + 0x60);
                var entry = new PackedEntry { Name = name, Offset = offset };
                entry.Size = file.View.ReadUInt32(index_offset + 0x64);
                if (!entry.CheckPlacement(file.MaxOffset))
                    return null;
                dir.Add(entry);
                index_offset += 0x80;
            }
            foreach (var entry in dir)
            {
                uint signature = file.View.ReadUInt32(entry.Offset);
                var res = AutoEntry.DetectFileType(signature);
                if (res != null)
                    entry.Type = res.Type;
                else if (file.View.AsciiEqual(entry.Offset, "BSE 1."))
                    entry.Type = "image";
                else if (file.View.AsciiEqual(entry.Offset + 4, "bw  "))
                    entry.Type = "audio";
            }
            return new ArcFile(file, this, dir);
        }

        public override Stream OpenEntry(ArcFile arc, Entry entry)
        {
            if (entry.Size < 0x50 || !arc.File.View.AsciiEqual(entry.Offset, "BSE 1."))
                return base.OpenEntry(arc, entry);
            int version = arc.File.View.ReadUInt16(entry.Offset + 8);
            if (version != 0x100 && version != 0x101)
                return base.OpenEntry(arc, entry);

            ushort checksum = arc.File.View.ReadUInt16(entry.Offset + 0xA);
            uint key = arc.File.View.ReadUInt32(entry.Offset + 0xC);
            var header = arc.File.View.ReadBytes(entry.Offset + 0x10, 0x40);
            if (0x101 == version)
                DecryptBse(header, new BseGenerator101(key));
            else
                DecryptBse(header, new BseGenerator100(key));
            var body = arc.File.CreateStream(entry.Offset + 0x50, entry.Size - 0x50);
            return new PrefixStream(header, body);
        }

        void DecryptBse(byte[] data, IBseGenerator decoder)
        {
            var decoded = new bool[0x40];
            for (int i = 0; i < decoded.Length; ++i)
            {
                int dst = decoder.NextKey() & 0x3F;
                while (decoded[dst])
                {
                    dst = (dst + 1) & 0x3F;
                }
                int shift = decoder.NextKey() & 7;
                bool right_shift = (decoder.NextKey() & 1) == 0;
                byte symbol = (byte)(data[dst] - decoder.NextKey());
                if (right_shift)
                {
                    data[dst] = Binary.RotByteR(symbol, shift);
                }
                else
                {
                    data[dst] = Binary.RotByteL(symbol, shift);
                }
                decoded[dst] = true;
            }
        }
    }

    internal class BgiDecoderBase : MsbBitStream
    {
        protected uint m_key;
        protected uint m_magic;

        protected BgiDecoderBase(Stream input, bool leave_open = false) : base(input, leave_open)
        {
        }

        protected byte UpdateKey()
        {
            uint v0 = 20021 * (m_key & 0xffff);
            uint v1 = m_magic | (m_key >> 16);
            v1 = v1 * 20021 + m_key * 346;
            v1 = (v1 + (v0 >> 16)) & 0xffff;
            m_key = (v1 << 16) + (v0 & 0xffff) + 1;
            return (byte)v1;
        }
    }

    internal sealed class DscDecoder : BgiDecoderBase
    {
        byte[] m_output;
        uint m_dec_count;

        public byte[] Output { get { return m_output; } }
        public uint Key { get { return m_key; } }

        public DscDecoder(IBinaryStream input) : base(input.AsStream)
        {
            m_magic = (uint)input.ReadUInt16() << 16;
            input.Position = 0x10;
            m_key = input.ReadUInt32();
            int output_size = input.ReadInt32();
            m_dec_count = input.ReadUInt32();
            m_output = new byte[output_size];
        }

        public void Unpack()
        {
            Input.Position = 0x20;
            HuffmanCode[] hcodes = new HuffmanCode[512];
            HuffmanNode[] hnodes = new HuffmanNode[1023];

            int leaf_node_count = 0;
            for (ushort i = 0; i < 512; i++)
            {
                int src = Input.ReadByte();
                if (-1 == src)
                    throw new EndOfStreamException("Incomplete compressed stream");
                byte depth = (byte)(src - UpdateKey());
                if (0 != depth)
                {
                    hcodes[leaf_node_count].Depth = depth;
                    hcodes[leaf_node_count].Code = i;
                    leaf_node_count++;
                }
            }
            Array.Sort(hcodes, 0, leaf_node_count);
            CreateHuffmanTree(hnodes, hcodes, leaf_node_count);
            HuffmanDecompress(hnodes, m_dec_count);
        }

        struct HuffmanCode : IComparable<HuffmanCode>
        {
            public ushort Code;
            public ushort Depth;

            public int CompareTo(HuffmanCode other)
            {
                int cmp = (int)Depth - (int)other.Depth;
                if (0 == cmp)
                    cmp = (int)Code - (int)other.Code;
                return cmp;
            }
        }

        class HuffmanNode
        {
            public bool IsParent;
            public int Code;
            public int LeftChildIndex;
            public int RightChildIndex;
        }

        static void CreateHuffmanTree(HuffmanNode[] hnodes, HuffmanCode[] hcode, int node_count)
        {
            var nodes_index = new int[2, 512];
            int next_node_index = 1;
            int depth_nodes = 1;
            int depth = 0;
            int child_index = 0;
            nodes_index[0, 0] = 0;
            for (int n = 0; n < node_count;)
            {
                int huffman_nodes_index = child_index;
                child_index ^= 1;

                int depth_existed_nodes = 0;
                while (n < hcode.Length && hcode[n].Depth == depth)
                {
                    var node = new HuffmanNode { IsParent = false, Code = hcode[n++].Code };
                    hnodes[nodes_index[huffman_nodes_index, depth_existed_nodes]] = node;
                    depth_existed_nodes++;
                }
                int depth_nodes_to_create = depth_nodes - depth_existed_nodes;
                for (int i = 0; i < depth_nodes_to_create; i++)
                {
                    var node = new HuffmanNode { IsParent = true };
                    nodes_index[child_index, i * 2] = node.LeftChildIndex = next_node_index++;
                    nodes_index[child_index, i * 2 + 1] = node.RightChildIndex = next_node_index++;
                    hnodes[nodes_index[huffman_nodes_index, depth_existed_nodes + i]] = node;
                }
                depth++;
                depth_nodes = depth_nodes_to_create * 2;
            }
        }

        int HuffmanDecompress(HuffmanNode[] hnodes, uint dec_count)
        {
            int dst_ptr = 0;

            for (uint k = 0; k < dec_count; k++)
            {
                int node_index = 0;
                do
                {
                    int bit = GetNextBit();
                    if (-1 == bit)
                        throw new EndOfStreamException();
                    if (0 == bit)
                        node_index = hnodes[node_index].LeftChildIndex;
                    else
                        node_index = hnodes[node_index].RightChildIndex;
                }
                while (hnodes[node_index].IsParent);

                int code = hnodes[node_index].Code;
                if (code >= 256)
                {
                    int offset = GetBits(12);
                    if (-1 == offset)
                        break;
                    int count = (code & 0xff) + 2;
                    offset += 2;
                    Binary.CopyOverlapped(m_output, dst_ptr - offset, dst_ptr, count);
                    dst_ptr += count;
                }
                else
                    m_output[dst_ptr++] = (byte)code;
            }
            return dst_ptr;
        }
    }

    internal sealed class DscEncoder : BgiDecoderBase
    {
        private new byte[] m_input;
        private List<byte> m_output;
        private new uint m_key;
        private new uint m_magic = 0x53440000; // "DS" << 16

        public byte[] Output { get { return m_output.ToArray(); } }

        public DscEncoder(byte[] input, uint key) : base(new MemoryStream(), false)
        {
            m_input = input;
            m_key = key;
            m_output = new List<byte>();
        }

        public void Pack()
        {
            // Header
            m_output.AddRange(System.Text.Encoding.ASCII.GetBytes("DSC FORMAT 1.00\0"));
            m_output.AddRange(BitConverter.GetBytes(m_key));
            m_output.AddRange(BitConverter.GetBytes(m_input.Length));

            var symbols = LZ77Compress(m_input);
            m_output.AddRange(BitConverter.GetBytes((uint)symbols.Count));
            m_output.AddRange(new byte[4]); // padding

            var depths = BuildHuffmanTree(symbols);

            // Encrypted Huffman
            uint tempKey = m_key;
            for (int i = 0; i < 512; i++)
            {
                byte encByte = (byte)(depths[i] + UpdateKeyStatic(ref tempKey, m_magic) & 0xFF);
                m_output.Add(encByte);
            }

            var codes = AssignCanonicalCodes(depths);

            WriteCompressedData(symbols, codes);
        }

        private List<LZ77Symbol> LZ77Compress(byte[] data)
        {
            var symbols = new List<LZ77Symbol>();
            int pos = 0;

            while (pos < data.Length)
            {
                int bestLength = 0;
                int bestOffset = 0;

                int searchStart = Math.Max(0, pos - 4095);

                for (int offset = pos - searchStart; offset >= 2; offset--)
                {
                    int matchPos = pos - offset;
                    int length = 0;

                    while (length < 257 && pos + length < data.Length &&
                           data[matchPos + length] == data[pos + length])
                    {
                        length++;
                    }

                    if (length > bestLength)
                    {
                        bestLength = length;
                        bestOffset = offset;
                    }
                }

                if (bestLength >= 3)
                {
                    symbols.Add(new LZ77Symbol { Code = (ushort)(256 + bestLength - 2), Offset = bestOffset });
                    pos += bestLength;
                }
                else
                {
                    symbols.Add(new LZ77Symbol { Code = data[pos], Offset = -1 });
                    pos++;
                }
            }

            return symbols;
        }

        private ushort[] BuildHuffmanTree(List<LZ77Symbol> symbols)
        {
            var freq = new Dictionary<ushort, int>();
            foreach (var sym in symbols)
            {
                if (!freq.ContainsKey(sym.Code))
                    freq[sym.Code] = 0;

                freq[sym.Code]++;
            }

            var heap = new SortedSet<HuffmanTreeNode>(Comparer<HuffmanTreeNode>.Create((a, b) =>
            {
                int cmp = a.Frequency.CompareTo(b.Frequency);
                if (cmp == 0) cmp = a.Id.CompareTo(b.Id);
                return cmp;
            }));

            int counter = 0;
            foreach (var kvp in freq)
            {
                heap.Add(new HuffmanTreeNode
                {
                    Frequency = kvp.Value,
                    Id = counter++,
                    Symbol = kvp.Key,
                    Left = null,
                    Right = null
                });
            }

            while (heap.Count > 1)
            {
                var left = heap.Min;
                heap.Remove(left);
                var right = heap.Min;
                heap.Remove(right);

                var parent = new HuffmanTreeNode
                {
                    Frequency = left.Frequency + right.Frequency,
                    Id = counter++,
                    Symbol = null,
                    Left = left,
                    Right = right
                };
                heap.Add(parent);
            }

            var depths = new ushort[512];
            if (heap.Count > 0)
                CalculateDepths(heap.Min, depths, 0);

            foreach (var kvp in freq)
            {
                if (depths[kvp.Key] == 0)
                    depths[kvp.Key] = 1;
                else if (depths[kvp.Key] > 255)
                    depths[kvp.Key] = 255;
            }

            return depths;
        }
        private void CalculateDepths(HuffmanTreeNode node, ushort[] depths, int depth)
        {
            if (node.Symbol.HasValue)
            {
                depths[node.Symbol.Value] = (ushort)(depth > 0 ? depth : 1);
            }
            else
            {
                if (node.Left != null)
                    CalculateDepths(node.Left, depths, depth + 1);
                if (node.Right != null)
                    CalculateDepths(node.Right, depths, depth + 1);
            }
        }

        private Dictionary<ushort, CodeInfo> AssignCanonicalCodes(ushort[] depths)
        {
            var symbolDepths = new List<(ushort symbol, ushort depth)>();
            for (int i = 0; i < 512; i++)
            {
                if (depths[i] > 0)
                    symbolDepths.Add(((ushort)i, depths[i]));
            }

            symbolDepths.Sort((a, b) =>
            {
                int cmp = a.depth.CompareTo(b.depth);
                if (cmp == 0) cmp = a.symbol.CompareTo(b.symbol);
                return cmp;
            });

            var codes = new Dictionary<ushort, CodeInfo>();
            int code = 0;
            int prevDepth = 0;

            foreach (var (symbol, depth) in symbolDepths)
            {
                if (depth > prevDepth)
                {
                    code <<= depth - prevDepth;
                    prevDepth = depth;
                }

                codes[symbol] = new CodeInfo { Code = code, Length = depth };
                code++;
            }

            return codes;
        }

        private void WriteCompressedData(List<LZ77Symbol> symbols, Dictionary<ushort, CodeInfo> codes)
        {
            int bitBuffer = 0;
            int bitsInBuffer = 0;

            foreach (var symbol in symbols)
            {
                var codeInfo = codes[symbol.Code];

                // Write Huffman
                for (int i = 0; i < codeInfo.Length; i++)
                {
                    int bit = (codeInfo.Code >> (codeInfo.Length - 1 - i)) & 1;
                    bitBuffer = (bitBuffer << 1) | bit;
                    bitsInBuffer++;

                    if (bitsInBuffer == 8)
                    {
                        m_output.Add((byte)bitBuffer);
                        bitBuffer = 0;
                        bitsInBuffer = 0;
                    }
                }

                // Backreference offset (if needed)
                if (symbol.Code >= 256 && symbol.Offset >= 0)
                {
                    int offsetBits = symbol.Offset - 2;
                    for (int i = 0; i < 12; i++)
                    {
                        int bit = (offsetBits >> (11 - i)) & 1;
                        bitBuffer = (bitBuffer << 1) | bit;
                        bitsInBuffer++;

                        if (bitsInBuffer == 8)
                        {
                            m_output.Add((byte)bitBuffer);
                            bitBuffer = 0;
                            bitsInBuffer = 0;
                        }
                    }
                }
            }

            // Flush remaining bits
            if (bitsInBuffer > 0)
            {
                bitBuffer <<= 8 - bitsInBuffer;
                m_output.Add((byte)bitBuffer);
            }
        }

        private static byte UpdateKeyStatic(ref uint key, uint magic)
        {
            uint v0 = 20021 * (key & 0xffff);
            uint v1 = magic | (key >> 16);
            v1 = v1 * 20021 + key * 346;
            v1 = (v1 + (v0 >> 16)) & 0xffff;
            key = (v1 << 16) + (v0 & 0xffff) + 1;
            return (byte)v1;
        }

        struct LZ77Symbol
        {
            public ushort Code;
            public int Offset;
        }

        struct CodeInfo
        {
            public int Code;
            public int Length;
        }

        class HuffmanTreeNode
        {
            public int Frequency;
            public int Id;
            public ushort? Symbol;
            public HuffmanTreeNode Left;
            public HuffmanTreeNode Right;
        }
    }

    internal interface IBseGenerator
    {
        int NextKey();
    }

    internal class BseGenerator100 : IBseGenerator
    {
        int m_key;

        public BseGenerator100(uint key)
        {
            m_key = (int)key;
        }

        public int NextKey()
        {
            uint v = (uint)(((m_key * 257 >> 8) + m_key * 97 + 23) ^ 0xA6CD9B75);
            m_key = (int)Binary.RotR(v, 16);
            return m_key;
        }
    }

    internal class BseGenerator101 : IBseGenerator
    {
        int m_key;

        public BseGenerator101(uint key)
        {
            m_key = (int)key;
        }

        public int NextKey()
        {
            uint v = (uint)((m_key * 127 >> 7) + m_key * 83 + 53) ^ 0xB97A7E5C;
            m_key = (int)Binary.RotR(v, 16);
            return m_key;
        }
    }

    internal class BgiKeyManager
    {
        private const string LocalKeyFileName = "bgi_keys.dat";

        public static Dictionary<string, uint> LoadKeys(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return new Dictionary<string, uint>();

                string json = File.ReadAllText(filePath);
                var stringDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                var result = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
                if (stringDict != null)
                {
                    foreach (var kvp in stringDict)
                    {
                        if (uint.TryParse(kvp.Value.Replace("0x", "").Replace("0X", ""),
                                        System.Globalization.NumberStyles.HexNumber,
                                        null, out uint key))
                        {
                            result[kvp.Key] = key;
                        }
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error loading keys from {filePath}: {ex.Message}", "BgiKeyManager");
                return new Dictionary<string, uint>();
            }
        }

        public static Dictionary<string, uint> LoadKeysFromSourceDir(IEnumerable<Entry> entries)
        {
            var firstFile = entries.FirstOrDefault();
            if (firstFile == null)
                return new Dictionary<string, uint>();

            string directory = Path.GetDirectoryName(Path.GetFullPath(firstFile.Name));
            string keyPath = GetLocalKeyPath(directory);

            if (File.Exists(keyPath))
            {
                System.Diagnostics.Trace.WriteLine($"Loading keys from: {keyPath}", "BgiKeyManager");
                return LoadKeys(keyPath);
            }

            System.Diagnostics.Trace.WriteLine($"No key file found at: {keyPath}", "BgiKeyManager");
            return new Dictionary<string, uint>();
        }

        public static void SaveKeys(string filePath, Dictionary<string, uint> keys)
        {
            try
            {
                var stringDict = keys.ToDictionary(
                    kvp => kvp.Key,
                    kvp => $"0x{kvp.Value:X8}"
                );

                string json = JsonConvert.SerializeObject(stringDict, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error saving keys to {filePath}: {ex.Message}", "BgiKeyManager");
            }
        }

        public static List<(string filename, uint oldKey, uint newKey)> MergeKeys(
            Dictionary<string, uint> existingKeys,
            Dictionary<string, uint> newKeys)
        {
            var conflicts = new List<(string, uint, uint)>();

            foreach (var kvp in newKeys)
            {
                if (existingKeys.TryGetValue(kvp.Key, out uint existingKey))
                {
                    if (existingKey != kvp.Value)
                    {
                        conflicts.Add((kvp.Key, existingKey, kvp.Value));
                    }
                    // Same key, no conflict
                }
                else
                {
                    // New file, add to existing dictionary
                    existingKeys[kvp.Key] = kvp.Value;
                }
            }

            return conflicts;
        }

        public static void ResolveConflictsOverwrite(
            Dictionary<string, uint> existingKeys,
            List<(string filename, uint oldKey, uint newKey)> conflicts)
        {
            foreach (var conflict in conflicts)
            {
                existingKeys[conflict.filename] = conflict.newKey;
            }
        }

        public static string GetLocalKeyPath(string extractionDirectory)
        {
            return Path.Combine(extractionDirectory, LocalKeyFileName);
        }

        public static uint? GetKeyForFile(string filename, string directory)
        {
            string localPath = GetLocalKeyPath(directory);
            var localKeys = LoadKeys(localPath);

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filename);

            if (localKeys.TryGetValue(fileNameWithoutExt, out uint key))
                return key;

            return null;
        }
    }

    [Serializable]
    public class BgiScheme : ResourceScheme
    {
        public Dictionary<string, BgiArchiveSettings> knownArchives;
        public bool ShowKeyFileWarning = true;
    }

    [Serializable]
    public class BgiArchiveSettings
    {
        public int Version;
        public bool ShowKeyWarning;
    }

    internal class BgiOptions : ResourceOptions
    {
        public int ArchiveVersion { get; set; } // 1 = PackFile, 2 = BURIKO ARC
        public bool CompressFiles { get; set; }
        public string KeyFilePath { get; set; } // When user selects bgi_keys.dat themselves
    }

    [Export(typeof(ResourceAlias))]
    [ExportMetadata("Extension", "_BP")]
    [ExportMetadata("Target", "SCR")]
    public class BpFormat : ResourceAlias { }
}
