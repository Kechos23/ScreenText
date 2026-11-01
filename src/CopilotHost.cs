using System;using System.Text.RegularExpressions;
namespace ScreenText {
    // Only public GitHub and single-tenant Enterprise Cloud origins are accepted.
    // Never derive OAuth or credential destinations from arbitrary URLs.
    public static class CopilotHost {
        public static string Normalize(string value){
            value=(value??"").Trim();if(value.StartsWith("https://",StringComparison.OrdinalIgnoreCase))value=value.Substring(8);if(value.EndsWith("/",StringComparison.Ordinal))value=value.Substring(0,value.Length-1);
            value=value.ToLowerInvariant();if(value=="github.com"||Regex.IsMatch(value,@"\A[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.ghe\.com\z"))return value;
            throw new ArgumentException("Use github.com or your company hostname, such as company.ghe.com. Paths, ports and self-hosted servers are not supported.");
        }
    }
}
