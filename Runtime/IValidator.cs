namespace Shardy {

    /// <summary>
    /// Handshake interface for client-server validation
    /// </summary>
    public interface IValidator {

        /// <summary>
        /// Validate acknowledgement data
        /// </summary>
        /// <param name="body">Data for validate</param>
        /// <returns>Validation result</returns>
        ValidatorState VerifyAcknowledgement(byte[] body);

        /// <summary>
        /// Get handshake data for send
        /// </summary>
        /// <param name="body">Data for handshake</param>
        /// <returns>Data for handshake</returns>
        byte[] Handshake(byte[] body = null);

        /// <summary>
        /// Get acknowledgement data for send
        /// </summary>
        /// <param name="body">Data from handshake</param>
        /// <returns>Data for acknowledge</returns>
        byte[] Acknowledgement(byte[] body);

    }
}