using System;
using System.Text;
using UnityEngine;

namespace Shardy {

    /// <summary>
    /// Default serializer
    /// </summary>
    public class DefaultValidator : IValidator {

        /// <summary>
        /// DTO for handshake
        /// </summary>
        [Serializable]
        class HandshakeDTO {

            /// <summary>
            /// Handshake version
            /// </summary>
            public int Version;

            /// <summary>
            /// Timestamp of handshake
            /// </summary>
            public long Timestamp;

            /// <summary>
            /// Nonce for handshake
            /// </summary>
            public string Nonce;

            /// <summary>
            /// Custom data for handshake
            /// </summary>
            public string Payload;
        }

        /// <summary>
        /// DTO for acknowledgement
        /// </summary>
        [Serializable]
        class AcknowledgementDTO {

            /// <summary>
            /// Received flag
            /// </summary>
            public bool Received;

            /// <summary>
            /// Nonce for acknowledgement
            /// </summary>
            public string Nonce;
        }

        /// <summary>
        /// Get handshake data for send
        /// </summary>
        /// <param name="body">Data for handshake</param>
        /// <returns>Data for handshake</returns>
        public byte[] Handshake(byte[] body = null) {
            var handshake = new HandshakeDTO { Version = 1, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Nonce = Guid.NewGuid().ToString("N"), Payload = body != null ? Encoding.UTF8.GetString(body) : null };
            var json = JsonUtility.ToJson(handshake);
            return Encoding.UTF8.GetBytes(Utils.ChangeKeysCase(json, false));
        }

        /// <summary>
        /// Get acknowledgement data for send
        /// </summary>
        /// <param name="body">Data from handshake</param>
        /// <returns>Data for acknowledge</returns>
        public byte[] Acknowledgement(byte[] body) {
            var json = Encoding.UTF8.GetString(body);
            var handshake = JsonUtility.FromJson<HandshakeDTO>(Utils.ChangeKeysCase(json, true));
            var ack = new AcknowledgementDTO { Received = true, Nonce = handshake.Nonce };
            return Encoding.UTF8.GetBytes(Utils.ChangeKeysCase(JsonUtility.ToJson(ack), false));
        }

        /// <summary>
        /// Validate acknowledgement data
        /// </summary>
        /// <param name="body">Data for validate</param>
        /// <returns>Validation result</returns>
        public ValidatorState VerifyAcknowledgement(byte[] body) {
            var json = Encoding.UTF8.GetString(body);
            var ack = JsonUtility.FromJson<AcknowledgementDTO>(Utils.ChangeKeysCase(json, true));
            if (!string.IsNullOrEmpty(ack.Nonce)) {
                return ValidatorState.Success;
            }
            return ValidatorState.Failed;
        }
    }
}
