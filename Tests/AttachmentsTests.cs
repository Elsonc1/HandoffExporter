using HandoffExporter.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using Xunit;
using static HandoffExporter.Models.WorkItemVO;

namespace HandoffExporter.Tests
{
    public class AttachmentsTests : IDisposable
    {
        private readonly string _dir;
        public AttachmentsTests() => _dir = Path.Combine(Path.GetTempPath(), "hatt-" + Guid.NewGuid().ToString("N"));
        public void Dispose() { try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { } }

        private static Item ItemWithAtts(int id, params string[] fileNames) => new()
        {
            Id = id,
            WorkItemType = "Product Backlog Item",
            Title = "t",
            SanitizedText = "s",
            Assets = new(),
            Children = new(),
            Attachments = fileNames.Select(f => new Attachment
            {
                Url = $"https://tfs.ndd.tech/_apis/wit/attachments/{Guid.NewGuid()}",
                FileName = f,
                ContentType = "file"
            }).ToList()
        };

        // ── Parse: attributes da relação AttachedFile ───────────────────────────────
        [Fact]
        public void Parse_RelationAttributes_NameAndSize()
        {
            const string json = """
            {"id":1,"fields":{},"relations":[{"rel":"AttachedFile",
              "url":"https://tfs.ndd.tech/_apis/wit/attachments/abc",
              "attributes":{"id":99,"name":"spec-funcional.pdf","resourceSize":2048}}]}
            """;
            var wi = JsonConvert.DeserializeObject<WorkItem>(json)!;
            Assert.Equal("spec-funcional.pdf", wi.Relations[0].Attributes!.Name);
            Assert.Equal(2048, wi.Relations[0].Attributes!.ResourceSize);
        }

        // ── Planner ──────────────────────────────────────────────────────────────────
        [Fact]
        public void Planner_AssignsFolderPerArtifact_WithRealName()
        {
            var item = ItemWithAtts(204055, "layout.xml");
            AttachmentPlanner.AssignLocalPaths(new[] { item });
            Assert.Equal("attachments/204055/layout.xml", item.Attachments[0].LocalPath);
        }

        [Fact]
        public void Planner_Dedupes_SameNameInSameItem()
        {
            var item = ItemWithAtts(1, "log.txt", "log.txt", "log.txt");
            AttachmentPlanner.AssignLocalPaths(new[] { item });
            Assert.Equal("attachments/1/log.txt", item.Attachments[0].LocalPath);
            Assert.Equal("attachments/1/log-2.txt", item.Attachments[1].LocalPath);
            Assert.Equal("attachments/1/log-3.txt", item.Attachments[2].LocalPath);
        }

        [Fact]
        public void Planner_Recursive_CoversChildren()
        {
            var parent = ItemWithAtts(1, "a.txt");
            parent.Children = new List<Item> { ItemWithAtts(2, "b.json") };
            AttachmentPlanner.AssignLocalPaths(new[] { parent });
            Assert.Equal("attachments/2/b.json", parent.Children[0].Attachments[0].LocalPath);
        }

        [Fact]
        public void SafeFileName_Sanitizes_TraversalAndInvalidChars()
        {
            Assert.Equal("evil.txt", AttachmentPlanner.SafeFileName(@"..\..\evil.txt"));
            Assert.Equal("evil.txt", AttachmentPlanner.SafeFileName("../etc/evil.txt"));
            Assert.Equal("a-b-c.xml", AttachmentPlanner.SafeFileName("a<b>c.xml").Replace("--", "-")); // chars inválidos viram '-'
            Assert.Equal("attachment", AttachmentPlanner.SafeFileName(null));
            Assert.Equal("attachment", AttachmentPlanner.SafeFileName("   "));
        }

        // ── Splitter emite localPath ─────────────────────────────────────────────────
        [Fact]
        public void Splitter_Emits_AttachmentLocalPath()
        {
            var item = ItemWithAtts(7, "doc.txt");
            item.Attachments[0].LocalPath = "attachments/7/doc.txt";
            var handoff = new HandoffJson
            {
                Source = new Source(), Request = new Request { AreaPath = "MacGyver" },
                ExportedAtUtc = "2026-06-11T00:00:00Z", Items = new() { item },
                Handoff = new Handoff { Generator = "HandoffExporter" }
            };
            HandoffSplitter.Split(handoff, _dir);
            var j = JObject.Parse(File.ReadAllText(Path.Combine(_dir, "pbi", "PBI-7.json")));
            Assert.Equal("attachments/7/doc.txt", (string)((JArray)j["attachments"]!)[0]!["localPath"]!);
        }

        // ── Downloader (HTTP fake — sem rede) ────────────────────────────────────────
        private class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
                => Task.FromResult(_respond(request));
        }

        private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond) => new(new FakeHandler(respond));

        [Fact]
        public async Task Downloader_WritesFile_AtPlannedPath()
        {
            var item = ItemWithAtts(10, "spec.txt");
            AttachmentPlanner.AssignLocalPaths(new[] { item });
            var http = Client(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) });

            var (ok, failed, skipped) = await new AttachmentDownloader(http, null!).DownloadAllAsync(new[] { item }, _dir);

            Assert.Equal((1, 0, 0), (ok, failed, skipped));
            var full = Path.Combine(_dir, "attachments", "10", "spec.txt");
            Assert.True(File.Exists(full));
            Assert.Equal(3, new FileInfo(full).Length);
            Assert.Equal("attachments/10/spec.txt", item.Attachments[0].LocalPath); // mantido
        }

        [Fact]
        public async Task Downloader_Failure_NullsLocalPath_DoesNotThrow()
        {
            var item = ItemWithAtts(11, "x.bin");
            AttachmentPlanner.AssignLocalPaths(new[] { item });
            var http = Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            var (ok, failed, skipped) = await new AttachmentDownloader(http, null!).DownloadAllAsync(new[] { item }, _dir);

            Assert.Equal((0, 1, 0), (ok, failed, skipped));
            Assert.Null(item.Attachments[0].LocalPath);                     // JSON não anuncia arquivo inexistente
            Assert.False(Directory.Exists(Path.Combine(_dir, "attachments", "11")));
        }

        [Fact]
        public async Task Downloader_OverMaxBytes_Skips_NullsLocalPath()
        {
            var item = ItemWithAtts(12, "grande.zip");
            AttachmentPlanner.AssignLocalPaths(new[] { item });
            var http = Client(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[100]) });

            var (ok, failed, skipped) = await new AttachmentDownloader(http, null!, maxBytes: 10).DownloadAllAsync(new[] { item }, _dir);

            Assert.Equal((0, 0, 1), (ok, failed, skipped));
            Assert.Null(item.Attachments[0].LocalPath);
            Assert.False(File.Exists(Path.Combine(_dir, "attachments", "12", "grande.zip")));
        }
    }
}
