namespace Shardy {

    /// <summary>
    /// Payload type command
    /// </summary>
    public enum PayloadType {

        /// <summary>
        /// Request and expect answer
        /// </summary>
        Request,

        /// <summary>
        /// Command without answer, event
        /// </summary>
        Command,

        /// <summary>
        /// Response on request
        /// </summary>
        Response
    }
}