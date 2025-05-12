using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shardy {

    /// <summary>
    /// Some usefull functions
    /// </summary>
    public static class Utils {

        /// <summary>
        /// Change keys first letter case in JSON string
        /// </summary>
        /// <param name="json">Json darta</param>
        /// <param name="isToUpper">Is uppercase or lowercase</param>
        /// <returns>Modified JSON</returns>
        public static string ChangeKeysCase(string json, bool isToUpper) {
            if (string.IsNullOrWhiteSpace(json)) {
                return json;
            }
            try {
                json = json.Trim();
                if (!json.StartsWith("{") || !json.EndsWith("}")) {
                    return json;
                }
                var content = json[1..^1].Trim();
                if (string.IsNullOrEmpty(content)) {
                    return "{}";
                }
                var result = new StringBuilder("{");
                var braceLevel = 0;
                var squareBracketLevel = 0;
                var isInQuotes = false;
                var isReadingKey = true;
                var currentKey = string.Empty;
                for (var i = 0; i < content.Length; i++) {
                    var c = content[i];
                    if (c == '"' && (i == 0 || content[i - 1] != '\\')) {
                        isInQuotes = !isInQuotes;
                        if (isReadingKey) {
                            currentKey += c;
                        } else {
                            result.Append(c);
                        }
                        continue;
                    }
                    if (!isInQuotes) {
                        if (c == '{') {
                            braceLevel++;
                        } else if (c == '}') {
                            braceLevel--;
                        } else if (c == '[') {
                            squareBracketLevel++;
                        } else if (c == ']') {
                            squareBracketLevel--;
                        }
                    }
                    if (isReadingKey && isInQuotes) {
                        currentKey += c;
                    } else if (isReadingKey && !isInQuotes && c == ':') {
                        var keyWithoutQuotes = currentKey[1..^1];
                        var modifiedKey = keyWithoutQuotes.Length > 0 ? (isToUpper ? char.ToUpperInvariant(keyWithoutQuotes[0]) + keyWithoutQuotes[1..] : char.ToLowerInvariant(keyWithoutQuotes[0]) + keyWithoutQuotes[1..]) : keyWithoutQuotes;
                        result.Append($"\"{modifiedKey}\":");
                        isReadingKey = false;
                        currentKey = "";
                        continue;
                    } else if (!isReadingKey && !isInQuotes && braceLevel == 0 && squareBracketLevel == 0 && c == ',') {
                        isReadingKey = true;
                        result.Append(c);
                        continue;
                    } else if (!isReadingKey) {
                        result.Append(c);
                    }
                }
                result.Append("}");
                return result.ToString();
            } catch {
                return json;
            }
        }

        /// <summary>
        /// Custom timer for WebGL compatibility
        /// </summary>
        /// <param name="delay">Interval for timer, ms</param>
        /// <param name="cancellation">Cancellation source</param>
        /// <param name="callback">Callback on tick</param>
        public static async Task SetTimer(float interval, CancellationTokenSource cancellation, Action callback) {
            while (!cancellation.IsCancellationRequested) {
                await SetDelay(interval);
                callback();
            }
        }

        /// <summary>
        /// Make delay
        /// Hack to prevent using Task.Delay in WebGL
        /// </summary>
        /// <param name="delay">Delay in ms</param>
        public static async Task SetDelay(float delay) {
            var target = DateTime.Now.AddMilliseconds(delay);
            while (DateTime.Now < target) {
                await Task.Yield();
            }
        }

        /// <summary>
        /// Extension to add cancellation for task
        /// </summary>
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken) {
            var source = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(state => ((TaskCompletionSource<bool>)state).TrySetResult(true), source)) {
                if (task != await Task.WhenAny(task, source.Task)) {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
            return task.Result;
        }

        /// <summary>
        /// Extension to add cancellation for task
        /// </summary>
        public static async Task WithCancellation(this Task task, CancellationToken cancellationToken) {
            var source = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(state => ((TaskCompletionSource<bool>)state).TrySetResult(true), source)) {
                if (task != await Task.WhenAny(task, source.Task).ConfigureAwait(false)) {
                    throw new OperationCanceledException(cancellationToken);
                }
                await task;
            }
        }

        /// <summary>
        /// Convert data to string
        /// </summary>
        public static string DataToString(byte[] data) {
            return (data == null) ? string.Empty : Encoding.UTF8.GetString(data);
        }

#if SHARDY_DEBUG || SHARDY_DEBUG_RAW

        /// <summary>
        /// Convert data to log string
        /// </summary>
        public static string DataToDebug(byte[] data) {
            return (data == null) ? string.Empty : ToLiteral(Encoding.UTF8.GetString(data));
        }

        /// <summary>
        /// Escape string for log
        /// </summary>
        /// <param name="data">Input string</param>
        static string ToLiteral(string data) {
            var literal = new StringBuilder(data.Length);
            foreach (var c in data) {
                switch (c) {
                    case '\"':
                        literal.Append("\\\"");
                        break;
                    case '\\':
                        literal.Append(@"\\");
                        break;
                    case '\0':
                        literal.Append(@"\0");
                        break;
                    case '\a':
                        literal.Append(@"\a");
                        break;
                    case '\b':
                        literal.Append(@"\b");
                        break;
                    case '\f':
                        literal.Append(@"\f");
                        break;
                    case '\n':
                        literal.Append(@"\n");
                        break;
                    case '\r':
                        literal.Append(@"\r");
                        break;
                    case '\t':
                        literal.Append(@"\t");
                        break;
                    case '\v':
                        literal.Append(@"\v");
                        break;
                    default:
                        if (c >= 0x20 && c <= 0x7e) {
                            literal.Append(c);
                        } else {
                            literal.Append(@"\u");
                            literal.Append(((int)c).ToString("x4"));
                        }
                        break;
                }
            }
            return literal.ToString();
        }

#endif

    }
}
