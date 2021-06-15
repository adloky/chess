using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessCon
{
    public static class BinaryWriterExtensions {
        private static void writeUInt64(this BinaryWriter writer, ulong ul) {
            while (ul > 0x7F) {
                writer.Write((byte)((ul & 0x7F) | 0x80));
                ul >>= 7;
            }
            writer.Write((byte)ul);
        }

        public static void WriteVarint(this BinaryWriter writer, long l) {
            var ul = (ulong)((l << 1) ^ (l >> 63));
            writeUInt64(writer, ul);
        }

        public static void WriteGuid(this BinaryWriter writer, Guid guid) {
            var b = guid.ToByteArray();
            writer.Write(b);
        }


    }
}
