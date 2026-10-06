using System;

namespace Shardy {

    /// <summary>
    /// Block data for transport
    /// </summary>
    public static class Block {

        /// <summary>
        /// Log tag
        /// </summary>
        const string TAG = "BLOCK";

        /// <summary>
        /// Length of block head
        /// </summary>
        public const int BLOCK_HEAD = 4;

        /// <summary>
        /// Maximum block body size representable by the three-byte length
        /// </summary>
        public const int MAX_BLOCK_SIZE = 0xFFFFFF;

        /// <summary>
        /// Encode block for transporting
        /// 
        /// First byte is type
        /// Next - length, and data
        /// </summary>
        /// <param name="type">Block type: data, kick or heartbeat</param>
        /// <param name="body">Body to send</param>
        /// <returns>Encoded type + body</returns>
        public static byte[] Encode(BlockType blockType, byte[] blockBody) {
            blockBody ??= new byte[0];
            if (blockBody.Length > MAX_BLOCK_SIZE) {
                return null;
            }
            var length = BLOCK_HEAD + blockBody.Length;
            var buffer = new byte[length];
            var index = 0;
            buffer[index++] = Convert.ToByte(blockType);
            buffer[index++] = Convert.ToByte(blockBody.Length >> 16 & 0xFF);
            buffer[index++] = Convert.ToByte(blockBody.Length >> 8 & 0xFF);
            buffer[index++] = Convert.ToByte(blockBody.Length & 0xFF);
            while (index < length) {
                buffer[index] = blockBody[index - BLOCK_HEAD];
                index++;
            }
            return buffer;
        }

        /// <summary>
        /// Decode block data
        /// </summary>
        /// <param name="buffer">Buffer with data to decode</param>
        /// <returns>Result with type as BlockType and body as byte array</returns>
        public static BlockData Decode(byte[] encodedData) {
            if (encodedData == null || encodedData.Length < BLOCK_HEAD) {
#if SHARDY_DEBUG_RAW
                Logger.Error("block frame must contain a complete header", TAG);
#endif                
                return new BlockData(BlockType.Data, null);
            }
            var bodyLength = (encodedData[1] << 16) | (encodedData[2] << 8) | encodedData[3];
            if (bodyLength != encodedData.Length - BLOCK_HEAD) {
#if SHARDY_DEBUG_RAW
                Logger.Error($"block frame length does not match its header: {bodyLength}", TAG);
#endif
                return new BlockData(BlockType.Data, null);
            }
            var type = (BlockType)encodedData[0];
            var body = new byte[bodyLength];
            for (var i = 0; i < body.Length; i++) {
                body[i] = encodedData[i + BLOCK_HEAD];
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

        /// <summary>
        /// Validate a maximum block body size
        /// </summary>
        public static bool Validate(int blockBodySize) {
            return blockBodySize >= 0 && blockBodySize <= MAX_BLOCK_SIZE;
        }
    }
}
