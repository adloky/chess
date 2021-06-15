using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessCon
{
    public static class BinaryReaderExtensions {
        private static ulong readUInt64(this BinaryReader reader) {
            int shift = 0;
            ulong result = 0;
            while (shift < 64) {
                var readResult = reader.ReadByte();
                if (readResult < 0) {
                    throw new IOException("EOS.");
                }

                byte _byte = (byte)readResult;

                result |= (ulong)(_byte & 0x7F) << shift;
                if ((_byte & 0x80) == 0) {
                    return result;
                }
                shift += 7;
            }

            throw new FormatException("Varint > 64bit.");
        }

        public static long ReadVarint(this BinaryReader reader) {
            var ul = readUInt64(reader);
            return (long)(ul >> 1) ^ -(long)(ul & 1);
        }

        public static Guid ReadGuid(this BinaryReader reader) {
            var b = reader.ReadBytes(16);
            return new Guid(b);
        }
    }
}
