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
        /// <param name="payloadType">Type of payload</param>
        /// <param name="payloadName">Command or request name</param>
        /// <param name="requestId">Request id</param>
        /// <param name="payloadData">Payload bytes</param>
        /// <param name="errorMessage">Error message or code</param>
        /// <returns>Encoded data</returns>
        public static byte[] Encode(ISerializer serializer, PayloadType payloadType, string payloadName, long requestId, byte[] payloadData, string errorMessage) {
            return serializer.Encode(new PayloadData(payloadType, payloadName, requestId, payloadData ?? new byte[0], errorMessage ?? string.Empty));
        }

        /// <summary>
        /// Decode received block
        /// </summary>
        /// <param name="serializer">Service serializer</param>
        /// <param name="encodedPayload">Encoded payload bytes</param>
        /// <returns>Payload data to use in commander</returns>
        public static PayloadData Decode(ISerializer serializer, byte[] encodedPayload) {
            return serializer.Decode(encodedPayload);
        }

        /// <summary>
        /// Check payload for available type
        /// </summary>
        /// <param name="payloadData">Payload data to check</param>
        public static bool Check(PayloadData payloadData) {
            return Enum.IsDefined(typeof(PayloadType), payloadData.Type)
                && !string.IsNullOrEmpty(payloadData.Name)
                && payloadData.Id >= 0
                && payloadData.Id <= long.MaxValue
                && payloadData.Data != null
                && payloadData.Error != null;
        }
    }
}
