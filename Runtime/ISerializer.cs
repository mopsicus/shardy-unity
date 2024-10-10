namespace Shardy {

    /// <summary>
    /// Serializer interface, uses in Payload
    /// </summary>
    public interface ISerializer {

        /// <summary>
        /// Serialize data to byte array
        /// </summary>
        /// <param name="body">Target data</param>
        /// <returns>Encoded data</returns>
        byte[] Encode(PayloadData body);

        /// <summary>
        /// Deserialize data
        /// </summary>
        /// <param name="body">Encoded data</param>
        /// <returns>Data to use</returns>
        PayloadData Decode(byte[] body);

    }
}