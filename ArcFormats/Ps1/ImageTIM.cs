//! \file       ImageTIM.cs
//! \date       2026 Jan 05
//! \brief      Standard PlayStation (PS1) image format.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameRes.Formats.Sony
{
    internal class TimMetaData : ImageMetaData
    {
        public uint BppMode;
        public bool HasClut;
        public uint ClutOffset;
        public uint ImageOffset;
    }

    [Export(typeof(ImageFormat))]
    public class TimFormat : ImageFormat
    {
        public override string Tag { get { return "TIM"; } }
        public override string Description { get { return "PlayStation image format"; } }
        public override uint Signature { get { return 0x00000010; } } // Magic: 10 00 00 00

        public override ImageMetaData ReadMetaData(IBinaryStream stream)
        {
            var header = stream.ReadHeader(8);
            if (header[0] != 0x10 || header[1] != 0)
                return null;

            ushort flag = header.ToUInt16(4);
            uint mode = (uint)(flag & 3);
            bool hasClut = (flag & 8) != 0;

            uint currentOffset = 8;
            uint clutOffset = 0;

            if (hasClut)
            {
                clutOffset = currentOffset;
                stream.Position = currentOffset;
                uint clutSize = stream.ReadUInt32();
                currentOffset += clutSize;
            }

            uint imageOffset = currentOffset;
            if (imageOffset + 12 > stream.Length) return null;

            // Image Header: BlockSize(4), VramX(2), VramY(2), WordWidth(2), Height(2)
            stream.Position = imageOffset + 8;
            ushort wordWidth = stream.ReadUInt16();
            ushort height = stream.ReadUInt16();

            int bpp;
            int width;
            switch (mode)
            {
                case 0: bpp = 4; width = wordWidth * 4; break;
                case 1: bpp = 8; width = wordWidth * 2; break;
                case 2: bpp = 16; width = wordWidth; break;
                case 3: bpp = 24; width = (wordWidth * 2) / 3; break;
                default: return null;
            }

            return new TimMetaData
            {
                Width = (uint)width,
                Height = height,
                BPP = bpp,
                BppMode = mode,
                HasClut = hasClut,
                ClutOffset = clutOffset,
                ImageOffset = imageOffset
            };
        }

        public override ImageData Read(IBinaryStream stream, ImageMetaData info)
        {
            var meta = (TimMetaData)info;
            BitmapPalette palette = null;

            if (meta.HasClut)
            {
                // CLUT Header: BlockSize(4), X(2), Y(2), Width(2), Height(2)
                // Width/Height are at offsets 8 and 10 within the CLUT block.
                stream.Position = meta.ClutOffset + 8;
                ushort colorsCount = stream.ReadUInt16();
                ushort rows = stream.ReadUInt16();
                int totalColors = colorsCount * rows;

                // Color data starts at offset 12
                var colorData = stream.ReadBytes(totalColors * 2);
                var colors = new Color[totalColors];
                for (int i = 0; i < totalColors; i++)
                {
                    ushort c = BitConverter.ToUInt16(colorData, i * 2);
                    colors[i] = ConvertBgr1555(c);
                }
                palette = new BitmapPalette(colors);
            }

            // Image data starts at ImageOffset + 12 (skipping the 12-byte header)
            stream.Position = meta.ImageOffset + 12;
            int pixelDataSize = (int)(meta.Width * meta.Height * meta.BPP / 8);
            if (meta.BPP == 4) pixelDataSize = (int)(meta.Width * meta.Height / 2);

            var pixels = stream.ReadBytes(pixelDataSize);
            PixelFormat format;

            switch (meta.BppMode)
            {
                case 0: format = PixelFormats.Indexed4; break;
                case 1: format = PixelFormats.Indexed8; break;
                case 2: format = PixelFormats.Bgr555; break;
                case 3: format = PixelFormats.Bgr24; break;
                default: throw new NotSupportedException("Unsupported TIM mode");
            }

            // PS1 4bpp nibbles are stored in reverse order compared to Windows standard
            if (meta.BppMode == 0)
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    byte b = pixels[i];
                    pixels[i] = (byte)((b >> 4) | (b << 4));
                }
            }

            return ImageData.Create(info, format, palette, pixels);
        }

        public override void Write(Stream file, ImageData image)
        {
            throw new NotImplementedException();
        }

        private Color ConvertBgr1555(ushort c)
        {
            // PS1 BGR1555: Bit 15=STP, 14-10=B, 9-5=G, 4-0=R
            byte r = (byte)((c & 0x1F) << 3);
            byte g = (byte)(((c >> 5) & 0x1F) << 3);
            byte b = (byte)(((c >> 10) & 0x1F) << 3);

            // Transparency logic: 0,0,0 is transparent unless STP bit is set.
            byte a = (c == 0) ? (byte)0 : (byte)255;
            if ((c & 0x8000) != 0) a = 255;

            return Color.FromArgb(a, r, g, b);
        }
    }
}