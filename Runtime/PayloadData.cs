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
        public int Id;

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
        /// <param name="type">Payload type</param>
        /// <param name="name">Command or request name</param>
        /// <param name="id">Request id</param>
        /// <param name="data">Data</param>
        /// <param name="error">Error message or code</param>
        public PayloadData(PayloadType type, string name, int id, byte[] data, string error) {
            Type = type;
            Id = id;
            Name = name;
            Data = data;
            Error = error;
        }
    }
}