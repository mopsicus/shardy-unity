using System;
using System.Text;
using UnityEngine;

namespace Shardy {

    /// <summary>
    /// Default serializer
    /// </summary>
    public class DefaultSerializer : ISerializer {

        /// <summary>
        /// DTO for serialization
        /// </summary>
        [Serializable]
        class PayloadDTO {

            /// <summary>
            /// Type of data
            /// </summary>        
            public int Type;

            /// <summary>
            /// Command or request name
            /// </summary>        
            public string Name;

            /// <summary>
            /// Request id
            /// </summary>        
            public long Id;

            /// <summary>
            /// Data
            /// </summary>
            public string Data;

            /// <summary>
            /// Error message or code
            /// </summary>        
            public string Error;
        }

        /// <summary>
        /// Serialize data to byte array
        /// </summary>
        /// <param name="payload">Payload to serialize</param>
        /// <returns>Encoded data</returns>    
        public byte[] Encode(PayloadData payload) {
            var dto = new PayloadDTO { Type = (int)payload.Type, Name = payload.Name, Id = payload.Id, Data = Convert.ToBase64String(payload.Data ?? new byte[0]), Error = payload.Error ?? string.Empty };
            var json = JsonUtility.ToJson(dto);
            return Encoding.UTF8.GetBytes(Utils.ChangeKeysCase(json, false));
        }

        /// <summary>
        /// Deserialize data
        /// </summary>
        /// <param name="encodedPayload">Serialized payload bytes</param>
        /// <returns>Decoded payload</returns>
        public PayloadData Decode(byte[] encodedPayload) {
            var json = Encoding.UTF8.GetString(encodedPayload);
            var dto = JsonUtility.FromJson<PayloadDTO>(Utils.ChangeKeysCase(json, true));
            if (dto == null || dto.Name == null || dto.Data == null || dto.Error == null) {
                throw new FormatException("payload must contain type, name, id, data, and error fields");
            }
            return new PayloadData((PayloadType)dto.Type, dto.Name, dto.Id, Convert.FromBase64String(dto.Data), dto.Error);
        }
    }
}
