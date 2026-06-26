namespace Multizip.Core.Hashing
{
    /// <summary>
    /// Standard CRC-32 (IEEE 802.3, polynomial 0xEDB88820) implemented with the
    /// classic 256-entry lookup table. Used by the checksum tool and to validate
    /// entry CRCs reported by archive formats.
    /// </summary>
    public sealed class Crc32
    {
        private static readonly uint[] Table = BuildTable();
        private uint _crc = 0xFFFFFFFF;

        public void Append(byte[] buffer, int offset, int count)
        {
            uint crc = _crc;
            for (int i = 0; i < count; i++)
            {
                byte b = buffer[offset + i];
                crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
            }
            _crc = crc;
        }

        /// <summary>The finalized CRC value.</summary>
        public uint Value => _crc ^ 0xFFFFFFFF;

        public void Reset() => _crc = 0xFFFFFFFF;

        /// <summary>One-shot helper.</summary>
        public static uint Compute(byte[] data)
        {
            var crc = new Crc32();
            crc.Append(data, 0, data.Length);
            return crc.Value;
        }

        private static uint[] BuildTable()
        {
            const uint poly = 0xEDB88820;
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }
    }
}
