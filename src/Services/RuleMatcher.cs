using System.Text;
using System.Text.RegularExpressions;
using UrlRouter.Models;

namespace UrlRouter.Services
{
    /// <summary>Outcome of evaluating the rule list against one URL.</summary>
    /// <param name="Rule">The rule that matched, or null when the fallback/picker applies.</param>
    /// <param name="Target">Where to open, or null when the picker should be shown.</param>
    /// <param name="UsedFallback">True when no rule matched and a configured fallback was used.</param>
    public record MatchResult(RoutingRule? Rule, BrowserTarget? Target, bool UsedFallback)
    {
        public bool NeedsPicker => Target is null;
    }

    /// <summary>
    /// Pure rule evaluation - no I/O, no side effects, so it can be exercised directly by
    /// the --test switch and the config UI's Test tab.
    /// </summary>
    public static class RuleMatcher
    {
        public static MatchResult Match(RouterConfig config, Uri uri)
        {
            foreach (var rule in config.Rules)
            {
                if (!rule.Enabled) continue;
                if (!Matches(rule, uri)) continue;

                // A rule pointing at a deleted target falls through rather than dead-ending.
                var target = config.FindTarget(rule.TargetId);
                if (target is null) continue;

                return new MatchResult(rule, target, UsedFallback: false);
            }

            var fallback = config.FindTarget(config.FallbackTargetId);
            return new MatchResult(null, fallback, UsedFallback: fallback is not null);
        }

        public static bool Matches(RoutingRule rule, Uri uri)
        {
            if (string.IsNullOrWhiteSpace(rule.HostPattern)) return false;
            if (!GlobRegex(rule.HostPattern, isHost: true).IsMatch(uri.Host)) return false;

            if (string.IsNullOrWhiteSpace(rule.PathPattern)) return true;

            // Only look at the query when the pattern actually mentions one, so the common
            // "/browse/ISD-*" case is not defeated by a tracking parameter on the URL.
            var subject = rule.PathPattern.Contains('?') ? uri.PathAndQuery : uri.AbsolutePath;
            return GlobRegex(rule.PathPattern, isHost: false).IsMatch(subject);
        }

        private static readonly Dictionary<string, Regex> RegexCache = new();

        /// <summary>
        /// Translates a glob to an anchored, case-insensitive regex. '*' is the only
        /// wildcard - '?' is left literal because it is meaningful inside a URL.
        ///
        /// Host patterns get one extra affordance: a leading "*." also matches the bare
        /// domain, so "*.gmail.com.au" covers both sso.gmail.com and gmail.com.au.
        /// Anchoring is what stops "*.gmail.com.au" matching "gmail.com.example.org".
        /// </summary>
        private static Regex GlobRegex(string pattern, bool isHost)
        {
            var key = (isHost ? "h:" : "p:") + pattern;
            if (RegexCache.TryGetValue(key, out var cached)) return cached;

            var body = new StringBuilder();
            var rest = pattern;

            if (isHost && rest.StartsWith("*.", StringComparison.Ordinal))
            {
                body.Append("(?:.*\\.)?");
                rest = rest[2..];
            }

            foreach (var c in rest)
            {
                body.Append(c == '*' ? ".*" : Regex.Escape(c.ToString()));
            }

            var regex = new Regex("^" + body + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            RegexCache[key] = regex;
            return regex;
        }
    }
}
