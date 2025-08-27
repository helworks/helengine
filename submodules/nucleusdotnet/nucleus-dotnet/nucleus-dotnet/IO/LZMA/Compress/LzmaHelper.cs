using System;
using System.IO;

namespace Nucleus.IO.LZMA {
    public static class LzmaHelper {
        private static readonly int dictionary = 1 << 23;
        private static readonly bool eos = false;

        // << 21 in case of low ram
        // static Int32 posStateBits = 2;
        // static  Int32 litContextBits = 3; // for normal files
        // UInt32 litContextBits = 0; // for 32-bit data
        // static  Int32 litPosBits = 0;
        // UInt32 litPosBits = 2; // for 32-bit data
        // static   Int32 algorithm = 2;
        // static    Int32 numFastBytes = 128;

        private static readonly CoderPropID[] propIDs =
                {
                    CoderPropID.DictionarySize,
                    CoderPropID.PosStateBits,
                    CoderPropID.LitContextBits,
                    CoderPropID.LitPosBits,
                    CoderPropID.Algorithm,
                    CoderPropID.NumFastBytes,
                    CoderPropID.MatchFinder,
                    CoderPropID.EndMarker
                };

        // these are the default properties, keeping it simple for now:
        private static readonly object[] properties =
                {
                    dictionary,
                    2,
                    3,
                    0,
                    2,
                    128,
                    "bt4",
                    eos
                };


        public static byte[] Compress(byte[] inputBytes) {
            using (MemoryStream inStream = new MemoryStream(inputBytes)) {
                using (MemoryStream outStream = new MemoryStream()) {
                    Compress(inStream, outStream);
                    return outStream.ToArray();
                }
            }
        }

        public static void Compress(Stream inStream, Stream outStream) {
            BinaryWriter writer = new BinaryWriter(outStream);
            //writer.WriteNucleusHeader();
            //writer.WriteNucleusLZMAHeader();
            //writer.Flush();

            LzmaEncoder encoder = new LzmaEncoder();
            encoder.SetCoderProperties(propIDs, properties);
            encoder.WriteCoderProperties(outStream);
            long fileSize = inStream.Length;
            for (int i = 0; i < 8; i++) {
                outStream.WriteByte((byte)(fileSize >> (8 * i)));
            }

            encoder.Code(inStream, outStream, -1, -1, null);
        }

        public static void Decompress(Stream inStream, Stream outStream) {
            BinaryReader reader = new BinaryReader(inStream);
            //reader.ReadNucleusHeader();
            //reader.ReadNucleusLZMAHeader();

            LzmaDecoder decoder = new LzmaDecoder();
            byte[] properties2 = new byte[5];
            if (inStream.Read(properties2, 0, 5) != 5) {
                throw new Exception("input .lzma is too short");
            }

            long outSize = 0;
            for (int i = 0; i < 8; i++) {
                int v = inStream.ReadByte();
                if (v < 0)
                    throw (new Exception("Can't Read 1"));
                outSize |= ((long)(byte)v) << (8 * i);
            }
            decoder.SetDecoderProperties(properties2);

            long compressedSize = inStream.Length - inStream.Position;
            decoder.Code(inStream, outStream, compressedSize, outSize, null);
        }

        public static byte[] Decompress(byte[] inputBytes) {
            using (MemoryStream inStream = new MemoryStream(inputBytes)) {
                using (MemoryStream outStream = new MemoryStream()) {
                    Decompress(inStream, outStream);
                    return outStream.ToArray();
                }
            }
        }
    }
}
