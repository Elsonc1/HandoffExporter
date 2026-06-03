using HandoffExporter;
using HandoffExporter.Models;
using HandoffExporter.Services;
using Xunit;

namespace HandoffExporter.Tests
{
    public class HandoffStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly HandoffStore _store;

        public HandoffStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hstore-" + Guid.NewGuid().ToString("N"));

            var handoff = new HandoffJson
            {
                Source = new Source { Type = "azure-devops", Collection = "NDD-DECollection", Project = "Central de Soluções" },
                Request = new Request { AreaPath = "MacGyver", Mode = "all-artifacts" },
                ExportedAtUtc = "2026-06-02T16:30:00.0000000Z",
                Items = new List<Item>
                {
                    new() {
                        Id = 100, WorkItemType = "Product Backlog Item", Title = "Integração ConfigVO",
                        SanitizedText = "PBI sobre configuração", State = "In Development",
                        Assets = new(), Attachments = new(),
                        Children = new List<Item>
                        {
                            new() { Id = 101, WorkItemType = "User Story", Title = "Tela de login",
                                    SanitizedText = "regra de negócio da autenticação", AcceptanceCriteria = "Dado usuário válido",
                                    State = "Active", Assets = new(), Attachments = new(), Children = new() }
                        }
                    }
                },
                Handoff = new Handoff { Version = "1.0", Generator = "HandoffExporter" }
            };
            HandoffSplitter.Split(handoff, _dir);
            RepoWriter.Write(new[]
            {
                new RepoInfo { Id = "g1", Name = "AppConnector", DefaultBranch = "refs/heads/main", Project = "Integrações", Branches = new() { "main" } }
            }, _dir);

            _store = new HandoffStore(_dir);
        }

        public void Dispose() { try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { } }

        [Fact] public void Exists_True_AfterSplit() => Assert.True(_store.Exists);

        [Fact]
        public void GetIndex_ReturnsCatalog()
        {
            var idx = _store.GetIndex();
            Assert.NotNull(idx);
            Assert.Contains("\"pbi\": 1", idx);
        }

        [Fact]
        public void GetPbi_ReturnsPbiJson()
        {
            var pbi = _store.GetPbi(100);
            Assert.NotNull(pbi);
            Assert.Contains("Integração ConfigVO", pbi);
            Assert.Contains("In Development", pbi);
        }

        [Fact]
        public void GetUs_ReturnsUsJson_WithParentAndAc()
        {
            var us = _store.GetUs(101);
            Assert.NotNull(us);
            Assert.Contains("\"parentPbiId\": 100", us);
            Assert.Contains("Dado usuário válido", us);
        }

        [Fact]
        public void GetPbi_Missing_ReturnsNull() => Assert.Null(_store.GetPbi(999));

        [Fact]
        public void Search_ByTitle_FindsPbi()
        {
            var hits = _store.Search("ConfigVO");
            Assert.Contains(hits, h => h.Type == "pbi" && h.Id == 100);
        }

        [Fact]
        public void Search_ByDescription_FindsUs()
        {
            var hits = _store.Search("autenticação");
            Assert.Contains(hits, h => h.Type == "us" && h.Id == 101);
        }

        [Fact]
        public void Search_ByAcceptanceCriteria_FindsUs()
        {
            var hits = _store.Search("usuário válido");
            Assert.Contains(hits, h => h.Id == 101);
        }

        [Fact]
        public void Search_CaseInsensitive()
        {
            Assert.NotEmpty(_store.Search("configvo"));
        }

        [Fact]
        public void Search_NoMatch_Empty() => Assert.Empty(_store.Search("zzz-nao-existe"));

        [Fact]
        public void Repos_IndexAndGet()
        {
            Assert.NotNull(_store.GetReposIndex());
            var repo = _store.GetRepo("AppConnector");
            Assert.NotNull(repo);
            Assert.Contains("Integrações", repo);
        }
    }
}
