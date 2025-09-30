using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

using GameRes.Utility;

namespace GameRes.Formats.NekoNyan
{
    public class SpriteArcEntry : PackedEntry
    {
        public SpriteDecryptParams DecryptParams { get; set; }
        public uint Key                          { get; set; }
    }

    [Serializable]
    public class SpriteScheme : ResourceScheme
    {
        public Dictionary<string, SpriteDecryptParams> KnownGame = new Dictionary<string, SpriteDecryptParams>();
    }

    [Serializable]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public class SpriteDecryptParams
    {
        public long FileCountBeginByte;
        public uint GenKeyInitMul;
        public uint GenKeyInitAdd;
        public int GenKeyInitShift;
        public uint GenKeyRoundAdd;
        public uint GenKeyRoundAnd;
        public int GenKeyRoundShift;

        public long DecryMod1;
        public byte DecryAdd;
        public long DecryMod2;
        public byte DecryXor;
    }

    [Export(typeof(ArchiveFormat))]
    public class SpriteArcDat : ArchiveFormat
    {
        private SpriteScheme m_scheme = new SpriteScheme();
        
        public override string Tag         { get; } = "DAT/NEKONYAN/SPRITE";
        public override string Description { get; } = "NEKONYAN/SPRITE resource archive";
        public override uint Signature     { get; } = 0x00000000;
        public override bool IsHierarchic  { get; } = true;

        public override ResourceScheme Scheme
        {
            get => m_scheme;
            set => m_scheme = (SpriteScheme)value;
        }

        public override ArcFile TryOpen(ArcView view)
        {
            const int header_size = 1024;
            if (view.MaxOffset < header_size)
                return null;  // file too small

            if (!TryIdentifyGame(view, out var game))
                return null;  // not a known game
            
            int file_count = 0;
            for (long i = game.FileCountBeginByte; i < header_size - 4; i += 4)
                file_count += view.View.ReadInt32(i);

            if (file_count == 0)
                return new ArcFile(view, this, Array.Empty<Entry>());
            
            var entries = new List<Entry>();
            uint seed1 = view.View.ReadUInt32(0xD4);
            uint seed2 = view.View.ReadUInt32(0x5C);

            // table of contents is encrypted, need to decrypt it first
            int toc_size = 16 * file_count;
            if (toc_size > view.MaxOffset - header_size)
                return null;  // file too small
            
            using (var toc_buffer = ArrayPool<byte>.Shared.RentSafe(toc_size))
            {
                if (view.View.Read(header_size, toc_buffer, 0, (uint)toc_size) != toc_size)
                    return null;  // file too small
                
                SpriteDecryptionUtils.Decrypt(new Span<byte>(toc_buffer, 0, toc_size), seed1, game);

                int content_offset = BitConverter.ToInt32(toc_buffer, 12);
                int const_size = content_offset - (header_size + toc_size);

                if (content_offset > view.MaxOffset)
                    return null;  // file too small
                
                using (var const_buffer = ArrayPool<byte>.Shared.RentSafe(const_size))
                {
                    if (view.View.Read(header_size + toc_size, const_buffer, 0, (uint)const_size) != const_size)
                        return null;  // file too small
                    
                    SpriteDecryptionUtils.Decrypt(new Span<byte>(const_buffer, 0, const_size), seed2, game);

                    for (int i = 0; i < file_count; i++)
                    {
                        int entry_offset = 16 * i;
                        uint size = BitConverter.ToUInt32(toc_buffer, entry_offset);
                        int const_addr = BitConverter.ToInt32(toc_buffer, entry_offset + 4);
                        uint key = BitConverter.ToUInt32(toc_buffer, entry_offset + 8);
                        uint data_addr = BitConverter.ToUInt32(toc_buffer, entry_offset + 12);

                        int cnt = 0;
                        for (; const_addr + cnt < const_size && const_buffer[const_addr + cnt] != 0; cnt++) { }

                        string name = Encoding.ASCII.GetString(const_buffer, const_addr, cnt);
                        entries.Add(new SpriteArcEntry
                        {
                            DecryptParams = game,
                            Name = name,
                            Offset = data_addr,
                            Size = size,
                            Key = key,
                            Type = FormatCatalog.Instance.GetTypeFromName(name)
                        });
                    }
                }
            }

            return new ArcFile(view, this, entries);
        }

