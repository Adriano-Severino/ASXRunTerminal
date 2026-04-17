using ASXRunTerminal.Config;
using ASXRunTerminal.Core;

namespace ASXRunTerminal.Tests;

public sealed class McpServerCatalogFileTests
{
    [Fact]
    public void EnsureExists_CreatesCatalogInsideUserHome()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");

        try
        {
            var catalogPath = McpServerCatalogFile.EnsureExists(() => userHome);

            Assert.Equal(
                Path.Combine(userHome, UserConfigFile.ConfigDirectoryName, McpServerCatalogFile.McpServersFileName),
                catalogPath);
            Assert.True(File.Exists(catalogPath));
            var fileContent = File.ReadAllText(catalogPath);
            Assert.Contains("\"servers\": []", fileContent);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void SaveAndLoad_WhenCatalogContainsStdioAndRemoteServers_RoundTripsValues()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var servers = new McpServerDefinition[]
        {
            McpServerDefinition.Stdio(
                "filesystem",
                new McpServerProcessOptions(
                    command: "node",
                    arguments: ["server.js", "--mode", "readonly"],
                    workingDirectory: "C:\\tools\\mcp",
                    environmentVariables: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["NODE_ENV"] = "production"
                    })),
            McpServerDefinition.Remote(
                "github",
                new McpServerRemoteOptions(
                    endpoint: new Uri("https://mcp.example.com/rpc"),
                    transportKind: McpRemoteTransportKind.Http,
                    authentication: McpAuthenticationOptions.Bearer("token-abc"),
                    headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["X-Tenant"] = "acme"
                    }))
        };

        try
        {
            McpServerCatalogFile.Save(servers, () => userHome);

            var loaded = McpServerCatalogFile.Load(() => userHome);

            Assert.Equal(2, loaded.Count);

            var stdioServer = loaded.Single(server => server.Name == "filesystem");
            Assert.NotNull(stdioServer.ProcessOptions);
            Assert.Null(stdioServer.RemoteOptions);
            Assert.Equal("node", stdioServer.ProcessOptions!.Command);
            Assert.Equal(3, stdioServer.ProcessOptions.Arguments.Count);
            Assert.Equal("C:\\tools\\mcp", stdioServer.ProcessOptions.WorkingDirectory);
            Assert.Equal("production", stdioServer.ProcessOptions.EnvironmentVariables["NODE_ENV"]);

            var remoteServer = loaded.Single(server => server.Name == "github");
            Assert.NotNull(remoteServer.RemoteOptions);
            Assert.Null(remoteServer.ProcessOptions);
            Assert.Equal(new Uri("https://mcp.example.com/rpc"), remoteServer.RemoteOptions!.Endpoint);
            Assert.Equal(McpRemoteTransportKind.Http, remoteServer.RemoteOptions.TransportKind);
            Assert.Equal("Bearer", remoteServer.RemoteOptions.Authentication.AuthorizationScheme);
            Assert.Equal("token-abc", remoteServer.RemoteOptions.Authentication.AuthorizationParameter);
            Assert.Equal("acme", remoteServer.RemoteOptions.Headers["X-Tenant"]);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Load_WhenCatalogIsInvalidJson_ThrowsInvalidOperationException()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var catalogDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var catalogPath = Path.Combine(catalogDirectory, McpServerCatalogFile.McpServersFileName);

        try
        {
            Directory.CreateDirectory(catalogDirectory);
            File.WriteAllText(catalogPath, "{ nao-e-json }");

            var exception = Assert.Throws<InvalidOperationException>(
                () => McpServerCatalogFile.Load(() => userHome));

            Assert.Contains("Arquivo de servidores MCP invalido", exception.Message);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void Load_WhenServerNamesAreDuplicated_ThrowsInvalidOperationException()
    {
        var testRoot = BuildTestRoot();
        var userHome = Path.Combine(testRoot, "home");
        var catalogDirectory = Path.Combine(userHome, UserConfigFile.ConfigDirectoryName);
        var catalogPath = Path.Combine(catalogDirectory, McpServerCatalogFile.McpServersFileName);

        try
        {
            Directory.CreateDirectory(catalogDirectory);
            File.WriteAllText(
                catalogPath,
                """
                {
                  "servers": [
                    {
                      "name": "filesystem",
                      "transport": "stdio",
                      "command": "node"
                    },
                    {
                      "name": "FILESYSTEM",
                      "transport": "stdio",
                      "command": "node"
                    }
                  ]
                }
                """);

            var exception = Assert.Throws<InvalidOperationException>(
                () => McpServerCatalogFile.Load(() => userHome));

            Assert.Contains("nome duplicado", exception.Message);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static string BuildTestRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "asxrun-mcp-catalog-tests",
            Guid.NewGuid().ToString("N"));
    }

    private static void DeleteTestRoot(string testRoot)
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
