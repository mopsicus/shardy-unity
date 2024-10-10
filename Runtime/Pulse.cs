using System;
using System.Threading;

#pragma warning disable 4014

namespace Shardy {

    /// <summary>
    /// Pulse service for support connection
    /// </summary>
    class Pulse {

        /// <summary>
        /// Callback
        /// </summary>
        public Action OnPulse = delegate { };

        /// <summary>
        /// Cancellation token source
        /// </summary>
        readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        /// <summary>
        /// Checks counter
        /// </summary>
        int _checks = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="interval">Interval to check pulse</param>
        public Pulse(float interval) {
            Utils.SetTimer(interval, _cancellation, () => CheckPulse());
        }

        /// <summary>
        /// Pulse checker
        /// </summary>
        public void CheckPulse() {
            _checks++;
            if (_checks > 1) {
                Reset();
                OnPulse();
            }
        }

        /// <summary>
        /// Reset timer when commands received
        /// </summary>
        public void Reset() {
            _checks = 0;
        }

        /// <summary>
        /// Stop and switch off service
        /// </summary>
        public void Clear() {
            Reset();
            _cancellation.Cancel();
        }
    }
}