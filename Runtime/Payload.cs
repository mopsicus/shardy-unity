using System;

namespace Shardy {

    /// <summary>
    /// Payload encode and decode block data to use in commander
    /// </summary>
    class Payload {

        /// <summary>
        /// Encode data for transfer
        /// </summary>
        /// <param name="serializer">Service serializer</param>
        /// <param name="type">Type of data</param>
        /// <param name="name">Request name</param>
        /// <param name="id">Request id</param>
        /// <param name="data">Data</param>
        /// <param name="error">Data</param>
        /// <returns>Encoded data</returns>
        public static byte[] Encode(ISerializer serializer, PayloadType type, string name, int id, byte[] data, string error) {
            return serializer.Encode(new PayloadData(type, name, id, data, error));
        }

        /// <summary>
        /// Decode received block
        /// </summary>
        /// <param name="serializer">Service serializer</param>
        /// <param name="data">Encoded  data</param>
        /// <returns>Payload data to use in commander</returns>
        public static PayloadData Decode(ISerializer serializer, byte[] data) {
            return serializer.Decode(data);
        }

        /// <summary>
        /// Check payload for available type
        /// </summary>
        /// <param name="payload">Payload data to check</param>
        public static bool Check(PayloadData payload) {
            return Enum.IsDefined(typeof(PayloadType), payload.Type);
        }
    }
}
