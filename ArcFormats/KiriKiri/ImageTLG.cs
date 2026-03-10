//! \file       ImageTLG.cs
//! \date       Thu Jul 17 21:31:39 2014
//! \brief      KiriKiri TLG image implementation.
//---------------------------------------------------------------------------
// TLG5/6 decoder
//  Copyright (C) 2000-2005  W.Dee <dee@kikyou.info> and contributors
//
// C# port by morkt
//

using System;
using System.IO;
using System.ComponentModel.Composition;
using System.Windows.Media;
using GameRes.Utility;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace GameRes.Formats.KiriKiri
{
    internal class TlgMetaData : ImageMetaData
    {
        public int Version;
        public int DataOffset;

        // TLGref / TLGqoi additions
        public bool IsTlgRef;
        public bool IsTlgQoi;
        public uint RefId;
        public int ImageIndex;
        public int ImageCount;
        public string HiddenName;
        public int BandHeight;
        public int BandCount;
        public ulong Q0;
        public ulong Q1;
        public ulong Q2;
        public ulong Q3;
        public int PayloadOffset;
    }

    [Export(typeof(ImageFormat))]
    public class TlgFormat : ImageFormat
    {
        public override string Tag { get { return "TLG"; } }
        public override string Description { get { return "KiriKiri game engine image format"; } }
        public override uint Signature { get { return 0x30474c54; } } // "TLG0"

        public TlgFormat ()
        {
            Extensions = new string[] { "tlg", "tlg5", "tlg6" };
            Signatures = new uint[] {
                0x30474C54, 0x35474C54, 0x36474C54, 0x35474CAB, 0x584D4B4A,
                0x71474C54, // "TLGq"
                0x72474C54, // "TLGr"
            };
        }

        public override ImageMetaData ReadMetaData (IBinaryStream stream)
        {
            var header = stream.ReadHeader (0x54);
            if (header.Length < 0x1C)
                return null;

            if (header.AsciiEqual ("TLGref\x00raw\x1a"))
                return ReadTlgRefMetaData (header);
            if (header.AsciiEqual ("TLGqoi\x00raw\x1a"))
                return ReadTlgQoiMetaData (header);

            int offset = 0x0F;
            if (!header.AsciiEqual ("TLG0.0\x00sds\x1a"))
                offset = 0;
            int version;
            if (!header.AsciiEqual (offset+6, "\x00raw\x1a"))
                return null;
            if (0xAB == header[offset])
                header[offset] = (byte)'T';
            if (header.AsciiEqual (offset, "TLG6.0"))
                version = 6;
            else if (header.AsciiEqual (offset, "TLG5.0"))
                version = 5;
            else if (header.AsciiEqual (offset, "XXXYYY"))
            {
                version = 5;
                header[offset+0x0C] ^= 0xAB;
                header[offset+0x10] ^= 0xAC;
            }
            else if (header.AsciiEqual (offset, "XXXZZZ"))
            {
                version = 6;
                header[offset+0x0F] ^= 0xAB;
                header[offset+0x13] ^= 0xAC;
            }
            else if (header.AsciiEqual (offset, "JKMXE8"))
            {
                version = 5;
                header[offset+0x0C] ^= 0x1A;
                header[offset+0x10] ^= 0x1C;
            }
            else
                return null;
            int colors = header[offset+11];
            if (6 == version)
            {
                if (1 != colors && 4 != colors && 3 != colors)
                    return null;
                if (header[offset+12] != 0 || header[offset+13] != 0 || header[offset+14] != 0)
                    return null;
                offset += 15;
            }
            else
            {
                if (4 != colors && 3 != colors)
                    return null;
                offset += 12;
            }
            return new TlgMetaData
            {
                Width   = header.ToUInt32 (offset),
                Height  = header.ToUInt32 (offset+4),
                BPP     = colors*8,
                Version     = version,
                DataOffset  = offset+8,
            };
        }

        TlgMetaData ReadTlgRefMetaData (CowArray<byte> header)
        {
            if (header.Length < 0x2C)
                return null;
            if (!header.AsciiEqual (20, "QREF"))
                return null;

            int chunk_size = LittleEndian.ToInt32 (header, 24);
            if (chunk_size < 16)
                return null;

            int name_bytes = LittleEndian.ToInt32 (header, 40);
            if (name_bytes < 0)
                return null;

            string hidden_name = string.Empty;
            if (name_bytes != 0 && header.Length >= 44 + name_bytes)
            {
                var name_buf = new byte[name_bytes];
                for (int i = 0; i < name_bytes; ++i)
                    name_buf[i] = header[44 + i];
                hidden_name = Encoding.Unicode.GetString(name_buf);
            }

            return new TlgMetaData
            {
                Width       = LittleEndian.ToUInt32 (header, 12),
                Height      = LittleEndian.ToUInt32 (header, 16),
                BPP         = 32,
                Version     = 0,
                DataOffset  = 0,
                IsTlgRef    = true,
                RefId       = LittleEndian.ToUInt32 (header, 28),
                ImageIndex  = LittleEndian.ToInt32 (header, 32),
                ImageCount  = LittleEndian.ToInt32 (header, 36),
                HiddenName  = hidden_name,
            };
        }

        TlgMetaData ReadTlgQoiMetaData (CowArray<byte> header)
        {
            if (header.Length < 0x54)
                return null;
            if (!header.AsciiEqual (20, "QHDR"))
                return null;

            int chunk_size = LittleEndian.ToInt32 (header, 24);
            if (chunk_size != 0x30)
                return null;

            return new TlgMetaData
            {
                Width       = LittleEndian.ToUInt32 (header, 12),
                Height      = LittleEndian.ToUInt32 (header, 16),
                BPP         = 32,
                Version     = 0,
                DataOffset  = 0,
                IsTlgQoi    = true,
                RefId       = LittleEndian.ToUInt32 (header, 28),
                BandHeight  = LittleEndian.ToInt32 (header, 36),
                BandCount   = LittleEndian.ToInt32 (header, 40),
                Q0          = LittleEndian.ToUInt64 (header, 44),
                Q1          = LittleEndian.ToUInt64 (header, 52),
                Q2          = LittleEndian.ToUInt64 (header, 60),
                Q3          = LittleEndian.ToUInt64 (header, 68),
                PayloadOffset = 0x54,
                ImageIndex  = 0,
                ImageCount  = 1,
            };
        }

        public override ImageData Read (IBinaryStream file, ImageMetaData info)
        {
            var meta = (TlgMetaData)info;

            if (meta.IsTlgRef)
                return ReadTlgRef (file, meta);
            if (meta.IsTlgQoi)
                throw new InvalidFormatException ("TLGqoi is a hidden backing image and cannot be opened directly.");
                // return ReadTlgQoiImage (file, meta, meta.ImageIndex, meta.ImageCount);

            var image = ReadTlg (file, meta);

            int tail_size = (int)Math.Min (file.Length - file.Position, 512);
            if (tail_size > 8)
            {
                var tail = file.ReadBytes (tail_size);
                try
                {
                    var blended_image = ApplyTags (image, meta, tail);
                    if (null != blended_image)
                        return blended_image;
                }
                catch (FileNotFoundException X)
                {
                    Trace.WriteLine (string.Format ("{0}: {1}", X.Message, X.FileName), "[TlgFormat.Read]");
                }
                catch (Exception X)
                {
                    Trace.WriteLine (X.Message, "[TlgFormat.Read]");
                }
            }
            PixelFormat format = 32 == meta.BPP ? PixelFormats.Bgra32 : PixelFormats.Bgr32;
            return ImageData.Create (meta, format, null, image, (int)meta.Width * 4);
        }

        ImageData ReadTlgRef (IBinaryStream file, TlgMetaData meta)
        {
            if (string.IsNullOrEmpty (meta.HiddenName))
                throw new InvalidFormatException ("TLGref hidden file name is empty");

            string hidden_name = meta.HiddenName;
            string dir = "/" + VFS.GetDirectoryName (meta.FileName);
            string candidate = string.IsNullOrEmpty (dir) ? hidden_name : VFS.CombinePath (dir, hidden_name);

            Entry hidden_entry = null;

            if (VFS.FileExists (candidate))
                hidden_entry = VFS.FindFile (candidate);
            else if (VFS.FileExists (hidden_name))
                hidden_entry = VFS.FindFile (hidden_name);
            else
                throw new FileNotFoundException ("Unable to locate referenced TLGqoi file.", candidate);

            using (var hidden = VFS.OpenBinaryStream (hidden_entry))
            {
                var hidden_info = ReadMetaData (hidden) as TlgMetaData;
                if (null == hidden_info || !hidden_info.IsTlgQoi)
                    throw new InvalidFormatException ("Referenced file is not TLGqoi");

                hidden_info.FileName = hidden_entry.Name;
                if (hidden_info.RefId != meta.RefId)
                    throw new InvalidFormatException ("TLGref/TLGqoi ref_id mismatch");

                var pixels = ReadTlgQoi (hidden, hidden_info, meta.ImageIndex, meta.ImageCount);

                var image_meta = new TlgMetaData
                {
                    Width    = meta.Width != 0 ? meta.Width : hidden_info.Width,
                    Height   = meta.Height != 0 ? meta.Height : hidden_info.Height,
                    BPP      = 32,
                    FileName = meta.FileName,
                };
                return ImageData.Create (image_meta, PixelFormats.Bgra32, null, pixels, (int)image_meta.Width * 4);
            }
        }
        ImageData ReadTlgQoiImage (IBinaryStream file, TlgMetaData meta, int image_index, int image_count)
        {
            var pixels = ReadTlgQoi (file, meta, image_index, image_count);
            var image_meta = new TlgMetaData
            {
                Width    = meta.Width,
                Height   = meta.Height,
                BPP      = 32,
                FileName = meta.FileName,
            };
            return ImageData.Create (image_meta, PixelFormats.Bgra32, null, pixels, (int)meta.Width * 4);
        }

        public override void Write (Stream file, ImageData image)
        {
            throw new NotImplementedException ("TlgFormat.Write not implemented");
        }

        byte[] ReadTlg (IBinaryStream src, TlgMetaData info)
        {
            src.Position = info.DataOffset;
            if (6 == info.Version)
                return ReadV6 (src, info);
            else
                return ReadV5 (src, info);
        }

        byte[] ReadTlgQoi (IBinaryStream src, TlgMetaData info, int image_index, int image_count)
        {
            src.Position = 0;
            var data = src.ReadBytes ((int)src.Length);
            if (null == data || data.Length < info.PayloadOffset)
                throw new InvalidFormatException ("Truncated TLGqoi file");

            if (image_count <= 0 || image_index < 0 || image_index >= image_count)
                throw new InvalidFormatException ("Invalid TLGqoi image selector");

            if (info.Q1 > info.Q2 || info.Q2 > info.Q3)
                throw new InvalidFormatException ("Invalid TLGqoi segment offsets");

            long payload = info.PayloadOffset;
            long q1 = (long)info.Q1;
            long q2 = (long)info.Q2;
            long q3 = (long)info.Q3;
            if (payload + q3 > data.Length)
                throw new InvalidFormatException ("TLGqoi payload exceeds file size");

            int token_offset = (int)payload;
            int token_length = (int)q1;
            int dt_offset = (int)(payload + q1);
            int dt_length = (int)(q2 - q1);
            int rt_offset = (int)(payload + q2);
            int rt_length = (int)(q3 - q2);

            var dtbl = ParseQwordChunk (data, dt_offset, dt_length, "DTBL");
            var rtbl = ParseQwordChunk (data, rt_offset, rt_length, "RTBL");

            if (rtbl.Values.Count == 0 || dtbl.Values.Count == 0)
                throw new InvalidFormatException ("Empty TLGqoi tables");

            if (dtbl.Values[0] < (ulong)(info.BandCount * 2))
                throw new InvalidFormatException ("DTBL count smaller than 2*band_count");
            if (rtbl.Values[0] < (ulong)info.BandCount)
                throw new InvalidFormatException ("RTBL count smaller than band_count");

            ulong rtbl_body_size = 0;
            for (int i = 0; i < info.BandCount; ++i)
                rtbl_body_size += rtbl.Values[1 + i];
            if ((ulong)rtbl.Tail.Length != rtbl_body_size)
                throw new InvalidFormatException ("RTBL body length mismatch");

            var plans = BuildBandPlans (info, dtbl, rtbl);

            int width = (int)info.Width;
            int height = (int)info.Height;
            var pixels = new byte[width * height * 4];
            int out_pos = 0;

            foreach (var plan in plans)
            {
                if (plan.TokenOffset < 0 || plan.TokenLength < 0 || plan.TokenOffset + plan.TokenLength > token_length)
                    throw new InvalidFormatException ("Token stream slice out of range");
                if (plan.RtblOffset < 0 || plan.RtblLength < 0 || plan.RtblOffset + plan.RtblLength > rtbl.Tail.Length)
                    throw new InvalidFormatException ("RTBL stream slice out of range");

                var qoi = new QoiLikeBandDecoder (data, token_offset + plan.TokenOffset, plan.TokenLength);
                var rtbl_reader = new Lz4BlockUleb128Reader (rtbl.Tail, plan.RtblOffset, plan.RtblLength);

                qoi.ResetBandState ();
                ulong control_prefetch;
                if (!rtbl_reader.ReadUleb128 (out control_prefetch))
                    throw new InvalidFormatException ("Missing RTBL header value");

                int visible_pixels = width * plan.BandHeightActual;
                int logical_width = width * image_count;
                int logical_pos = 0;
                int phase = image_index;
                ulong run_remaining = control_prefetch;
                int control_remaining = plan.ControlCount;

                int band_pixels_written = 0;
                while (band_pixels_written < visible_pixels)
                {
                    while (run_remaining != 0)
                    {
                        --run_remaining;
                        if (phase != 0)
                            --phase;
                        else
                        {
                            phase = image_count - 1;
                            pixels[out_pos++] = qoi.PixelB;
                            pixels[out_pos++] = qoi.PixelG;
                            pixels[out_pos++] = qoi.PixelR;
                            pixels[out_pos++] = qoi.PixelA;
                            ++band_pixels_written;
                            if (band_pixels_written >= visible_pixels)
                                break;
                        }
                        ++logical_pos;
                        if (logical_pos >= logical_width)
                        {
                            logical_pos = 0;
                            phase = image_index;
                        }
                        if (band_pixels_written >= visible_pixels)
                            break;
                    }
                    if (band_pixels_written >= visible_pixels)
                        break;

                    if (control_remaining <= 0)
                        throw new InvalidFormatException ("TLGqoi control stream exhausted");

                    --control_remaining;

                    uint base_run;
                    if (!qoi.DecodeToken (out base_run))
                        throw new InvalidFormatException ("TLGqoi token stream exhausted");

                    ulong extra_run;
                    if (!rtbl_reader.ReadUleb128 (out extra_run))
                        throw new InvalidFormatException ("RTBL body exhausted");

                    run_remaining = (ulong)base_run + extra_run;
                }
            }

            if (out_pos != pixels.Length)
                throw new InvalidFormatException ("TLGqoi decoded pixel count mismatch");

            return pixels;
        }

        ImageData ApplyTags (byte[] image, TlgMetaData meta, byte[] tail)
        {
            int i = tail.Length - 8;
            while (i >= 0)
            {
                if ('s' == tail[i+3] && 'g' == tail[i+2] && 'a' == tail[i+1] && 't' == tail[i])
                    break;
                --i;
            }
            if (i < 0)
                return null;
            var tags = new TagsParser (tail, i+4);
            if (!tags.Parse())
                return null;
            var base_name   = tags.GetString (1);
            meta.OffsetX    = tags.GetInt (2) & 0xFFFF;
            meta.OffsetY    = tags.GetInt (3) & 0xFFFF;
            if (string.IsNullOrEmpty (base_name))
                return null;
            int method = 1;
            if (tags.HasKey (4))
                method = tags.GetInt (4);

            base_name = VFS.CombinePath (VFS.GetDirectoryName (meta.FileName), base_name);
            if (base_name == meta.FileName)
                return null;

            TlgMetaData base_info;
            byte[] base_image;
            using (var base_file = VFS.OpenBinaryStream (base_name))
            {
                base_info = ReadMetaData (base_file) as TlgMetaData;
                if (null == base_info)
                    return null;
                base_info.FileName = base_name;
                base_image = ReadTlg (base_file, base_info);
            }
            var pixels = BlendImage (base_image, base_info, image, meta, method);
            PixelFormat format = 32 == base_info.BPP ? PixelFormats.Bgra32 : PixelFormats.Bgr32;
            return ImageData.Create (base_info, format, null, pixels, (int)base_info.Width*4);
        }

        byte[] BlendImage (byte[] base_image, ImageMetaData base_info, byte[] overlay, ImageMetaData overlay_info, int method)
        {
            int dst_stride = (int)base_info.Width * 4;
            int src_stride = (int)overlay_info.Width * 4;
            int dst = overlay_info.OffsetY * dst_stride + overlay_info.OffsetX * 4;
            int src = 0;
            int gap = dst_stride - src_stride;
            for (uint y = 0; y < overlay_info.Height; ++y)
            {
                for (uint x = 0; x < overlay_info.Width; ++x)
                {
                    byte src_alpha = overlay[src+3];
                    if (2 == method)
                    {
                        base_image[dst]   ^= overlay[src];
                        base_image[dst+1] ^= overlay[src+1];
                        base_image[dst+2] ^= overlay[src+2];
                        base_image[dst+3] ^= src_alpha;
                    }
                    else if (src_alpha != 0)
                    {
                        if (0xFF == src_alpha || 0 == base_image[dst+3])
                        {
                            base_image[dst]   = overlay[src];
                            base_image[dst+1] = overlay[src+1];
                            base_image[dst+2] = overlay[src+2];
                            base_image[dst+3] = src_alpha;
                        }
                        else
                        {
                            // FIXME this blending algorithm is oversimplified.
                            base_image[dst+0] = (byte)((overlay[src+0] * src_alpha
                                              + base_image[dst+0] * (0xFF - src_alpha)) / 0xFF);
                            base_image[dst+1] = (byte)((overlay[src+1] * src_alpha
                                              + base_image[dst+1] * (0xFF - src_alpha)) / 0xFF);
                            base_image[dst+2] = (byte)((overlay[src+2] * src_alpha
                                              + base_image[dst+2] * (0xFF - src_alpha)) / 0xFF);
                            base_image[dst+3] = (byte)Math.Max (src_alpha, base_image[dst+3]);
                        }
                    }
                    dst += 4;
                    src += 4;
                }
                dst += gap;
            }
            return base_image;
        }

        const int TVP_TLG6_H_BLOCK_SIZE = 8;
        const int TVP_TLG6_W_BLOCK_SIZE = 8;

        const int TVP_TLG6_GOLOMB_N_COUNT = 4;
        const int TVP_TLG6_LeadingZeroTable_BITS = 12;
        const int TVP_TLG6_LeadingZeroTable_SIZE = (1<<TVP_TLG6_LeadingZeroTable_BITS);

        byte[] ReadV6 (IBinaryStream src, TlgMetaData info)
        {
            int width = (int)info.Width;
            int height = (int)info.Height;
            int colors = info.BPP / 8;
            int max_bit_length = src.ReadInt32();

            int x_block_count = ((width - 1)/ TVP_TLG6_W_BLOCK_SIZE) + 1;
            int y_block_count = ((height - 1)/ TVP_TLG6_H_BLOCK_SIZE) + 1;
            int main_count = width / TVP_TLG6_W_BLOCK_SIZE;
            int fraction = width -  main_count * TVP_TLG6_W_BLOCK_SIZE;

            var image_bits = new uint[height * width];
            var bit_pool = new byte[max_bit_length / 8 + 5];
            var pixelbuf = new uint[width * TVP_TLG6_H_BLOCK_SIZE + 1];
            var filter_types = new byte[x_block_count * y_block_count];
            var zeroline = new uint[width];
            var LZSS_text = new byte[4096];

            // initialize zero line (virtual y=-1 line)
            uint zerocolor = 3 == colors ? 0xff000000 : 0x00000000;
            for (var i = 0; i < width; ++i)
                zeroline[i] = zerocolor;

            uint[] prevline = zeroline;
            int prevline_index = 0;

            // initialize LZSS text (used by chroma filter type codes)
            int p = 0;
            for (uint i = 0; i < 32*0x01010101; i += 0x01010101)
            {
                for (uint j = 0; j < 16*0x01010101; j += 0x01010101)
                {
                    LZSS_text[p++] = (byte)(i       & 0xff);
                    LZSS_text[p++] = (byte)(i >> 8  & 0xff);
                    LZSS_text[p++] = (byte)(i >> 16 & 0xff);
                    LZSS_text[p++] = (byte)(i >> 24 & 0xff);
                    LZSS_text[p++] = (byte)(j       & 0xff);
                    LZSS_text[p++] = (byte)(j >> 8  & 0xff);
                    LZSS_text[p++] = (byte)(j >> 16 & 0xff);
                    LZSS_text[p++] = (byte)(j >> 24 & 0xff);
                }
            }
            // read chroma filter types.
            // chroma filter types are compressed via LZSS as used by TLG5.
            {
                int inbuf_size = src.ReadInt32();
                byte[] inbuf = src.ReadBytes (inbuf_size);
                if (inbuf_size != inbuf.Length)
                    return null;
                TVPTLG5DecompressSlide (filter_types, inbuf, inbuf_size, LZSS_text, 0);
            }

            // for each horizontal block group ...
            for (int y = 0; y < height; y += TVP_TLG6_H_BLOCK_SIZE)
            {
                int ylim = y + TVP_TLG6_H_BLOCK_SIZE;
                if (ylim >= height) ylim = height;

                int pixel_count = (ylim - y) * width;

                // decode values
                for (int c = 0; c < colors; c++)
                {
                    // read bit length
                    int bit_length = src.ReadInt32();

                    // get compress method
                    int method = (bit_length >> 30) & 3;
                    bit_length &= 0x3fffffff;

                    // compute byte length
                    int byte_length = bit_length / 8;
                    if (0 != (bit_length % 8)) byte_length++;

                    // read source from input
                    src.Read (bit_pool, 0, byte_length);

                    // decode values
                    // two most significant bits of bitlength are
                    // entropy coding method;
                    // 00 means Golomb method,
                    // 01 means Gamma method (not yet suppoted),
                    // 10 means modified LZSS method (not yet supported),
                    // 11 means raw (uncompressed) data (not yet supported).
                    
                    switch (method)
                    {
                    case 0:
                        if (c == 0 && colors != 1)
                            TVPTLG6DecodeGolombValuesForFirst (pixelbuf, pixel_count, bit_pool);
                        else
                            TVPTLG6DecodeGolombValues (pixelbuf, c*8, pixel_count, bit_pool);
                        break;
                    default:
                        throw new InvalidFormatException ("Unsupported entropy coding method");
                    }
                }

                // for each line
                int ft = (y / TVP_TLG6_H_BLOCK_SIZE) * x_block_count; // within filter_types
                int skipbytes = (ylim - y) * TVP_TLG6_W_BLOCK_SIZE;

                for (int yy = y; yy < ylim; yy++)
                {
                    int curline = yy*width;

                    int dir = (yy&1)^1;
                    int oddskip = ((ylim - yy -1) - (yy-y));
                    if (0 != main_count)
                    {
                        int start =
                            ((width < TVP_TLG6_W_BLOCK_SIZE) ? width : TVP_TLG6_W_BLOCK_SIZE) *
                                (yy - y);
                        TVPTLG6DecodeLineGeneric (
                            prevline, prevline_index,
                            image_bits, curline,
                            width, 0, main_count,
                            filter_types, ft,
                            skipbytes,
                            pixelbuf, start,
                            zerocolor, oddskip, dir);
                    }

                    if (main_count != x_block_count)
                    {
                        int ww = fraction;
                        if (ww > TVP_TLG6_W_BLOCK_SIZE) ww = TVP_TLG6_W_BLOCK_SIZE;
                        int start = ww * (yy - y);
                        TVPTLG6DecodeLineGeneric (
                            prevline, prevline_index,
                            image_bits, curline,
                            width, main_count, x_block_count,
                            filter_types, ft,
                            skipbytes,
                            pixelbuf, start,
                            zerocolor, oddskip, dir);
                    }
                    prevline = image_bits;
                    prevline_index = curline;
                }
            }
            int stride = width * 4;
            var pixels = new byte[height * stride];
            Buffer.BlockCopy (image_bits, 0, pixels, 0, pixels.Length);
            return pixels;
        }

        byte[] ReadV5 (IBinaryStream src, TlgMetaData info)
        {
            int width = (int)info.Width;
            int height = (int)info.Height;
            int colors = info.BPP / 8;
            int blockheight = src.ReadInt32();
            int blockcount = (height - 1) / blockheight + 1;

            // skip block size section
            src.Seek (blockcount * 4, SeekOrigin.Current);

            int stride = width * 4;
            var image_bits = new byte[height * stride];
            var text = new byte[4096];
            for (int i = 0; i < 4096; ++i)
                text[i] = 0;

            var inbuf = new byte[blockheight * width + 10];
            byte [][] outbuf = new byte[4][];
            for (int i = 0; i < colors; i++)
                outbuf[i] = new byte[blockheight * width + 10];

            int z = 0;
            int prevline = -1;
            for (int y_blk = 0; y_blk < height; y_blk += blockheight)
            {
                // read file and decompress
                for (int c = 0; c < colors; c++)
                {
                    byte mark = src.ReadUInt8();
                    int size;
                    size = src.ReadInt32();
                    if (mark == 0)
                    {
                        // modified LZSS compressed data
                        if (size != src.Read (inbuf, 0, size))
                            return null;
                        z = TVPTLG5DecompressSlide (outbuf[c], inbuf, size, text, z);
                    }
                    else
                    {
                        // raw data
                        src.Read (outbuf[c], 0, size);
                    }
                }

                // compose colors and store
                int y_lim = y_blk + blockheight;
                if (y_lim > height) y_lim = height;
                int outbuf_pos = 0;
                for (int y = y_blk; y < y_lim; y++)
                {
                    int current = y * stride;
                    int current_org = current;
                    if (prevline >= 0)
                    {
                        // not first line
                        switch(colors)
                        {
                        case 3:
                            TVPTLG5ComposeColors3To4 (image_bits, current, prevline,
                                                        outbuf, outbuf_pos, width);
                            break;
                        case 4:
                            TVPTLG5ComposeColors4To4 (image_bits, current, prevline,
                                                        outbuf, outbuf_pos, width);
                            break;
                        }
                    }
                    else
                    {
                        // first line
                        switch(colors)
                        {
                        case 3:
                            for (int pr = 0, pg = 0, pb = 0, x = 0;
                                    x < width; x++)
                            {
                                int b = outbuf[0][outbuf_pos+x];
                                int g = outbuf[1][outbuf_pos+x];
                                int r = outbuf[2][outbuf_pos+x];
                                b += g; r += g;
                                image_bits[current++] = (byte)(pb += b);
                                image_bits[current++] = (byte)(pg += g);
                                image_bits[current++] = (byte)(pr += r);
                                image_bits[current++] = 0xff;
                            }
                            break;
                        case 4:
                            for (int pr = 0, pg = 0, pb = 0, pa = 0, x = 0;
                                    x < width; x++)
                            {
                                int b = outbuf[0][outbuf_pos+x];
                                int g = outbuf[1][outbuf_pos+x];
                                int r = outbuf[2][outbuf_pos+x];
                                int a = outbuf[3][outbuf_pos+x];
                                b += g; r += g;
                                image_bits[current++] = (byte)(pb += b);
                                image_bits[current++] = (byte)(pg += g);
                                image_bits[current++] = (byte)(pr += r);
                                image_bits[current++] = (byte)(pa += a);
                            }
                            break;
                        }
                    }
                    outbuf_pos += width;
                    prevline = current_org;
                }
            }
            return image_bits;
        }

        void TVPTLG5ComposeColors3To4 (byte[] outp, int outp_index, int upper,
                                       byte[][] buf, int bufpos, int width)
        {
            byte pc0 = 0, pc1 = 0, pc2 = 0;
            byte c0, c1, c2;
            for (int x = 0; x < width; x++)
            {
                c0 = buf[0][bufpos+x];
                c1 = buf[1][bufpos+x];
                c2 = buf[2][bufpos+x];
                c0 += c1; c2 += c1;
                outp[outp_index++] = (byte)(((pc0 += c0) + outp[upper+0]) & 0xff);
                outp[outp_index++] = (byte)(((pc1 += c1) + outp[upper+1]) & 0xff);
                outp[outp_index++] = (byte)(((pc2 += c2) + outp[upper+2]) & 0xff);
                outp[outp_index++] = 0xff;
                upper += 4;
            }
        }

        void TVPTLG5ComposeColors4To4 (byte[] outp, int outp_index, int upper,
                                       byte[][] buf, int bufpos, int width)
        {
            byte pc0 = 0, pc1 = 0, pc2 = 0, pc3 = 0;
            byte c0, c1, c2, c3;
            for (int x = 0; x < width; x++)
            {
                c0 = buf[0][bufpos+x];
                c1 = buf[1][bufpos+x];
                c2 = buf[2][bufpos+x];
                c3 = buf[3][bufpos+x];
                c0 += c1; c2 += c1;
                outp[outp_index++] = (byte)(((pc0 += c0) + outp[upper+0]) & 0xff);
                outp[outp_index++] = (byte)(((pc1 += c1) + outp[upper+1]) & 0xff);
                outp[outp_index++] = (byte)(((pc2 += c2) + outp[upper+2]) & 0xff);
                outp[outp_index++] = (byte)(((pc3 += c3) + outp[upper+3]) & 0xff);
                upper += 4;
            }
        }

        int TVPTLG5DecompressSlide (byte[] outbuf, byte[] inbuf, int inbuf_size, byte[] text, int initialr)
        {
            int r = initialr;
            uint flags = 0;
            int o = 0;
            for (int i = 0; i < inbuf_size; )
            {
                if (((flags >>= 1) & 256) == 0)
                {
                    flags = (uint)(inbuf[i++] | 0xff00);
                }
                if (0 != (flags & 1))
                {
                    int mpos = inbuf[i] | ((inbuf[i+1] & 0xf) << 8);
                    int mlen = (inbuf[i+1] & 0xf0) >> 4;
                    i += 2;
                    mlen += 3;
                    if (mlen == 18) mlen += inbuf[i++];

                    while (0 != mlen--)
                    {
                        outbuf[o++] = text[r++] = text[mpos++];
                        mpos &= (4096 - 1);
                        r &= (4096 - 1);
                    }
                }
                else
                {
                    byte c = inbuf[i++];
                    outbuf[o++] = c;
                    text[r++] = c;
                    r &= (4096 - 1);
                }
            }
            return r;
        }

        static uint tvp_make_gt_mask (uint a, uint b)
        {
            uint tmp2 = ~b;
            uint tmp = ((a & tmp2) + (((a ^ tmp2) >> 1) & 0x7f7f7f7f) ) & 0x80808080;
            tmp = ((tmp >> 7) + 0x7f7f7f7f) ^ 0x7f7f7f7f;
            return tmp;
        }

        static uint tvp_packed_bytes_add (uint a, uint b)
        {
            uint tmp = (uint)((((a & b)<<1) + ((a ^ b) & 0xfefefefe) ) & 0x01010100);
            return a+b-tmp;
        }

        static uint tvp_med2 (uint a, uint b, uint c)
        {
            /* do Median Edge Detector   thx, Mr. sugi  at    kirikiri.info */
            uint aa_gt_bb = tvp_make_gt_mask(a, b);
            uint a_xor_b_and_aa_gt_bb = ((a ^ b) & aa_gt_bb);
            uint aa = a_xor_b_and_aa_gt_bb ^ a;
            uint bb = a_xor_b_and_aa_gt_bb ^ b;
            uint n = tvp_make_gt_mask(c, bb);
            uint nn = tvp_make_gt_mask(aa, c);
            uint m = ~(n | nn);
            return (n & aa) | (nn & bb) | ((bb & m) - (c & m) + (aa & m));
        }

        static uint tvp_med (uint a, uint b, uint c, uint v)
        {
            return tvp_packed_bytes_add (tvp_med2 (a, b, c), v);
        }

        static uint tvp_avg (uint a, uint b, uint c, uint v)
        {
            return tvp_packed_bytes_add ((((a&b) + (((a^b) & 0xfefefefe) >> 1)) + ((a^b)&0x01010101)), v);
        }

        delegate uint tvp_decoder (uint a, uint b, uint c, uint v);

        void TVPTLG6DecodeLineGeneric (uint[] prevline, int prevline_index,
                                       uint[] curline, int curline_index,
                                       int width, int start_block, int block_limit,
                                       byte[] filtertypes, int filtertypes_index,
                                       int skipblockbytes,
                                       uint[] inbuf, int inbuf_index,
                                       uint initialp, int oddskip, int dir)
        {
            /*
                chroma/luminosity decoding
                (this does reordering, color correlation filter, MED/AVG  at a time)
            */
            uint p, up;

            if (0 != start_block)
            {
                prevline_index += start_block * TVP_TLG6_W_BLOCK_SIZE;
                curline_index  += start_block * TVP_TLG6_W_BLOCK_SIZE;
                p  = curline[curline_index-1];
                up = prevline[prevline_index-1];
            }
            else
            {
                p = up = initialp;
            }

            inbuf_index += skipblockbytes * start_block;
            int step = 0 != (dir & 1) ? 1 : -1;

            for (int i = start_block; i < block_limit; i++)
            {
                int w = width - i*TVP_TLG6_W_BLOCK_SIZE;
                if (w > TVP_TLG6_W_BLOCK_SIZE) w = TVP_TLG6_W_BLOCK_SIZE;
                int ww = w;
                if (step == -1) inbuf_index += ww-1;
                if (0 != (i & 1)) inbuf_index += oddskip * ww;

                tvp_decoder decoder;
                switch (filtertypes[filtertypes_index+i])
                {
                case 0:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, v);
                    break;
                case 1:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, v);
                    break;
                case 2:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (((v>>8)&0xff)<<8) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 3:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (((v>>8)&0xff)<<8) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 4:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 5:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 6:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 7:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 8:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 9:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 10:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 11:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 12:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 13:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 14:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 15:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 16:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 17:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 18:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))) + ((v&0xff000000))));
                    break;
                case 19:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))) + ((v&0xff000000))));
                    break;
                case 20:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 21:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 22:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 23:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 24:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 25:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 26:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 27:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff))) + ((v&0xff000000))));
                    break;
                case 28:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff)+((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 29:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+(v&0xff)+((v>>8)&0xff)+((v>>16)&0xff))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v>>16)&0xff))<<8)) + (0xff & ((v&0xff)+((v>>8)&0xff)+((v>>16)&0xff))) + ((v&0xff000000))));
                    break;
                case 30:
                    decoder = (a, b, c, v) => tvp_med (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v&0xff)<<1))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v&0xff)<<1))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                case 31:
                    decoder = (a, b, c, v) => tvp_avg (a, b, c, (uint)
                        ((0xff0000 & ((((v>>16)&0xff)+((v&0xff)<<1))<<16)) + (0xff00 & ((((v>>8)&0xff)+((v&0xff)<<1))<<8)) + (0xff & ((v&0xff))) + ((v&0xff000000))));
                    break;
                default: return;
                }
                do {
                    uint u = prevline[prevline_index];
                    p = decoder (p, u, up, inbuf[inbuf_index]);
                    up = u;
                    curline[curline_index] = p;
                    curline_index++;
                    prevline_index++;
                    inbuf_index += step;
                } while (0 != --w);
                if (step == 1)
                    inbuf_index += skipblockbytes - ww;
                else
                    inbuf_index += skipblockbytes + 1;
                if (0 != (i&1)) inbuf_index -= oddskip * ww;
            }
        }

        static class TVP_Tables
        {
            public static byte[] TVPTLG6LeadingZeroTable = new byte[TVP_TLG6_LeadingZeroTable_SIZE];
            public static sbyte[,] TVPTLG6GolombBitLengthTable = new sbyte
                [TVP_TLG6_GOLOMB_N_COUNT*2*128, TVP_TLG6_GOLOMB_N_COUNT];
            static short[,] TVPTLG6GolombCompressed = new short[TVP_TLG6_GOLOMB_N_COUNT,9] {
                    {3,7,15,27,63,108,223,448,130,},
                    {3,5,13,24,51,95,192,384,257,},
                    {2,5,12,21,39,86,155,320,384,},
                    {2,3,9,18,33,61,129,258,511,},
                    /* Tuned by W.Dee, 2004/03/25 */
            };

            static TVP_Tables ()
            {
                TVPTLG6InitLeadingZeroTable();
                TVPTLG6InitGolombTable();
            }

            static void TVPTLG6InitLeadingZeroTable ()
            {
                /* table which indicates first set bit position + 1. */
                /* this may be replaced by BSF (IA32 instrcution). */
                
                for (int i = 0; i < TVP_TLG6_LeadingZeroTable_SIZE; i++)
                {
                    int cnt = 0;
                    int j;
                    for(j = 1; j != TVP_TLG6_LeadingZeroTable_SIZE && 0 == (i & j);
                        j <<= 1, cnt++);
                    cnt++;
                    if (j == TVP_TLG6_LeadingZeroTable_SIZE) cnt = 0;
                    TVPTLG6LeadingZeroTable[i] = (byte)cnt;
                }
            }

            static void TVPTLG6InitGolombTable()
            {
                for (int n = 0; n < TVP_TLG6_GOLOMB_N_COUNT; n++)
                {
                    int a = 0;
                    for (int i = 0; i < 9; i++)
                    {
                        for (int j = 0; j < TVPTLG6GolombCompressed[n,i]; j++)
                            TVPTLG6GolombBitLengthTable[a++,n] = (sbyte)i;
                    }
                    if(a != TVP_TLG6_GOLOMB_N_COUNT*2*128)
                        throw new Exception ("Invalid data initialization");   /* THIS MUST NOT BE EXECUETED! */
                    /* (this is for compressed table data check) */
                }
            }
        }

        void TVPTLG6DecodeGolombValuesForFirst (uint[] pixelbuf, int pixel_count, byte[] bit_pool)
        {
            /*
                decode values packed in "bit_pool".
                values are coded using golomb code.
                "ForFirst" function do dword access to pixelbuf,
                clearing with zero except for blue (least siginificant byte).
            */
            int bit_pool_index = 0;

            int n = TVP_TLG6_GOLOMB_N_COUNT - 1; /* output counter */
            int a = 0; /* summary of absolute values of errors */

            int bit_pos = 1;
            bool zero = 0 == (bit_pool[bit_pool_index] & 1);

            for (int pixel = 0; pixel < pixel_count; )
            {
                /* get running count */
                int count;

                {
                    uint t = LittleEndian.ToUInt32 (bit_pool, bit_pool_index) >> bit_pos;
                    int b = TVP_Tables.TVPTLG6LeadingZeroTable[t & (TVP_TLG6_LeadingZeroTable_SIZE-1)];
                    int bit_count = b;
                    while (0 == b)
                    {
                        bit_count += TVP_TLG6_LeadingZeroTable_BITS;
                        bit_pos += TVP_TLG6_LeadingZeroTable_BITS;
                        bit_pool_index += bit_pos >> 3;
                        bit_pos &= 7;
                        t = LittleEndian.ToUInt32 (bit_pool, bit_pool_index) >> bit_pos;
                        b = TVP_Tables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                        bit_count += b;
                    }
                    bit_pos += b;
                    bit_pool_index += bit_pos >> 3;
                    bit_pos &= 7;

                    bit_count --;
                    count = 1 << bit_count;
                    count += ((LittleEndian.ToInt32 (bit_pool, bit_pool_index) >> (bit_pos)) & (count-1));

                    bit_pos += bit_count;
                    bit_pool_index += bit_pos >> 3;
                    bit_pos &= 7;
                }
                if (zero)
                {
                    /* zero values */

                    /* fill distination with zero */
                    do { pixelbuf[pixel++] = 0; } while (0 != --count);
                    zero = !zero;
                }
                else
                {
                    /* non-zero values */

                    /* fill distination with glomb code */
                    
                    do
                    {
                        int k = TVP_Tables.TVPTLG6GolombBitLengthTable[a,n];
                        int v, sign;

                        uint t = LittleEndian.ToUInt32 (bit_pool, bit_pool_index) >> bit_pos;
                        int bit_count;
                        int b;
                        if (0 != t)
                        {
                            b = TVP_Tables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                            bit_count = b;
                            while (0 == b)
                            {
                                bit_count += TVP_TLG6_LeadingZeroTable_BITS;
                                bit_pos += TVP_TLG6_LeadingZeroTable_BITS;
                                bit_pool_index += bit_pos >> 3;
                                bit_pos &= 7;
                                t = LittleEndian.ToUInt32 (bit_pool, bit_pool_index) >> bit_pos;
                                b = TVP_Tables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                                bit_count += b;
                            }
                            bit_count --;
                        }
                        else
                        {
                            bit_pool_index += 5;
                            bit_count = bit_pool[bit_pool_index-1];
                            bit_pos = 0;
                            t = LittleEndian.ToUInt32 (bit_pool, bit_pool_index);
                            b = 0;
                        }

                        v = (int)((bit_count << k) + ((t >> b) & ((1<<k)-1)));
                        sign = (v & 1) - 1;
                        v >>= 1;
                        a += v;
                        pixelbuf[pixel++] = (byte)((v ^ sign) + sign + 1);

                        bit_pos += b;
                        bit_pos += k;
                        bit_pool_index += bit_pos >> 3;
                        bit_pos &= 7;

                        if (--n < 0)
                        {
                            a >>= 1;
                            n = TVP_TLG6_GOLOMB_N_COUNT - 1;
                        }
                    } while (0 != --count);
                    zero = !zero;
                }
            }
        }

        void TVPTLG6DecodeGolombValues (uint[] pixelbuf, int offset, int pixel_count, byte[] bit_pool)
        {
            /*
                decode values packed in "bit_pool".
                values are coded using golomb code.
            */
            uint mask = (uint)~(0xff << offset);
            int bit_pool_index = 0;

            int n = TVP_TLG6_GOLOMB_N_COUNT - 1; /* output counter */
            int a = 0; /* summary of absolute values of errors */

            int bit_pos = 1;
            bool zero = 0 == (bit_pool[bit_pool_index] & 1);

            for (int pixel = 0; pixel < pixel_count; )
            {
                /* get running count */
                int count;

                {
                    uint t = LittleEndian.ToUInt32 (bit_pool, bit_pool_index) >> bit_pos;
                    int b = TVP_Tables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                    int bit_count = b;
                    while (0 == b)
                    {
                        bit_count += TVP_TLG6_LeadingZeroTable_BITS;
                        bit_pos += TVP_TLG6_LeadingZeroTable_BITS;
                        bit_pool_index += bit_pos >> 3;
                        bit_pos &= 7;
                        t = LittleEndian.ToUInt32 (bit_pool, bit_pool_index) >> bit_pos;
                        b = TVP_Tables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                        bit_count += b;
                    }
                    bit_pos += b;
                    bit_pool_index += bit_pos >> 3;
                    bit_pos &= 7;

                    bit_count --;
                    count = 1 << bit_count;
                    count += (int)((LittleEndian.ToUInt32 (bit_pool, bit_pool_index) >> (bit_pos)) & (count-1));

                    bit_pos += bit_count;
                    bit_pool_index += bit_pos >> 3;
                    bit_pos &= 7;
                }
                if (zero)
                {
                    /* zero values */

                    /* fill distination with zero */
                    do { pixelbuf[pixel++] &= mask; } while (0 != --count);
                    zero = !zero;
                }
                else
                {
                    /* non-zero values */

                    /* fill distination with glomb code */
                    
                    do
                    {
                        int k = TVP_Tables.TVPTLG6GolombBitLengthTable[a,n];
                        int v, sign;

                        uint t = LittleEndian.ToUInt32 (bit_pool, bit_pool_index) >> bit_pos;
                        int bit_count;
                        int b;
                        if (0 != t)
                        {
                            b = TVP_Tables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                            bit_count = b;
                            while (0 == b)
                            {
                                bit_count += TVP_TLG6_LeadingZeroTable_BITS;
                                bit_pos += TVP_TLG6_LeadingZeroTable_BITS;
                                bit_pool_index += bit_pos >> 3;
                                bit_pos &= 7;
                                t = LittleEndian.ToUInt32 (bit_pool, bit_pool_index) >> bit_pos;
                                b = TVP_Tables.TVPTLG6LeadingZeroTable[t&(TVP_TLG6_LeadingZeroTable_SIZE-1)];
                                bit_count += b;
                            }
                            bit_count --;
                        }
                        else
                        {
                            bit_pool_index += 5;
                            bit_count = bit_pool[bit_pool_index-1];
                            bit_pos = 0;
                            t = LittleEndian.ToUInt32 (bit_pool, bit_pool_index);
                            b = 0;
                        }

                        v = (int)((bit_count << k) + ((t >> b) & ((1<<k)-1)));
                        sign = (v & 1) - 1;
                        v >>= 1;
                        a += v;
                        uint c = (uint)((pixelbuf[pixel] & mask) | (uint)((byte)((v ^ sign) + sign + 1) << offset));
                        pixelbuf[pixel++] = c;

                        bit_pos += b;
                        bit_pos += k;
                        bit_pool_index += bit_pos >> 3;
                        bit_pos &= 7;

                        if (--n < 0)
                        {
                            a >>= 1;
                            n = TVP_TLG6_GOLOMB_N_COUNT - 1;
                        }
                    } while (0 != --count);
                    zero = !zero;
                }
            }
        }

        sealed class QwordChunk
        {
            public readonly List<ulong> Values;
            public readonly byte[] Tail;

            public QwordChunk (List<ulong> values, byte[] tail)
            {
                Values = values;
                Tail = tail;
            }
        }

        sealed class BandPlan
        {
            public int Index;
            public int YStart;
            public int BandHeightActual;
            public int TokenOffset;
            public int TokenLength;
            public int RtblOffset;
            public int RtblLength;
            public int ControlCount;
        }

        QwordChunk ParseQwordChunk (byte[] data, int offset, int length, string tag)
        {
            if (offset < 0 || length < 8 || offset + length > data.Length)
                throw new InvalidFormatException ("Invalid chunk range");

            if (data[offset+0] != tag[0] || data[offset+1] != tag[1] || data[offset+2] != tag[2] || data[offset+3] != tag[3])
                throw new InvalidFormatException (string.Format ("Expected chunk {0}", tag));

            int size = LittleEndian.ToInt32 (data, offset + 4);
            if (size < 0 || size > length - 8)
                throw new InvalidFormatException (string.Format ("Truncated {0} chunk", tag));

            var values = new List<ulong>();
            int pos = offset + 8;
            int end = pos + size;
            while (pos < end)
            {
                ulong value;
                pos = ReadUleb64 (data, pos, end, out value);
                values.Add (value);
            }

            int tail_len = length - 8 - size;
            var tail = new byte[tail_len];
            if (tail_len > 0)
                Buffer.BlockCopy (data, end, tail, 0, tail_len);
            return new QwordChunk (values, tail);
        }

        int ReadUleb64 (byte[] data, int pos, int end, out ulong value)
        {
            int shift = 0;
            value = 0;
            while (true)
            {
                if (pos >= end)
                    throw new InvalidFormatException ("Truncated ULEB64");
                byte b = data[pos++];
                value |= (ulong)(b & 0x7F) << shift;
                if (b < 0x80)
                    return pos;
                shift += 7;
                if (shift >= 64)
                    throw new InvalidFormatException ("ULEB64 too large");
            }
        }

        List<BandPlan> BuildBandPlans (TlgMetaData info, QwordChunk dtbl, QwordChunk rtbl)
        {
            var plans = new List<BandPlan> (info.BandCount);
            int token_offset = 0;
            int rtbl_offset = 0;
            int y = 0;
            int height = (int)info.Height;

            for (int i = 0; i < info.BandCount && y < height; ++i)
            {
                int band_h = info.BandHeight;
                if (y + band_h > height)
                    band_h = height - y;

                int token_len = (int)dtbl.Values[1 + i * 2];
                int control_count = (int)dtbl.Values[1 + i * 2 + 1];
                int rtbl_len = (int)rtbl.Values[1 + i];

                plans.Add (new BandPlan
                {
                    Index = i,
                    YStart = y,
                    BandHeightActual = band_h,
                    TokenOffset = token_offset,
                    TokenLength = token_len,
                    RtblOffset = rtbl_offset,
                    RtblLength = rtbl_len,
                    ControlCount = control_count,
                });

                token_offset += token_len;
                rtbl_offset += rtbl_len;
                y += band_h;
            }
            return plans;
        }

        sealed class Lz4BlockUleb128Reader
        {
            readonly byte[] m_data;
            readonly int m_end;
            int m_file_pos;
            readonly byte[][] m_bank = new byte[][] { null, null };
            int m_bank_index = 1;
            byte[] m_cur = null;
            int m_cur_pos = 0;

            public Lz4BlockUleb128Reader (byte[] data, int offset, int length)
            {
                m_data = data;
                m_file_pos = offset;
                m_end = offset + length;
            }

            bool FillBlock ()
            {
                if (m_file_pos + 4 > m_end)
                    return false;
                uint header = LittleEndian.ToUInt32 (m_data, m_file_pos);
                m_file_pos += 4;
                if (0 == header)
                    return false;

                int compressed_size = (int)((header >> 16) & 0xFFFF);
                bool use_dict = 0 != (header & 0x8000);
                int out_size = (int)(header & 0x7FFF);
                if (0 == out_size)
                    out_size = 0x8000;
                if (m_file_pos + compressed_size > m_end)
                    throw new InvalidFormatException ("Truncated RTBL LZ4 block");

                byte[] previous = use_dict ? m_bank[m_bank_index & 1] : null;
                m_bank_index ^= 1;
                byte[] outbuf = Lz4DecompressBlockRaw (m_data, m_file_pos, compressed_size, out_size, previous);
                m_file_pos += compressed_size;
                m_bank[m_bank_index & 1] = outbuf;
                m_cur = outbuf;
                m_cur_pos = 0;
                return true;
            }

            public bool ReadUleb128 (out ulong value)
            {
                int shift = 0;
                value = 0;
                while (true)
                {
                    if (null == m_cur || m_cur_pos >= m_cur.Length)
                    {
                        if (!FillBlock())
                            return false;
                    }
                    byte b = m_cur[m_cur_pos++];
                    value |= (ulong)(b & 0x7F) << shift;
                    if (b < 0x80)
                        return true;
                    shift += 7;
                    if (shift >= 64)
                        throw new InvalidFormatException ("RTBL ULEB128 too large");
                }
            }
        }

        static byte[] Lz4DecompressBlockRaw (byte[] src, int offset, int length, int out_size, byte[] dictionary)
        {
            int ip = offset;
            int end = offset + length;
            var output = new byte[out_size];
            int op = 0;
            int dict_len = null != dictionary ? dictionary.Length : 0;

            while (op < out_size)
            {
                if (ip >= end)
                    throw new InvalidFormatException ("LZ4 block ended early");
                int token = src[ip++];

                int lit_len = token >> 4;
                if (15 == lit_len)
                {
                    byte s;
                    do
                    {
                        if (ip >= end)
                            throw new InvalidFormatException ("Truncated LZ4 literal length");
                        s = src[ip++];
                        lit_len += s;
                    } while (0xFF == s);
                }

                if (ip + lit_len > end)
                    throw new InvalidFormatException ("Truncated LZ4 literals");
                if (op + lit_len > out_size)
                    lit_len = out_size - op;
                Buffer.BlockCopy (src, ip, output, op, lit_len);
                ip += lit_len;
                op += lit_len;
                if (op >= out_size)
                    break;

                if (ip + 2 > end)
                    throw new InvalidFormatException ("Truncated LZ4 match offset");
                int match_offset = src[ip] | (src[ip+1] << 8);
                ip += 2;
                if (0 == match_offset)
                    throw new InvalidFormatException ("Invalid LZ4 offset");

                int match_len = token & 0x0F;
                if (15 == match_len)
                {
                    byte s;
                    do
                    {
                        if (ip >= end)
                            throw new InvalidFormatException ("Truncated LZ4 match length");
                        s = src[ip++];
                        match_len += s;
                    } while (0xFF == s);
                }
                match_len += 4;

                while (match_len > 0 && op < out_size)
                {
                    int src_index = op - match_offset;
                    byte v;
                    if (src_index >= 0)
                    {
                        v = output[src_index];
                    }
                    else
                    {
                        int dict_index = dict_len + src_index;
                        if (null == dictionary || dict_index < 0 || dict_index >= dict_len)
                            throw new InvalidFormatException ("LZ4 offset exceeds available dictionary/window");
                        v = dictionary[dict_index];
                    }
                    output[op++] = v;
                    --match_len;
                }
            }
            if (op == out_size)
                return output;

            var result = new byte[op];
            Buffer.BlockCopy (output, 0, result, 0, op);
            return result;
        }

        sealed class QoiLikeBandDecoder
        {
            readonly byte[] m_data;
            readonly int m_end;
            int m_pos;
            readonly Bgra32[] m_index = new Bgra32[64];
            Bgra32 m_pixel;

            public byte PixelB { get { return m_pixel.B; } }
            public byte PixelG { get { return m_pixel.G; } }
            public byte PixelR { get { return m_pixel.R; } }
            public byte PixelA { get { return m_pixel.A; } }

            public QoiLikeBandDecoder (byte[] data, int offset, int length)
            {
                m_data = data;
                m_pos = offset;
                m_end = offset + length;
                m_pixel = new Bgra32 (0, 0, 0, 0xFF);
            }

            static int Hash (Bgra32 p)
            {
                return (7 * p.B + 5 * p.G + 3 * p.R + 11 * p.A) & 63;
            }

            Bgra32 StorePixel (Bgra32 p)
            {
                m_pixel = p;
                m_index[Hash (p)] = p;
                return p;
            }

            public bool DecodeToken (out uint run)
            {
                if (m_pos >= m_end)
                {
                    run = 0;
                    return false;
                }

                byte op = m_data[m_pos++];
                if (0xFF == op)
                {
                    if (m_pos + 4 > m_end)
                        throw new InvalidFormatException ("Truncated RGBA token");
                    byte r = m_data[m_pos++];
                    byte g = m_data[m_pos++];
                    byte b = m_data[m_pos++];
                    byte a = m_data[m_pos++];
                    StorePixel (new Bgra32 (b, g, r, a));
                    run = 1;
                    return true;
                }
                if (0xFE == op)
                {
                    if (m_pos + 3 > m_end)
                        throw new InvalidFormatException ("Truncated RGB token");
                    byte r = m_data[m_pos++];
                    byte g = m_data[m_pos++];
                    byte b = m_data[m_pos++];
                    StorePixel (new Bgra32 (b, g, r, m_pixel.A));
                    run = 1;
                    return true;
                }

                int tag = op >> 6;
                if (0 == tag)
                {
                    m_pixel = m_index[op & 0x3F];
                    run = 1;
                    return true;
                }
                if (1 == tag)
                {
                    StorePixel (new Bgra32 (
                        (byte)((m_pixel.B + (op & 0x03) - 2) & 0xFF),
                        (byte)((m_pixel.G + ((op >> 2) & 0x03) - 2) & 0xFF),
                        (byte)((m_pixel.R + ((op >> 4) & 0x03) - 2) & 0xFF),
                        m_pixel.A));
                    run = 1;
                    return true;
                }
                if (2 == tag)
                {
                    if (m_pos >= m_end)
                        throw new InvalidFormatException ("Truncated LUMA token");
                    byte b1 = m_data[m_pos++];
                    int vg = (op & 0x3F) - 32;
                    StorePixel (new Bgra32 (
                        (byte)((m_pixel.B + vg + (b1 & 0x0F) - 8) & 0xFF),
                        (byte)((m_pixel.G + vg) & 0xFF),
                        (byte)((m_pixel.R + vg + ((b1 >> 4) & 0x0F) - 8) & 0xFF),
                        m_pixel.A));
                    run = 1;
                    return true;
                }
                if (3 == tag)
                {
                    run = (uint)((op & 0x3F) + 1);
                    return true;
                }

                run = 0;
                return false;
            }

            public void ResetBandState ()
            {
                for (int i = 0; i < m_index.Length; ++i)
                    m_index[i] = new Bgra32 (0, 0, 0, 0);
                m_pixel = new Bgra32 (0, 0, 0, 0xFF);

                uint run;
                if (!DecodeToken (out run) || run != 1 || m_pixel.B != 0 || m_pixel.G != 0 || m_pixel.R != 0 || m_pixel.A != 0)
                    throw new InvalidFormatException ("Band token prologue #0 mismatch");
                if (!DecodeToken (out run) || run != 1 || m_pixel.B != 0 || m_pixel.G != 0 || m_pixel.R != 0 || m_pixel.A != 0xFF)
                    throw new InvalidFormatException ("Band token prologue #1 mismatch");
            }
        }

        struct Bgra32
        {
            public byte B;
            public byte G;
            public byte R;
            public byte A;

            public Bgra32 (byte b, byte g, byte r, byte a)
            {
                B = b;
                G = g;
                R = r;
                A = a;
            }
        }
    }

    internal class TagsParser
    {
        byte[]                              m_tags;
        Dictionary<int, Tuple<int, int>>    m_map = new Dictionary<int, Tuple<int, int>>();
        int                                 m_offset;

        public TagsParser (byte[] tags, int offset)
        {
            m_tags = tags;
            m_offset = offset;
        }

        public bool Parse ()
        {
            int length = LittleEndian.ToInt32 (m_tags, m_offset);
            m_offset += 4;
            if (length <= 0 || length > m_tags.Length - m_offset)
                return false;
            while (m_offset < m_tags.Length)
            {
                int key_len = ParseInt();
                if (key_len < 0)
                    return false;
                int key;
                switch (key_len)
                {
                case 1:
                    key = m_tags[m_offset];
                    break;
                case 2:
                    key = LittleEndian.ToUInt16 (m_tags, m_offset);
                    break;
                case 4:
                    key = LittleEndian.ToInt32 (m_tags, m_offset);
                    break;
                default:
                    return false;
                }
                m_offset += key_len + 1;
                int value_len = ParseInt();
                if (value_len < 0)
                    return false;
                m_map[key] = Tuple.Create (m_offset, value_len);
                m_offset += value_len + 1;
            }
            return m_map.Count > 0;
        }

        int ParseInt ()
        {
            int colon = Array.IndexOf (m_tags, (byte)':', m_offset);
            if (-1 == colon)
                return -1;
            var len_str = Encoding.ASCII.GetString (m_tags, m_offset, colon-m_offset);
            m_offset = colon + 1;
            return Int32.Parse (len_str);
        }

        public bool HasKey (int key)
        {
            return m_map.ContainsKey (key);
        }

        public int GetInt (int key)
        {
            var val = m_map[key];
            switch (val.Item2)
            {
            case 0: return 0;
            case 1: return m_tags[val.Item1];
            case 2: return LittleEndian.ToUInt16 (m_tags, val.Item1);
            case 4: return LittleEndian.ToInt32 (m_tags, val.Item1);
            default: throw new InvalidFormatException();
            }
        }

        public string GetString (int key)
        {
            var val = m_map[key];
            return Encodings.cp932.GetString (m_tags, val.Item1, val.Item2);
        }
    }
}