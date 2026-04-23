using System.Text.RegularExpressions;

namespace ASXRunTerminal.Core;

internal static class SecretMasker
{
    private const string MaskValue = "***";

    private static readonly RegexOptions CommonRegexOptions =
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private static readonly Regex SensitiveKeyValueRegex = new(
        @"(?<prefix>\b(?:password|passwd|pwd|secret|token|api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret)\b\s*[""']?\s*[:=]\s*)(?<quote>[""']?)(?<value>[^""'\s,;]+)(?<suffix>\k<quote>)",
        CommonRegexOptions);

    private static readonly Regex AuthorizationHeaderRegex = new(
        @"(?<prefix>\bAuthorization\s*:\s*(?:Bearer|Basic)\s+)(?<value>[^,\s;]+)",
        CommonRegexOptions);

    private static readonly Regex BearerTokenRegex = new(
        @"(?<prefix>\bBearer\s+)(?<value>[A-Za-z0-9._~+/\-=]+)",
        CommonRegexOptions);

    private static readonly Regex CommandLineSecretFlagRegex = new(
        @"(?<prefix>(?:^|(?<=\s))--?(?:password|passwd|pwd|secret|token|api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|bearer-token)(?:\s+|=))(?<quote>[""']?)(?<value>[^""'\s]+)(?<suffix>\k<quote>)",
        CommonRegexOptions);

    private static readonly Regex QueryStringSecretRegex = new(
        @"(?<prefix>[?&](?:access[_-]?token|token|api[_-]?key|secret|password|passwd)=)(?<value>[^&#\s]+)",
        CommonRegexOptions);

    private static readonly Regex UrlUserInfoRegex = new(
        @"(?<prefix>\bhttps?://)(?<value>[^/@\s]+)(?<suffix>@)",
        CommonRegexOptions);

    private static readonly Regex JwtRegex = new(
        @"\beyJ[A-Za-z0-9_-]{6,}\.[A-Za-z0-9_-]{6,}\.[A-Za-z0-9_-]{6,}\b",
        CommonRegexOptions);

    private static readonly Regex OpenAiApiKeyRegex = new(
        @"\bsk-[A-Za-z0-9_-]{12,}\b",
        CommonRegexOptions);

    private static readonly Regex GitHubTokenRegex = new(
        @"\b(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{20,}\b|\bgithub_pat_[A-Za-z0-9_]{20,}\b",
        CommonRegexOptions);

    private static readonly Regex PrivateKeyBlockRegex = new(
        @"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var sanitized = value;
        sanitized = ApplyValueMask(sanitized, SensitiveKeyValueRegex);
        sanitized = ApplyValueMask(sanitized, AuthorizationHeaderRegex);
        sanitized = ApplyValueMask(sanitized, BearerTokenRegex);
        sanitized = ApplyValueMask(sanitized, CommandLineSecretFlagRegex);
        sanitized = ApplyValueMask(sanitized, QueryStringSecretRegex);
        sanitized = ApplyValueMask(sanitized, UrlUserInfoRegex);
        sanitized = JwtRegex.Replace(sanitized, MaskValue);
        sanitized = OpenAiApiKeyRegex.Replace(sanitized, MaskValue);
        sanitized = GitHubTokenRegex.Replace(sanitized, MaskValue);
        sanitized = PrivateKeyBlockRegex.Replace(sanitized, "[REDACTED PRIVATE KEY]");

        return sanitized;
    }

    private static string ApplyValueMask(string value, Regex regex)
    {
        return regex.Replace(value, static match =>
        {
            if (!match.Groups["value"].Success)
            {
                return match.Value;
            }

            var capturedValue = match.Groups["value"].Value;
            if (!ShouldMask(capturedValue))
            {
                return match.Value;
            }

            var prefix = match.Groups["prefix"].Success
                ? match.Groups["prefix"].Value
                : string.Empty;
            var quote = match.Groups["quote"].Success
                ? match.Groups["quote"].Value
                : string.Empty;
            var suffix = match.Groups["suffix"].Success
                ? match.Groups["suffix"].Value
                : string.Empty;
            return $"{prefix}{quote}{MaskValue}{suffix}";
        });
    }

    private static bool ShouldMask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, MaskValue, StringComparison.Ordinal))
        {
            return false;
        }

        var isPlaceholder = trimmed.Length >= 2
            && ((trimmed.StartsWith('<') && trimmed.EndsWith('>'))
                || (trimmed.StartsWith('{') && trimmed.EndsWith('}')));

        return !isPlaceholder;
    }
}
