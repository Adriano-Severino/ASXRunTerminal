using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class SecretMaskerTests
{
    [Fact]
    public void Mask_ReplacesSensitiveKeyValuePatterns()
    {
        var input =
            "password=abc123 token:xyz Authorization: Bearer secret-token ?api_key=qwerty --bearer-token <token>";

        var masked = SecretMasker.Mask(input);

        Assert.DoesNotContain("abc123", masked, StringComparison.Ordinal);
        Assert.DoesNotContain("xyz", masked, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", masked, StringComparison.Ordinal);
        Assert.DoesNotContain("qwerty", masked, StringComparison.Ordinal);
        Assert.Contains("password=***", masked, StringComparison.Ordinal);
        Assert.Contains("token:***", masked, StringComparison.Ordinal);
        Assert.Contains("Authorization: Bearer ***", masked, StringComparison.Ordinal);
        Assert.Contains("?api_key=***", masked, StringComparison.Ordinal);
        Assert.Contains("--bearer-token <token>", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void Mask_ReplacesCommonTokenFormats()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.c2lnbmF0dXJl";
        const string openAiKey = "sk-proj-ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
        const string githubToken = "ghp_123456789012345678901234567890123456";
        const string privateKeyBlock =
            "-----BEGIN PRIVATE KEY-----\nZXhhbXBsZQ==\n-----END PRIVATE KEY-----";
        var input = $"{jwt}\n{openAiKey}\n{githubToken}\n{privateKeyBlock}";

        var masked = SecretMasker.Mask(input);

        Assert.DoesNotContain(jwt, masked, StringComparison.Ordinal);
        Assert.DoesNotContain(openAiKey, masked, StringComparison.Ordinal);
        Assert.DoesNotContain(githubToken, masked, StringComparison.Ordinal);
        Assert.DoesNotContain("ZXhhbXBsZQ==", masked, StringComparison.Ordinal);
        Assert.Contains("***", masked, StringComparison.Ordinal);
        Assert.Contains("[REDACTED PRIVATE KEY]", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void Mask_ReplacesSensitiveValues_InJsonFlagsAndUrlCredentials()
    {
        var input =
            """{"token":"abc123","client_secret":"shh"} --bearer-token real-token --api-key='key-123' https://user:pass@example.com/path?token=xyz""";

        var masked = SecretMasker.Mask(input);

        Assert.DoesNotContain("abc123", masked, StringComparison.Ordinal);
        Assert.DoesNotContain("shh", masked, StringComparison.Ordinal);
        Assert.DoesNotContain("real-token", masked, StringComparison.Ordinal);
        Assert.DoesNotContain("key-123", masked, StringComparison.Ordinal);
        Assert.DoesNotContain("user:pass", masked, StringComparison.Ordinal);
        Assert.DoesNotContain("xyz", masked, StringComparison.Ordinal);
        Assert.Contains(@"""token"":""***""", masked, StringComparison.Ordinal);
        Assert.Contains(@"""client_secret"":""***""", masked, StringComparison.Ordinal);
        Assert.Contains("--bearer-token ***", masked, StringComparison.Ordinal);
        Assert.Contains("--api-key='***'", masked, StringComparison.Ordinal);
        Assert.Contains("https://***@example.com/path?token=***", masked, StringComparison.Ordinal);
    }
}
