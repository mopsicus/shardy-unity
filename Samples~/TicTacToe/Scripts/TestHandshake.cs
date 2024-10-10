using System.Text;
using Shardy;

/// <summary>
/// Handshake stub
/// </summary>
class TestHandshake : IValidator {

    /// <summary>
    /// Data for acknowledgement
    /// </summary>
    public byte[] Acknowledgement(byte[] body) {
        //
        // generate as you wish here
        //        
        return body;
    }

    /// <summary>
    /// Data for initial handshake
    /// </summary>
    public byte[] Handshake(byte[] body = null) {
        //
        // generate as you wish here
        //        
        return Encoding.UTF8.GetBytes("handshake_data");
    }

    /// <summary>
    /// Verify acknowledgement
    /// </summary>
    public ValidatorState VerifyAcknowledgement(byte[] body) {
        //
        // validate as you wish here
        //        
        return ValidatorState.Success;
    }
}