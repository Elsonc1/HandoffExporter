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
                                    State = "Active", Assets = new(), Attachments = new(), Children = new() },
                            new() { Id = 102, WorkItemType = "Sprint Task", Title = "ajuste fino",
                                    SanitizedText = "task de sprint", State = "Done", Assets = new(), Attachments = new(), Children = new() }
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
            Assert.Contains("\"st\": 1", idx); // Sprint Task contabilizada na pasta certa
        }

        [Fact]
        public void GetItem_Pbi_ResolvedByType()
        {
            var pbi = _store.GetItem(100);
            Assert.NotNull(pbi);
            Assert.Contains("Integração ConfigVO", pbi);
            Assert.Contains("In Development", pbi);
        }

        [Fact]
        public void GetItem_Us_HasParentAndAc()
        {
            var us = _store.GetItem(101);
            Assert.NotNull(us);
            Assert.Contains("\"parentId\": 100", us);
            Assert.Contains("Dado usuário válido", us);
        }

        [Fact]
        public void GetItem_SprintTask_ResolvedFromStFolder()
        {
            // O id 102 é Sprint Task → vive em st/ST-102.json; GetItem resolve via index.
            var st = _store.GetItem(102);
            Assert.NotNull(st);
            Assert.Contains("Sprint Task", st);
            Assert.True(File.Exists(Path.Combine(_dir, "st", "ST-102.json")));
        }

        [Fact]
        public void GetItem_Missing_ReturnsNull() => Assert.Null(_store.GetItem(999));

        [Fact]
        public void Search_ByTitle_FindsPbi() => Assert.Contains(_store.Search("ConfigVO"), h => h.Id == 100);

        [Fact]
        public void Search_ByDescription_FindsUs() => Assert.Contains(_store.Search("autenticação"), h => h.Id == 101);

        [Fact]
        public void Search_ByAcceptanceCriteria_FindsUs() => Assert.Contains(_store.Search("usuário válido"), h => h.Id == 101);

        [Fact]
        public void Search_ReturnsWorkItemTypeInHit()
        {
            var hit = _store.Search("ConfigVO").First(h => h.Id == 100);
            Assert.Equal("Product Backlog Item", hit.Type);
        }

        [Fact] public void Search_CaseInsensitive() => Assert.NotEmpty(_store.Search("configvo"));
        [Fact] public void Search_NoMatch_Empty() => Assert.Empty(_store.Search("zzz-nao-existe"));

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
