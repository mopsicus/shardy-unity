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
            public int Id;

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
        /// <param name="body">Target data</param>
        /// <returns>Encoded data</returns>    
        public byte[] Encode(PayloadData body) {
            var dto = new PayloadDTO { Type = (int)body.Type, Name = body.Name, Id = body.Id, Data = body.Data != null ? Convert.ToBase64String(body.Data) : null, Error = body.Error };
            var json = JsonUtility.ToJson(dto);
            return Encoding.UTF8.GetBytes(Utils.ChangeKeysCase(json, false));
        }

        /// <summary>
        /// Deserialize data
        /// </summary>
        /// <param name="body">Encoded data</param>
        /// <returns>Data to use</returns>
        public PayloadData Decode(byte[] body) {
            var json = Encoding.UTF8.GetString(body);
            var dto = JsonUtility.FromJson<PayloadDTO>(Utils.ChangeKeysCase(json, true));
            return new PayloadData((PayloadType)dto.Type, dto.Name, dto.Id, string.IsNullOrEmpty(dto.Data) ? null : Convert.FromBase64String(dto.Data), dto.Error);
        }
    }
}
