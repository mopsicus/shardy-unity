using System;

namespace Shardy {

    /// <summary>
    /// Block data for transport
    /// </summary>
    class Block {

        /// <summary>
        /// Length of block head
        /// </summary>
        public const int HEAD_SIZE = 4;

        /// <summary>
        /// Encode block for transporting
        /// 
        /// First byte is type
        /// Next - length, and data
        /// </summary>
        /// <param name="type">Block type: data, kick or heartbeat</param>
        /// <param name="body">Body to send</param>
        /// <returns>Encoded type + body</returns>
        public static byte[] Encode(BlockType type, byte[] body) {
            var length = (body != null) ? HEAD_SIZE + body.Length : HEAD_SIZE;
            var buffer = new byte[length];
            var index = 0;
            buffer[index++] = Convert.ToByte(type);
            buffer[index++] = Convert.ToByte(body.Length >> 16 & 0xFF);
            buffer[index++] = Convert.ToByte(body.Length >> 8 & 0xFF);
            buffer[index++] = Convert.ToByte(body.Length & 0xFF);
            while (index < length) {
                buffer[index] = body[index - HEAD_SIZE];
                index++;
            }
            return buffer;
        }

        /// <summary>
        /// Decode block data
        /// </summary>
        /// <param name="buffer">Buffer with data to decode</param>
        /// <returns>Result with type as BlockType and body as byte array</returns>
        public static BlockData Decode(byte[] data) {
            var type = (BlockType)data[0];
            var body = new byte[data.Length - HEAD_SIZE];
            for (var i = 0; i < body.Length; i++) {
                body[i] = data[i + HEAD_SIZE];
            }
            return new BlockData(type, body);
        }

        /// <summary>
        /// Check received block
        /// </summary>
        /// <param name="type">Byte index for BlockType</param>
        /// <returns>Is correct block or not</returns>
        public static bool Check(BlockType type) {
            return Enum.IsDefined(typeof(BlockType), type);
        }
    }
}