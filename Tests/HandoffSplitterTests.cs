using HandoffExporter;
using HandoffExporter.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace HandoffExporter.Tests
{
    public class HandoffSplitterTests : IDisposable
    {
        private readonly string _dir;

        public HandoffSplitterTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hsplit-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { /* best effort */ }
        }

        // ── 1x1 PNG transparente ───────────────────────────────────────────────
        private const string PngDataUri =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

        // ── Builders ────────────────────────────────────────────────────────────
        private static Item Us(int id, string title = "US", string san = "texto", string? raw = null,
            string state = "Active", string? ac = null, List<Asset>? assets = null,
            List<Item>? children = null, List<Attachment>? atts = null) => new()
        {
            Id = id,
            WorkItemType = "User Story",
            Title = title,
            SanitizedText = san,
            RawHtml = raw,
            State = state,
            AcceptanceCriteria = ac,
            Assets = assets ?? new(),
            Attachments = atts ?? new(),
            Children = children ?? new()
        };

        private static Item Pbi(int id, string title = "PBI", string san = "pbi", string? raw = null,
            string state = "New", List<Item>? children = null, List<Attachment>? atts = null) => new()
        {
            Id = id,
            WorkItemType = "Product Backlog Item",
            Title = title,
            SanitizedText = san,
            RawHtml = raw,
            State = state,
            Assets = new(),
            Attachments = atts ?? new(),
            Children = children ?? new()
        };

        private static HandoffJson Handoff(params Item[] roots) => new()
        {
            Source = new Source { Type = "azure-devops", Collection = "NDD-DECollection", Project = "Central de Solucoes" },
            Request = new Request { AreaPath = "MacGyver", PbiId = null, IncludeIssues = false, Mode = "all-artifacts" },
            ExportedAtUtc = "2026-06-02T16:30:00.0000000Z",
            Items = roots.ToList(),
            Handoff = new Handoff { Version = "1.0", Generator = "HandoffExporter" }
        };

        private static Asset DataUriAsset() => new() { Url = PngDataUri, DataUri = PngDataUri, ContentType = "image", FileName = "image" };
        private static Asset UrlAsset(string url) => new() { Url = url, DataUri = null, ContentType = "image", FileName = "image" };

        private JObject ReadJson(params string[] rel) => JObject.Parse(File.ReadAllText(Path.Combine(_dir, Path.Combine(rel))));
        private string ReadText(params string[] rel) => File.ReadAllText(Path.Combine(_dir, Path.Combine(rel)));

        // ── Guard clauses ─────────────────────────────────────────────────────────
        [Fact]
        public void Split_NullHandoff_Throws() =>
            Assert.Throws<ArgumentNullException>(() => HandoffSplitter.Split(null!, _dir));

        [Fact]
        public void Split_EmptyOutDir_Throws() =>
            Assert.Throws<ArgumentException>(() => HandoffSplitter.Split(Handoff(), "  "));

        // ── Estrutura / index ──────────────────────────────────────────────────────
        [Fact]
        public void Split_CreatesIndexAndFolders()
        {
            HandoffSplitter.Split(Handoff(Pbi(1, children: new() { Us(2) })), _dir);
            Assert.True(File.Exists(Path.Combine(_dir, "index.json")));
            Assert.True(Directory.Exists(Path.Combine(_dir, "pbi")));
            Assert.True(Directory.Exists(Path.Combine(_dir, "us")));
            Assert.True(File.Exists(Path.Combine(_dir, "pbi", "PBI-1.json")));
            Assert.True(File.Exists(Path.Combine(_dir, "us", "US-2.json")));
        }

        [Fact]
        public void Split_IndexCountsCorrect()
        {
            HandoffSplitter.Split(Handoff(Pbi(1, children: new() { Us(2), Us(3) }), Pbi(4)), _dir);
            var idx = ReadJson("index.json");
            Assert.Equal(2, (int)idx["counts"]!["pbi"]!);
            Assert.Equal(2, (int)idx["counts"]!["us"]!);
            Assert.Equal(2, ((JArray)idx["pbis"]!).Count);
        }

        [Fact]
        public void Split_EmptyItems_IndexOnly_ZeroCounts()
        {
            HandoffSplitter.Split(Handoff(), _dir);
            var idx = ReadJson("index.json");
            Assert.Equal(0, (int)idx["counts"]!["pbi"]!);
            Assert.Equal(0, (int)idx["counts"]!["us"]!);
            Assert.Empty(Directory.GetFiles(Path.Combine(_dir, "pbi")));
            Assert.Empty(Directory.GetFiles(Path.Combine(_dir, "us")));
        }

        // ── Determinismo ──────────────────────────────────────────────────────────
        [Fact]
        public void Split_ChildUsIdsSortedAscending()
        {
            // filhos em ordem invertida na entrada
            HandoffSplitter.Split(Handoff(Pbi(1, children: new() { Us(9), Us(3), Us(7) })), _dir);
            var pbi = ReadJson("pbi", "PBI-1.json");
            Assert.Equal(new[] { 3, 7, 9 }, ((JArray)pbi["childUsIds"]!).Select(t => (int)t!).ToArray());
        }

        [Fact]
        public void Split_Deterministic_TwoRuns_SameBytes()
        {
            var a = Path.Combine(_dir, "a");
            var b = Path.Combine(_dir, "b");
            var h = Handoff(Pbi(1, children: new() { Us(3), Us(2) }), Pbi(5));
            HandoffSplitter.Split(h, a);
            HandoffSplitter.Split(h, b);
            Assert.Equal(File.ReadAllText(Path.Combine(a, "index.json")), File.ReadAllText(Path.Combine(b, "index.json")));
            Assert.Equal(File.ReadAllText(Path.Combine(a, "pbi", "PBI-1.json")), File.ReadAllText(Path.Combine(b, "pbi", "PBI-1.json")));
        }

        // ── Sanitização: nada de base64 inline ─────────────────────────────────────
        [Fact]
        public void Split_NoBase64LeaksIntoAgentJson()
        {
            HandoffSplitter.Split(Handoff(Pbi(1, children: new() { Us(2, assets: new() { DataUriAsset() }) })), _dir);
            var agentFiles = Directory.GetFiles(Path.Combine(_dir, "us"))
                .Concat(Directory.GetFiles(Path.Combine(_dir, "pbi")))
                .Append(Path.Combine(_dir, "index.json"));
            foreach (var f in agentFiles)
                Assert.DoesNotContain("base64", File.ReadAllText(f), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Split_DataUriExtractedToValidPng()
        {
            HandoffSplitter.Split(Handoff(Pbi(1, children: new() { Us(2, assets: new() { DataUriAsset() }) })), _dir);
            var assetPath = Path.Combine(_dir, "assets", "2-asset-1.png");
            Assert.True(File.Exists(assetPath));
            var bytes = File.ReadAllBytes(assetPath);
            Assert.True(bytes.Length >= 8);
            Assert.Equal(0x89, bytes[0]);
            Assert.Equal((byte)'P', bytes[1]);
            Assert.Equal((byte)'N', bytes[2]);
            Assert.Equal((byte)'G', bytes[3]);

            var us = ReadJson("us", "US-2.json");
            var asset = (JObject)((JArray)us["assets"]!)[0]!;
            Assert.Equal("assets/2-asset-1.png", (string)asset["path"]!);
            Assert.Equal("image/png", (string)asset["contentType"]!);
        }

        [Fact]
        public void Split_ExternalUrlAsset_KeptAsReference_NoFile()
        {
            HandoffSplitter.Split(Handoff(Pbi(1, children: new() { Us(2, assets: new() { UrlAsset("https://tfs.ndd.tech/x.png") }) })), _dir);
            Assert.Empty(Directory.GetFiles(Path.Combine(_dir, "assets")));
            var asset = (JObject)((JArray)ReadJson("us", "US-2.json")["assets"]!)[0]!;
            Assert.Equal("https://tfs.ndd.tech/x.png", (string)asset["url"]!);
        }

        [Fact]
        public void Split_MalformedBase64_DoesNotThrow_NoLeak_NoFile()
        {
            var bad = new Asset { Url = "data:image/png;base64,@@@nope@@@", DataUri = "data:image/png;base64,@@@nope@@@", ContentType = "image", FileName = "image" };
            HandoffSplitter.Split(Handoff(Pbi(1, children: new() { Us(2, assets: new() { bad }) })), _dir);
            Assert.Empty(Directory.GetFiles(Path.Combine(_dir, "assets")));
            var us = ReadText("us", "US-2.json");
            Assert.DoesNotContain("base64", us, StringComparison.OrdinalIgnoreCase);
            var asset = (JObject)((JArray)ReadJson("us", "US-2.json")["assets"]!)[0]!;
            Assert.Null((string?)asset["url"]); // data: URI nunca emitida inline
        }

        // ── RawHtml → raw/ ──────────────────────────────────────────────────────────
        [Fact]
        public void Split_RawHtml_WrittenToRawFolder()
        {
            HandoffSplitter.Split(Handoff(Pbi(1, raw: "<b>x</b>", children: new() { Us(2, raw: "<i>y</i>") })), _dir);
            Assert.True(File.Exists(Path.Combine(_dir, "raw", "1.html")));
            Assert.True(File.Exists(Path.Combine(_dir, "raw", "2.html")));
            Assert.Equal("<b>x</b>", File.ReadAllText(Path.Combine(_dir, "raw", "1.html")));
            Assert.Equal("raw/1.html", (string)ReadJson("pbi", "PBI-1.json")["rawPath"]!);
        }

        [Fact]
        public void Split_NullRawHtml_NoRawFile_RawPathNull()
        {
            HandoffSplitter.Split(Handoff(Pbi(1, raw: null, children: new() { Us(2, raw: null) })), _dir);
            Assert.Empty(Directory.GetFiles(Path.Combine(_dir, "raw")));
            Assert.Null((string?)ReadJson("us", "US-2.json")["rawPath"]);
        }

        // ── Cross-refs ──────────────────────────────────────────────────────────────
        [Fact]
        public void Split_Us_HasParentPbiId()
        {
            HandoffSplitter.Split(Handoff(Pbi(10, children: new() { Us(20) })), _dir);
            Assert.Equal(10, (int)ReadJson("us", "US-20.json")["parentPbiId"]!);
        }

        [Fact]
        public void Split_PbiChildrenPaths_AreRootRelative()
        {
            HandoffSplitter.Split(Handoff(Pbi(1, children: new() { Us(2) })), _dir);
            var child = (JObject)((JArray)ReadJson("pbi", "PBI-1.json")["children"]!)[0]!;
            Assert.Equal("us/US-2.json", (string)child["path"]!);
        }

        [Fact]
        public void Split_PbiWithoutChildren_Preserved_EmptyChildUsIds()
        {
            HandoffSplitter.Split(Handoff(Pbi(1)), _dir);
            Assert.True(File.Exists(Path.Combine(_dir, "pbi", "PBI-1.json")));
            Assert.Empty((JArray)ReadJson("pbi", "PBI-1.json")["childUsIds"]!);
        }

        [Fact]
        public void Split_DeepNesting_AllDescendants_AsUs_WithImmediateParent()
        {
            // PBI 1 → US 2 → US 3
            HandoffSplitter.Split(Handoff(Pbi(1, children: new() { Us(2, children: new() { Us(3) }) })), _dir);
            Assert.True(File.Exists(Path.Combine(_dir, "us", "US-2.json")));
            Assert.True(File.Exists(Path.Combine(_dir, "us", "US-3.json")));
            Assert.Equal(1, (int)ReadJson("us", "US-2.json")["parentPbiId"]!);
            Assert.Equal(2, (int)ReadJson("us", "US-3.json")["parentPbiId"]!);
            Assert.Equal(2, (int)ReadJson("index.json")["counts"]!["us"]!);
        }

        // ── Enriquecimento (Fase 2a) ───────────────────────────────────────────────
        [Fact]
        public void Split_State_And_AcceptanceCriteria_Emitted()
        {
            HandoffSplitter.Split(Handoff(Pbi(1, state: "In Development",
                children: new() { Us(2, state: "Active", ac: "Dado/Quando/Então") })), _dir);

            var pbi = ReadJson("pbi", "PBI-1.json");
            Assert.Equal("In Development", (string)pbi["state"]!);

            var us = ReadJson("us", "US-2.json");
            Assert.Equal("Active", (string)us["state"]!);
            Assert.Equal("Dado/Quando/Então", (string)us["acceptanceCriteria"]!);

            // index também expõe o state do PBI
            var idxPbi = (JObject)((JArray)ReadJson("index.json")["pbis"]!)[0]!;
            Assert.Equal("In Development", (string)idxPbi["state"]!);
        }

        // ── Attachments ──────────────────────────────────────────────────────────────
        [Fact]
        public void Split_Attachments_Preserved()
        {
            var att = new Attachment { Url = "https://tfs.ndd.tech/a/1", FileName = "spec.pdf", ContentType = "file", Size = 42 };
            HandoffSplitter.Split(Handoff(Pbi(1, atts: new() { att })), _dir);
            var a = (JObject)((JArray)ReadJson("pbi", "PBI-1.json")["attachments"]!)[0]!;
            Assert.Equal("spec.pdf", (string)a["fileName"]!);
            Assert.Equal(42, (int)a["size"]!);
        }

        // ── Robustez ──────────────────────────────────────────────────────────────
        [Fact]
        public void Split_TitleWithBraces_DoesNotThrow()
        {
            // chaves no conteúdo não podem quebrar a serialização nem o logging
            var ex = Record.Exception(() => HandoffSplitter.Split(Handoff(Pbi(1, title: "VO {ConfigVO} {0}", san: "a {b} c")), _dir));
            Assert.Null(ex);
            Assert.Contains("VO {ConfigVO} {0}", ReadText("pbi", "PBI-1.json"));
        }

        [Fact]
        public void Split_NullChildrenAndAssets_DoesNotThrow()
        {
            var item = new Item { Id = 1, WorkItemType = "Product Backlog Item", Title = "x", SanitizedText = "y", Assets = null!, Attachments = null!, Children = null! };
            var h = Handoff();
            h.Items = new List<Item> { item };
            var ex = Record.Exception(() => HandoffSplitter.Split(h, _dir));
            Assert.Null(ex);
            Assert.True(File.Exists(Path.Combine(_dir, "pbi", "PBI-1.json")));
        }
    }
}
