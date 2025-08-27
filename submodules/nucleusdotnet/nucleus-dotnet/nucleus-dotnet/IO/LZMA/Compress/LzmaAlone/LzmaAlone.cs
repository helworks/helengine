// Source: https://www.7-zip.org/sdk.html   Adapted to follow namespaces and clean code rules
using System;
using System.IO;
namespace Nucleus.IO.LZMA {
    //using CommandLineParser;

    public class CDoubleStream : Stream {
        public System.IO.Stream s1;
        public System.IO.Stream s2;
        public int fileIndex;
        public long skipSize;

        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override long Length { get { return s1.Length + s2.Length - skipSize; } }
        public override long Position {
            get { return 0; }
            set { }
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) {
            int numTotal = 0;
            while (count > 0) {
                if (fileIndex == 0) {
                    int num = s1.Read(buffer, offset, count);
                    offset += num;
                    count -= num;
                    numTotal += num;
                    if (num == 0)
                        fileIndex++;
                }
                if (fileIndex == 1) {
                    numTotal += s2.Read(buffer, offset, count);
                    return numTotal;
                }
            }
            return numTotal;
        }
        public override void Write(byte[] buffer, int offset, int count) {
            throw (new Exception("can't Write"));
        }
        public override long Seek(long offset, System.IO.SeekOrigin origin) {
            throw (new Exception("can't Seek"));
        }
        public override void SetLength(long value) {
            throw (new Exception("can't SetLength"));
        }
    }

    class LzmaAlone {
        enum Key {
            Help1 = 0,
            Help2,
            Mode,
            Dictionary,
            FastBytes,
            LitContext,
            LitPos,
            PosBits,
            MatchFinder,
            EOS,
            StdIn,
            StdOut,
            Train
        };

        static void PrintHelp() {
#if !WINRT
            System.Console.WriteLine("\nUsage:  LZMA <e|d> [<switches>...] inputFile outputFile\n" +
                "  e: encode file\n" +
                "  d: decode file\n" +
                "  b: Benchmark\n" +
                "<Switches>\n" +
                // "  -a{N}:  set compression mode - [0, 1], default: 1 (max)\n" +
                "  -d{N}:  set dictionary - [0, 29], default: 23 (8MB)\n" +
                "  -fb{N}: set number of fast bytes - [5, 273], default: 128\n" +
                "  -lc{N}: set number of literal context bits - [0, 8], default: 3\n" +
                "  -lp{N}: set number of literal pos bits - [0, 4], default: 0\n" +
                "  -pb{N}: set number of pos bits - [0, 4], default: 2\n" +
                "  -mf{MF_ID}: set Match Finder: [bt2, bt4], default: bt4\n" +
                "  -eos:   write End Of Stream marker\n"
                // + "  -si:    read data from stdin\n"
                // + "  -so:    write data to stdout\n"
                );
#endif
        }

        static bool GetNumber(string s, out Int32 v) {
            v = 0;
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];
                if (c < '0' || c > '9')
                    return false;
                v *= 10;
                v += c - '0';
            }
            return true;
        }

        static int IncorrectCommand() {
            throw (new Exception("Command line error"));
            // System.Console.WriteLine("\nCommand line error\n");
            // return 1;
        }
    }
}
