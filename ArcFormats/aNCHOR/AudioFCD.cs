//! \file       AudioFCD.cs
//! \date       2026-01-19
//! \brief      AGES Mk2 audio format.
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
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

namespace GameRes.Formats.Anchor
{
    [Export(typeof(AudioFormat))]
    public class FcdAudio : AudioFormat
    {
        public override string         Tag { get { return "FCD"; } }
        public override string Description { get { return "AGES Mk2 audio format"; } }
        public override uint     Signature { get { return 0x00444346; } } // 'FCD\x00'

        public FcdAudio ()
        {
            Extensions = new string[] { "fcd" };
        }

        public override SoundInput TryOpen (IBinaryStream file)
        {
            file.Position = 4;
            // guess: big endian, version=2, type=0 (ogg), offset=0xC
            byte[] data = { 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x4F, 0x67, 0x67, 0x53 };
            if (!file.ReadBytes (0x0C).SequenceEqual (data))
                throw new NotSupportedException();
            return new OggInput (new StreamRegion (file.AsStream, 0x0C));
        }
    }
}
