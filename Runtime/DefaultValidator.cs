using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Shardy {

    /// <summary>
    /// Default validator
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
            public bool IsReceived;

            /// <summary>
            /// Nonce for acknowledgement
            /// </summary>
            public string Nonce;
        }

        /// <summary>
        /// Get handshake data for send
        /// </summary>
        /// <param name="customHandshakePayload">Optional custom handshake payload</param>
        /// <returns>Data for handshake</returns>
        public byte[] Handshake(byte[] customHandshakePayload = null) {
            var handshake = new HandshakeDTO { Version = 1, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Nonce = Guid.NewGuid().ToString("N"), Payload = customHandshakePayload != null ? Encoding.UTF8.GetString(customHandshakePayload) : null };
            var json = JsonUtility.ToJson(handshake);
            return Encoding.UTF8.GetBytes(Utils.ChangeKeysCase(json, false));
        }

        /// <summary>
        /// Get acknowledgement data for send
        /// </summary>
        /// <param name="handshakePayload">Handshake payload to acknowledge</param>
        /// <returns>Data for acknowledge</returns>
        public byte[] Acknowledgement(byte[] handshakePayload) {
            var json = Encoding.UTF8.GetString(handshakePayload);
            var handshake = JsonUtility.FromJson<HandshakeDTO>(Utils.ChangeKeysCase(json, true));
            var ack = new AcknowledgementDTO { IsReceived = true, Nonce = handshake.Nonce };
            // Preserve the wire-format key while following boolean field naming conventions.
            var acknowledgementJson = RenameJsonKey(JsonUtility.ToJson(ack), "IsReceived", "Received");
            return Encoding.UTF8.GetBytes(Utils.ChangeKeysCase(acknowledgementJson, false));
        }

        /// <summary>
        /// Validate acknowledgement data
        /// </summary>
        /// <param name="acknowledgementPayload">Acknowledgement payload to validate</param>
        /// <returns>Validation result</returns>
        public ValidatorState VerifyAcknowledgement(byte[] acknowledgementPayload) {
            try {
                var json = Encoding.UTF8.GetString(acknowledgementPayload);
                json = RenameJsonKey(Utils.ChangeKeysCase(json, true), "Received", "IsReceived");
                var ack = JsonUtility.FromJson<AcknowledgementDTO>(json);
                if (ack != null && ack.IsReceived && !string.IsNullOrEmpty(ack.Nonce)) {
                    return ValidatorState.Success;
                }
            } catch (Exception) {
            }
            return ValidatorState.Failed;
        }

        /// <summary>
        /// Rename a key in a JSON string
        /// </summary>
        /// <param name="json">The JSON string in which to rename the key</param>
        /// <param name="oldName">The old key name to be replaced</param>
        /// <param name="newName">The new key name to replace the old key</param>
        /// <returns>The JSON string with the key renamed</returns>
        static string RenameJsonKey(string json, string oldName, string newName) {
            var pattern = "(^|[,{]\\s*)\"" + Regex.Escape(oldName) + "\"(\\s*:)";
            return Regex.Replace(json, pattern, "$1\"" + newName + "\"$2");
        }
    }
}
