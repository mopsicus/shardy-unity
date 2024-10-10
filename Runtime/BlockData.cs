namespace Shardy {

    /// <summary>
    /// Block result after decode
    /// </summary>
    public struct BlockData {

        /// <summary>
        /// Type of block
        /// </summary>
        public BlockType Type;

        /// <summary>
        /// Data length
        /// </summary>
        public int Length;

        /// <summary>
        /// Data bytes array
        /// </summary>
        public byte[] Body;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">Block type</param>
        /// <param name="body">Data for block</param>
        public BlockData(BlockType type, byte[] body) {
            Type = type;
            Length = body.Length;
            Body = body;
        }
    }
}