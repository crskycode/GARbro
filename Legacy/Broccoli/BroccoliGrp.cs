using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq; // Adicionado para operações de lista se necessário
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameRes.Utility;

namespace GameRes.Formats.Broccoli
{
    [Export(typeof(ImageFormat))]
    public class GrpFormat : ImageFormat
    {
        public override string         Tag { get { return "GRP/BROCCOLI"; } }
        public override string Description { get { return "Broccoli engine image"; } }
        public override uint     Signature { get { return 0; } } 
        
        public override ImageMetaData ReadMetaData (IBinaryStream file)
        {
            // Precisamos descomprimir para saber o tamanho real
            using (var data = DecompressData(file))
            {
                if (data == null) return null;

                var size = data.Length;
                uint width = 0, height = 0;
                int bpp = 32; 

                // Mesma lógica de resolução do seu script Python
                if (size == 1920000)      { width = 800; height = 600; }
                else if (size == 1228800) { width = 640; height = 480; }
                else if (size == 1536000) { width = 800; height = 480; }
                else if (size == 768000)  { width = 640; height = 400; }
                else if (size == 307200)  { width = 640; height = 480; bpp = 8; }
                else if (size == 96000)   { width = 200; height = 120; }
                else
                {
                    // Fallback: Tenta ler header de 4 bytes (Width/Height)
                    if (size > 4)
                    {
                        data.Position = 0;
                        var reader = new BinaryReader(data);
                        ushort w = reader.ReadUInt16();
                        ushort h = reader.ReadUInt16();
                        if (w * h * 4 == size - 4)
                        {
                            width = w; 
                            height = h;
                        }
                    }
                }

                if (width == 0) return null;

                return new ImageMetaData
                {
                    Width = width,
                    Height = height,
                    BPP = bpp,
                };
            }
        }

        public override ImageData Read (IBinaryStream file, ImageMetaData info)
        {
            using (var stream = DecompressData(file))
            {
                if (stream == null) throw new InvalidFormatException();

                // Pular header de 4 bytes se for o caso do fallback
                int headerSkip = 0;
                if (stream.Length != info.Width * info.Height * (info.BPP / 8))
                {
                    if (stream.Length == (info.Width * info.Height * 4) + 4)
                        headerSkip = 4;
                }

                stream.Position = headerSkip;

                // Caso 1: Máscara isolada (Grayscale)
                if (info.BPP == 8)
                {
                    var gray = new byte[info.Width * info.Height];
                    stream.Read(gray, 0, gray.Length);
                    return ImageData.Create(info, PixelFormats.Gray8, null, gray);
                }

                // Caso 2: Imagem Colorida (BGRX)
                // Devido às limitações da API, não vamos carregar a máscara (_m) aqui.
                // O GARbro mostrará a imagem com fundo preto/sólido.
                // Use o seu script Python para combinar com a máscara depois.
                
                var pixels = new byte[info.Width * info.Height * 4];
                stream.Read(pixels, 0, pixels.Length);

                // O formato é geralmente Bgr32 (o 4º byte é lixo/padding, não alpha real)
                return ImageData.Create(info, PixelFormats.Bgr32, null, pixels);
            }
        }

        public override void Write (Stream file, ImageData image)
        {
            throw new System.NotImplementedException("Use repack.py");
        }

        private MemoryStream DecompressData(IBinaryStream input)
        {
            input.Position = 0;
            byte[] buffer = input.ReadBytes(2048);
            int zlibOffset = -1;

            // Busca assinatura ZLIB (78 9C, etc)
            for (int i = 0; i < buffer.Length - 1; i++)
            {
                if (buffer[i] == 0x78 && 
                   (buffer[i+1] == 0x9C || buffer[i+1] == 0xDA || buffer[i+1] == 0x01))
                {
                    zlibOffset = i;
                    break;
                }
            }

            if (zlibOffset == -1) return null;

            try 
            {
                // Pula o header ZLIB (2 bytes) para usar DeflateStream padrão
                input.Position = zlibOffset + 2; 

                using (var zStream = new System.IO.Compression.DeflateStream(input.AsStream, System.IO.Compression.CompressionMode.Decompress, true))
                {
                    var output = new MemoryStream();
                    zStream.CopyTo(output);
                    output.Position = 0;
                    return output;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}