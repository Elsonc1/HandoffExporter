using HandoffExporter.Models;
using HandoffExporter.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace HandoffExporter.Tests
{
    public class ReposTests : IDisposable
    {
        private readonly string _dir;
        public ReposTests() => _dir = Path.Combine(Path.GetTempPath(), "hrepos-" + Guid.NewGuid().ToString("N"));
        public void Dispose() { try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { } }

        private static RepoInfo Repo(string name, string id = "g", string? def = "refs/heads/main",
            long size = 10, List<string>? branches = null) => new()
        {
            Id = id,
            Name = name,
            DefaultBranch = def,
            Size = size,
            RemoteUrl = "https://tfs.ndd.tech/_git/" + name,
            WebUrl = "https://tfs.ndd.tech/_git/" + name,
            Project = "Integrações",
            Branches = branches ?? new() { "main" }
        };

        private JObject ReadJson(params string[] rel) => JObject.Parse(File.ReadAllText(Path.Combine(_dir, Path.Combine(rel))));

        // ── URL encoding (multi-project, single collection) ────────────────────────
        [Fact]
        public void BuildBaseUrl_EncodesAccentedProject()
        {
            Assert.Equal(
                "https://tfs.ndd.tech/NDD-DECollection/Integra%C3%A7%C3%B5es",
                GitQueryService.BuildBaseUrl("NDD-DECollection", "Integrações"));
        }

        // ── RepoWriter ───────────────────────────────────────────────────────────────
        [Fact]
        public void Write_CreatesIndexAndPerRepoFiles()
        {
            RepoWriter.Write(new[] { Repo("Alpha"), Repo("Beta") }, _dir);
            Assert.True(File.Exists(Path.Combine(_dir, "repos", "index.json")));
            Assert.True(File.Exists(Path.Combine(_dir, "repos", "Alpha", "repo.json")));
            Assert.True(File.Exists(Path.Combine(_dir, "repos", "Beta", "repo.json")));
            Assert.Equal(2, (int)ReadJson("repos", "index.json")["count"]!);
        }

        [Fact]
        public void Write_RepoJson_HasExpectedFields()
        {
            RepoWriter.Write(new[] { Repo("AppConnector", id: "guid-1", def: "refs/heads/main", size: 4096, branches: new() { "main", "develop" }) }, _dir);
            var j = ReadJson("repos", "AppConnector", "repo.json");
            Assert.Equal("guid-1", (string)j["id"]!);
            Assert.Equal("AppConnector", (string)j["name"]!);
            Assert.Equal("Integrações", (string)j["project"]!);
            Assert.Equal("refs/heads/main", (string)j["defaultBranch"]!);
            Assert.Equal(4096, (long)j["size"]!);
            Assert.Equal(new[] { "develop", "main" }, ((JArray)j["branches"]!).Select(t => (string)t!).ToArray());
        }

        [Fact]
        public void Write_OrderedByName_Deterministic()
        {
            var input = new[] { Repo("Zeta"), Repo("Alpha"), Repo("Mid") };
            var a = Path.Combine(_dir, "a");
            var b = Path.Combine(_dir, "b");
            RepoWriter.Write(input, a);
            RepoWriter.Write(input, b);
            Assert.Equal(File.ReadAllText(Path.Combine(a, "repos", "index.json")),
                         File.ReadAllText(Path.Combine(b, "repos", "index.json")));
            var first = (string)((JArray)JObject.Parse(File.ReadAllText(Path.Combine(a, "repos", "index.json")))["repos"]!)[0]!["name"]!;
            Assert.Equal("Alpha", first);
        }

        [Fact]
        public void Write_BranchesSortedInRepoJson()
        {
            RepoWriter.Write(new[] { Repo("R", branches: new() { "zebra", "alpha", "main" }) }, _dir);
            Assert.Equal(new[] { "alpha", "main", "zebra" },
                ((JArray)ReadJson("repos", "R", "repo.json")["branches"]!).Select(t => (string)t!).ToArray());
        }

        [Fact]
        public void Write_NullRepos_DoesNotThrow()
        {
            var ex = Record.Exception(() => RepoWriter.Write(null, _dir));
            Assert.Null(ex);
        }

        [Fact]
        public void Write_InvalidFolderChars_IndexPathMatchesActualFile()
        {
            // nome com chars problemáticos para pasta; o invariante é: o path do index aponta para o arquivo real.
            RepoWriter.Write(new[] { Repo("a:b*c") }, _dir);
            var path = (string)((JArray)ReadJson("repos", "index.json")["repos"]!)[0]!["path"]!;
            var full = Path.Combine(_dir, Path.Combine(path.Split('/')));
            Assert.True(File.Exists(full), $"esperado arquivo em {full}");
        }

        // ── Parsing dos DTOs da API Git ────────────────────────────────────────────
        [Fact]
        public void Parse_GitRepoResult_FromFixture()
        {
            const string json = """
            {"count":1,"value":[{"id":"abc-123","name":"AppConnector","defaultBranch":"refs/heads/main",
            "size":4096,"remoteUrl":"https://tfs.ndd.tech/_git/AppConnector","webUrl":"https://tfs.ndd.tech/x",
            "project":{"id":"p1","name":"Integrações"}}]}
            """;
            var r = JsonConvert.DeserializeObject<GitRepoResult>(json)!;
            Assert.Equal(1, r.Count);
            Assert.Single(r.Value);
            Assert.Equal("AppConnector", r.Value[0].Name);
            Assert.Equal("refs/heads/main", r.Value[0].DefaultBranch);
            Assert.Equal(4096, r.Value[0].Size);
            Assert.Equal("Integrações", r.Value[0].Project!.Name);
        }

        [Fact]
        public void Parse_GitRefResult_FromFixture()
        {
            const string json = """
            {"count":2,"value":[{"name":"refs/heads/main","objectId":"sha1"},{"name":"refs/heads/develop","objectId":"sha2"}]}
            """;
            var r = JsonConvert.DeserializeObject<GitRefResult>(json)!;
            Assert.Equal(2, r.Value.Count);
            Assert.Equal("refs/heads/main", r.Value[0].Name);
        }
    }
}
