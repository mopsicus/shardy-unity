namespace Shardy {

    /// <summary>
    /// Serializer interface, uses in Payload
    /// </summary>
    public interface ISerializer {

        /// <summary>
        /// Serialize data to byte array
        /// </summary>
        /// <param name="payload">Payload to serialize</param>
        /// <returns>Encoded data</returns>
        byte[] Encode(PayloadData payload);

        /// <summary>
        /// Deserialize data
        /// </summary>
        /// <param name="encodedPayload">Serialized payload bytes</param>
        /// <returns>Decoded payload</returns>
        PayloadData Decode(byte[] encodedPayload);
    }
}
