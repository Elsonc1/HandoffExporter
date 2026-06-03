using HandoffExporter.Models;
using HandoffExporter.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace HandoffExporter.Tests
{
    public class ReposJoinTests : IDisposable
    {
        private readonly string _dir;
        public ReposJoinTests() => _dir = Path.Combine(Path.GetTempPath(), "hjoin-" + Guid.NewGuid().ToString("N"));
        public void Dispose() { try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { } }

        private JObject ReadJson(params string[] rel) => JObject.Parse(File.ReadAllText(Path.Combine(_dir, Path.Combine(rel))));

        private static RepoInfo RepoWithPrs() => new()
        {
            Id = "g",
            Name = "AppConnector",
            Project = "Integrações",
            Branches = new() { "main" },
            PullRequests = new()
            {
                new PrInfo { Id = 42, Title = "PR A", Status = "completed", WorkItemIds = new() { 193404, 193405 } },
                new PrInfo { Id = 7,  Title = "PR B", Status = "active",    WorkItemIds = new() { 193404 } }
            },
            Commits = new() { new CommitInfo { CommitId = "sha1", Comment = "fix", Author = "Fulano", Date = "2026-06-02" } }
        };

        // ── Escrita PRs / commits / links ──────────────────────────────────────────
        [Fact]
        public void Write_PullRequests_File_OrderedById()
        {
            RepoWriter.Write(new[] { RepoWithPrs() }, _dir);
            var pr = ReadJson("repos", "AppConnector", "pull-requests.json");
            Assert.Equal(2, (int)pr["count"]!);
            var ids = ((JArray)pr["pullRequests"]!).Select(p => (int)p["id"]!).ToArray();
            Assert.Equal(new[] { 7, 42 }, ids); // ordenado por id
        }

        [Fact]
        public void Write_Commits_File()
        {
            RepoWriter.Write(new[] { RepoWithPrs() }, _dir);
            var c = ReadJson("repos", "AppConnector", "commits.json");
            Assert.Equal(1, (int)c["count"]!);
            Assert.Equal("sha1", (string)((JArray)c["commits"]!)[0]!["commitId"]!);
        }

        [Fact]
        public void Write_Links_Aggregated_And_Sorted()
        {
            RepoWriter.Write(new[] { RepoWithPrs() }, _dir);
            var links = (JArray)ReadJson("repos", "links.json")["links"]!;
            // 193404 aparece em PR 7 e 42; 193405 em PR 42 → 3 vínculos
            Assert.Equal(3, links.Count);
            // ordenado por workItemId, depois prId
            Assert.Equal(193404, (int)links[0]!["workItemId"]!);
            Assert.Equal(7, (int)links[0]!["prId"]!);
            Assert.Equal(193404, (int)links[1]!["workItemId"]!);
            Assert.Equal(42, (int)links[1]!["prId"]!);
            Assert.Equal(193405, (int)links[2]!["workItemId"]!);
        }

        [Fact]
        public void Write_NoPrs_NoPullRequestsFile_EmptyLinks()
        {
            RepoWriter.Write(new[] { new RepoInfo { Id = "g", Name = "Solo", Branches = new() { "main" } } }, _dir);
            Assert.False(File.Exists(Path.Combine(_dir, "repos", "Solo", "pull-requests.json")));
            Assert.Equal(0, (int)ReadJson("repos", "links.json")["count"]!);
        }

        // ── HandoffStore.GetLinksForWorkItem ──────────────────────────────────────
        [Fact]
        public void Store_GetLinksForWorkItem_Filters()
        {
            RepoWriter.Write(new[] { RepoWithPrs() }, _dir);
            var store = new HandoffStore(_dir);
            var arr = JArray.Parse(store.GetLinksForWorkItem(193404));
            Assert.Equal(2, arr.Count);
            Assert.All(arr, l => Assert.Equal(193404, (int)l["workItemId"]!));

            Assert.Equal(1, JArray.Parse(store.GetLinksForWorkItem(193405)).Count);
            Assert.Empty(JArray.Parse(store.GetLinksForWorkItem(999)));
        }

        // ── Parsing dos DTOs ───────────────────────────────────────────────────────
        [Fact]
        public void Parse_GitPrResult()
        {
            const string json = """
            {"count":1,"value":[{"pullRequestId":42,"title":"PR","status":"completed",
            "sourceRefName":"refs/heads/f","targetRefName":"refs/heads/main",
            "createdBy":{"displayName":"Fulano"},"creationDate":"2026-06-02"}]}
            """;
            var r = JsonConvert.DeserializeObject<GitPrResult>(json)!;
            Assert.Equal(42, r.Value[0].PullRequestId);
            Assert.Equal("Fulano", r.Value[0].CreatedBy!.DisplayName);
        }

        [Fact]
        public void Parse_ResourceRefResult_WorkItemIdsAsString()
        {
            const string json = """{"count":2,"value":[{"id":"193404"},{"id":"193405"}]}""";
            var r = JsonConvert.DeserializeObject<ResourceRefResult>(json)!;
            Assert.Equal("193404", r.Value[0].Id);
        }

        [Fact]
        public void Parse_GitCommitResult()
        {
            const string json = """{"count":1,"value":[{"commitId":"sha","comment":"c","author":{"name":"F","date":"2026"}}]}""";
            var r = JsonConvert.DeserializeObject<GitCommitResult>(json)!;
            Assert.Equal("sha", r.Value[0].CommitId);
            Assert.Equal("F", r.Value[0].Author!.Name);
        }
    }
}