        public override Stream OpenEntry(ArcFile arc, Entry entry)
        {
            if (!(entry is SpriteArcEntry sprite_entry))
                return arc.File.CreateStream (entry.Offset, entry.Size);
            
            return new SpriteDecryptionStream(arc.File.CreateStream(entry.Offset, entry.Size), sprite_entry.Key, sprite_entry.DecryptParams);
        }

        private bool TryIdentifyGame(ArcView view, out SpriteDecryptParams decrypt_params)
        {
            decrypt_params = null;
            string info_path = VFS.CombinePath(VFS.GetDirectoryName(view.Name), "app.info");
            if (!File.Exists(info_path))
                return false;

            using (var sr = new StreamReader(info_path, Encoding.UTF8))
            {
                string company = sr.ReadLine();
                string product = sr.ReadLine();

                if (string.IsNullOrEmpty(company) || string.IsNullOrEmpty(product))
                    return false;

                return m_scheme.KnownGame.TryGetValue($"{company}/{product}", out decrypt_params);
            }
        }
    }
    
    public static class SpriteDecryptionUtils
    {
        public const int KeyTableSize = 256;
        
        public static void Decrypt(Span<byte> data, uint key, SpriteDecryptParams decry_params, long base_index = 0)
        {
            Span<byte> key_table = stackalloc byte[KeyTableSize];
            GenerateKeyTable(key_table, key, decry_params);
            Decrypt(data, key_table, decry_params, base_index);
        }

        public static void Decrypt(Span<byte> data, Span<byte> key_table, SpriteDecryptParams decry_params, long base_index = 0)
        {
            if (key_table.Length != KeyTableSize)
                ThrowInvalidKeyTable();
            
            for (int i = 0; i < data.Length; i++)
            {
                long key_index = base_index + i;
                byte current_byte = data[i];
                current_byte ^= key_table[(int)(key_index % decry_params.DecryMod1)];
                current_byte += decry_params.DecryAdd;
                current_byte += key_table[(int)(key_index % decry_params.DecryMod2)];
                current_byte ^= decry_params.DecryXor;
                data[i] = current_byte;
            }
        }
        
        public static void GenerateKeyTable(Span<byte> key_table, uint seed, in SpriteDecryptParams decry_params)
        {
            if (key_table.Length != KeyTableSize)
                ThrowInvalidKeyTable();
            
            uint state1 = seed * decry_params.GenKeyInitMul + decry_params.GenKeyInitAdd;
            uint state2 = (state1 << decry_params.GenKeyInitShift) ^ state1;
            for (int i = 0; i < KeyTableSize; i++)
            {
                state1 -= seed;
                state1 += state2;
                state2 = state1 + decry_params.GenKeyRoundAdd;
                state1 *= state2 & decry_params.GenKeyRoundAnd;
                key_table[i] = (byte)state1;
                state1 >>= decry_params.GenKeyRoundShift;
            }
        }
        
        private static void ThrowInvalidKeyTable()
        {
            throw new ArgumentException($"Key table must be exactly {KeyTableSize} bytes long.");
        }
    }

    public class SpriteDecryptionStream : Stream
    {
        private readonly Stream m_base_stream;
        private readonly byte[] m_key_table;
        private readonly SpriteDecryptParams m_decry_params;
        
        public SpriteDecryptionStream(Stream encrypted_source, uint decryption_key, SpriteDecryptParams decry_params)
        {
            m_base_stream = encrypted_source;
            m_decry_params = decry_params;
            
            m_key_table = new byte[SpriteDecryptionUtils.KeyTableSize];
            SpriteDecryptionUtils.GenerateKeyTable(m_key_table, decryption_key, decry_params);
        }

        public override void Flush()
        {
            m_base_stream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return m_base_stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            m_base_stream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long init_position = m_base_stream.Position;
            int bytes_read = m_base_stream.Read(buffer, offset, count);

            if (bytes_read == 0)
                return 0;
            
            var span = new Span<byte>(buffer, offset, bytes_read);
            SpriteDecryptionUtils.Decrypt(span, m_key_table, m_decry_params, (int)init_position);

            return bytes_read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException("SpriteDecryptionStream does not support writing yet.");
        }

        public override bool CanRead => m_base_stream.CanRead;
        public override bool CanSeek => m_base_stream.CanSeek;
        public override bool CanWrite => false;  // Unimplemented for writing

        public override long Length => m_base_stream.Length;

        public override long Position
        {
            get => m_base_stream.Position;
            set => m_base_stream.Position = value;
        }
    }
}