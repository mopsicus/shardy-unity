namespace Shardy {

    /// <summary>
    /// Handshake interface for client-server validation
    /// </summary>
    public interface IValidator {

        /// <summary>
        /// Validate acknowledgement data
        /// </summary>
        /// <param name="acknowledgementPayload">Acknowledgement payload to validate</param>
        /// <returns>Validation result</returns>
        ValidatorState VerifyAcknowledgement(byte[] acknowledgementPayload);

        /// <summary>
        /// Get handshake data for send
        /// </summary>
        /// <param name="customHandshakePayload">Optional custom handshake payload</param>
        /// <returns>Data for handshake</returns>
        byte[] Handshake(byte[] customHandshakePayload = null);

        /// <summary>
        /// Get acknowledgement data for send
        /// </summary>
        /// <param name="handshakePayload">Handshake payload to acknowledge</param>
        /// <returns>Data for acknowledge</returns>
        byte[] Acknowledgement(byte[] handshakePayload);
    }
}
