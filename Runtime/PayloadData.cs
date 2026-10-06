using System;

namespace Shardy {

    /// <summary>
    /// Payload after decode
    /// </summary>
    [Serializable]
    public struct PayloadData {

        /// <summary>
        /// Type of data
        /// </summary>
        public PayloadType Type;

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
        public byte[] Data;

        /// <summary>
        /// Error message or code
        /// </summary>
        public string Error;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="payloadType">Payload type</param>
        /// <param name="payloadName">Command or request name</param>
        /// <param name="requestId">Request id</param>
        /// <param name="payloadData">Payload bytes</param>
        /// <param name="errorMessage">Error message or code</param>
        public PayloadData(PayloadType payloadType, string payloadName, long requestId, byte[] payloadData, string errorMessage) {
            Type = payloadType;
            Id = requestId;
            Name = payloadName;
            Data = payloadData;
            Error = errorMessage;
        }
    }
}
