using NLog;
using NLog.Filters;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ccf.Ck.Libs.Logging
{
    [Filter("ErrorDeduplicationFilter")]
    public class ErrorDeduplicationFilter : Filter
    {
        private static readonly HashSet<string> _RecentErrorSignatures = new HashSet<string>();

        protected override FilterResult Check(LogEventInfo logEvent)
        {
            var signature = GetErrorSignature(logEvent);

            lock (_RecentErrorSignatures)
            {
                if (_RecentErrorSignatures.Contains(signature))
                {
                    return FilterResult.Ignore;
                }

                _RecentErrorSignatures.Add(signature);
                if (_RecentErrorSignatures.Count > 100) // Keep the list small
                {
                    _RecentErrorSignatures.Remove(_RecentErrorSignatures.First());
                }
            }

            return FilterResult.Log;
        }

        private string GetErrorSignature(LogEventInfo logEvent)
        {
            using (var sha256 = SHA256.Create())
            {
                // Combine the exception (if any) and the message into a single string
                var input = (logEvent.Exception?.ToString() ?? logEvent.Message) + logEvent.LoggerName + logEvent.Level;

                // Convert the input string to a byte array
                var inputBytes = Encoding.UTF8.GetBytes(input);

                // Compute the hash
                var hashBytes = sha256.ComputeHash(inputBytes);

                // Convert the hash bytes to a hexadecimal string
                var hashString = new StringBuilder();
                foreach (var b in hashBytes)
                {
                    hashString.Append(b.ToString("x2"));
                }

                return hashString.ToString();
            }
        }
    }
}
