namespace UrlRouter.Services
{
    /// <summary>
    /// Result of normalizing a clicked URL.
    /// </summary>
    /// <param name="Original">Exactly what the shell handed us.</param>
    /// <param name="Normalized">What rules are matched against, and what the browser is given.</param>
    /// <param name="UnwrapCount">How many redirect wrappers were peeled off.</param>
    public record NormalizedUrl(string Original, string Normalized, int UnwrapCount)
    {
        public bool WasWrapped => UnwrapCount > 0;
    }

    /// <summary>
    /// Peels Outlook/Teams link-protection wrappers off a URL before routing.
    ///
    /// This is load-bearing rather than cosmetic. Microsoft Defender Safe Links rewrites
    /// every link in email and Teams chat to
    /// https://{tenant}.safelinks.protection.outlook.com/?url={encoded real url}&amp;data=...
    /// so without unwrapping, every link clicked in Outlook or Teams presents the *same*
    /// host to the rule matcher and no routing rule could ever distinguish them.
    /// </summary>
    public static class UrlNormalizer
    {
        private const int MaxUnwraps = 5;

        /// <summary>Query parameter that carries the real destination, per wrapper family.</summary>
        private const string DestinationParameter = "url";

        public static NormalizedUrl Normalize(string url, bool unwrapSafeLinks = true)
        {
            var current = (url ?? string.Empty).Trim();
            var unwraps = 0;

            if (unwrapSafeLinks)
            {
                for (var i = 0; i < MaxUnwraps; i++)
                {
                    var inner = TryUnwrap(current);
                    if (inner is null) break;

                    current = inner;
                    unwraps++;
                }
            }

            return new NormalizedUrl(url ?? string.Empty, current, unwraps);
        }

        /// <summary>
        /// Returns the wrapped destination if <paramref name="url"/> is a known redirector,
        /// otherwise null.
        /// </summary>
        private static string? TryUnwrap(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            if (!IsWrapper(uri)) return null;

            var destination = GetQueryParameter(uri.Query, DestinationParameter);
            if (string.IsNullOrWhiteSpace(destination)) return null;

            // The wrapped value is percent-encoded. Decode once; a doubly-wrapped link is
            // handled by the caller's loop rather than by decoding repeatedly here, which
            // would corrupt URLs that legitimately contain an encoded '%'.
            var decoded = Uri.UnescapeDataString(destination);

            // Only accept an absolute http(s) destination - anything else means we
            // misread the wrapper and should leave the original URL alone.
            if (!Uri.TryCreate(decoded, UriKind.Absolute, out var inner)) return null;
            if (inner.Scheme != Uri.UriSchemeHttp && inner.Scheme != Uri.UriSchemeHttps) return null;

            return decoded;
        }

        /// <summary>
        /// Known link-protection wrappers. Deliberately limited to the Microsoft families
        /// that Outlook and Teams actually produce; add entries here as new ones appear.
        /// </summary>
        private static bool IsWrapper(Uri uri)
        {
            var host = uri.Host;

            // apc01.safelinks.protection.outlook.com, nam02.safelinks..., etc.
            if (host.EndsWith(".safelinks.protection.outlook.com", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("safelinks.protection.outlook.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Teams renders its own Safe Links interstitial from the Office CDN.
            if (host.Equals("statics.teams.cdn.office.net", StringComparison.OrdinalIgnoreCase) &&
                uri.AbsolutePath.Contains("safelinks", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Minimal query parser. Avoids System.Web/HttpUtility so the routing hot path does
        /// not drag in another assembly. '+' is left alone: Safe Links percent-encodes
        /// everything, and treating '+' as a space would corrupt real URLs that contain one.
        /// </summary>
        private static string? GetQueryParameter(string query, string name)
        {
            if (string.IsNullOrEmpty(query)) return null;

            var span = query.AsSpan(query[0] == '?' ? 1 : 0);

            while (!span.IsEmpty)
            {
                var amp = span.IndexOf('&');
                var pair = amp < 0 ? span : span[..amp];
                span = amp < 0 ? default : span[(amp + 1)..];

                var eq = pair.IndexOf('=');
                if (eq < 0) continue;

                if (pair[..eq].Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair[(eq + 1)..].ToString();
                }
            }

            return null;
        }
    }
}
