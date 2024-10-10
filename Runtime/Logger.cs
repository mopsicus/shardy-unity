using System;
#if !UNITY_WEBGL || UNITY_EDITOR
using UnityEngine;
#endif

namespace Shardy {

    /// <summary>
    /// Log colors
    /// </summary>
    public enum LogColor {
        Default,
        Red,
        Orange,
        Yellow,
        Green,
        Blue,
        Purple
    }

    /// <summary>
    /// Custom logger
    /// </summary>
    public class Logger {

        /// <summary>
        /// Date/time format for log
        /// </summary>
        const string DATE_FORMAT = "yyyy-MM-ddTHH:mm:ss.msZ";

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Mask for log
        /// </summary>
        const string COMMON_MASK = "{0} {1}{2}";

        /// <summary>
        /// Mask for label
        /// </summary>    
        const string LABEL_MASK = "{0} [{1}] {2}";
#else
        /// <summary>
        /// Mask for log
        /// </summary>
        const string COMMON_MASK = "{0} <color={1}>{2}</color>{3}";

        /// <summary>
        /// Mask for label
        /// </summary>    
        const string LABEL_MASK = "{0} <color={1}><b>[{2}]</b></color> {3}";
#endif

        /// <summary>
        /// Log info
        /// </summary>
        /// <param name="data">Data to log</param>
        /// <param name="label">Log label</param>
        /// <param name="color">Log color</param>
        public static void Info(object data, string label = "", LogColor color = LogColor.Default) {
#if UNITY_WEBGL && !UNITY_EDITOR
            Console.WriteLine(string.Format(GetMask(label), DateTime.Now.ToString(DATE_FORMAT), label, data));
#else
            Debug.LogFormat(GetMask(label), DateTime.Now.ToString(DATE_FORMAT), GetColor(color), label, data);
#endif
        }

        /// <summary>
        /// Log warning
        /// </summary>
        /// <param name="data">Data to log</param>
        /// <param name="label">Log label</param>
        /// <param name="color">Log color</param>
        public static void Warning(object data, string label = "", LogColor color = LogColor.Yellow) {
#if UNITY_WEBGL && !UNITY_EDITOR
            Console.WriteLine(string.Format(GetMask(label), DateTime.Now.ToString(DATE_FORMAT), label, data));
#else
            Debug.LogWarningFormat(GetMask(label), DateTime.Now.ToString(DATE_FORMAT), GetColor(color), label, data);
#endif
        }

        /// <summary>
        /// Log error
        /// </summary>
        /// <param name="data">Data to log</param>
        /// <param name="label">Log label</param>
        /// <param name="color">Log color</param>
        public static void Error(object data, string label = "", LogColor color = LogColor.Red) {
#if UNITY_WEBGL && !UNITY_EDITOR
            Console.WriteLine(string.Format(GetMask(label), DateTime.Now.ToString(DATE_FORMAT), label, data));
#else
            Debug.LogErrorFormat(GetMask(label), DateTime.Now.ToString(DATE_FORMAT), GetColor(color), label, data);
#endif
        }

        /// <summary>
        /// Get mask for log
        /// </summary>
        /// <param name="label">Label for log</param>
        static string GetMask(string label) {
            return string.IsNullOrEmpty(label) ? COMMON_MASK : LABEL_MASK;
        }

        /// <summary>
        /// Get HEX color for log
        /// </summary>
        /// <param name="color">Color index</param>
        static string GetColor(LogColor color) {
            return color switch {
                LogColor.Red => "#EE4B2B",
                LogColor.Orange => "#FFAC1C",
                LogColor.Yellow => "#FFFF8F",
                LogColor.Green => "#50C878",
                LogColor.Blue => "#89CFF0",
                LogColor.Purple => "#BF40BF",
                _ => "#CCCCCC",
            };
        }
    }
}