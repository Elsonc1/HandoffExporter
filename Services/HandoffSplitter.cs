using HandoffExporter.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HandoffExporter.Services
{
    /// <summary>
    /// Quebra um <see cref="HandoffJson"/> em sub-arquivos pequenos, **segmentados pelo
    /// WorkItemType real de cada item** (não pela posição na árvore):
    ///   {outDir}/index.json                 catálogo: counts por tipo + roots(árvore) + items(lookup)
    ///   {outDir}/&lt;tipo&gt;/&lt;PREFIX&gt;-&lt;id&gt;.json  ex.: pbi/PBI-1.json, us/US-2.json, st/ST-9.json, spike/SPIKE-3.json
    ///   {outDir}/assets/ , {outDir}/raw/
    ///
    /// Cada arquivo carrega parentId/parentPath e children (com tipo+path). Determinístico,
    /// sem segredos, data-URIs extraídos p/ assets e RawHtml movido p/ raw.
    /// </summary>
    public static class HandoffSplitter
    {
        public static void Split(HandoffJson handoff, string outDir, ILogHelper? logHelper = null)
        {
            if (handoff == null) throw new ArgumentNullException(nameof(handoff));
            if (string.IsNullOrWhiteSpace(outDir)) throw new ArgumentException("outDir vazio", nameof(outDir));

            logHelper?.Info("Split iniciado -> {0}", outDir);

            // Limpa o snapshot anterior (arquivos stale de runs antigas enganam o leitor).
            PrepareOutputDirectory(outDir, logHelper);

            string assetsDir = Path.Combine(outDir, "assets");
            string rawDir = Path.Combine(outDir, "raw");
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(rawDir);

            // 1) Achatar a árvore (dedupe por id), capturando o parent imediato.
            var flat = new Dictionary<int, (Item item, int? parentId)>();
            void Visit(Item it, int? parent)
            {
                if (it == null) return;
                if (!flat.ContainsKey(it.Id)) flat[it.Id] = (it, parent);
                foreach (var c in (it.Children ?? new List<Item>()).Where(c => c != null))
                    Visit(c, it.Id);
            }
            foreach (var r in (handoff.Items ?? new List<Item>()).Where(i => i != null))
                Visit(r, null);

            // 2) Path por id (folder/prefix pelo WorkItemType).
            var pathById = new Dictionary<int, (string folder, string prefix, string path, string type)>();
            foreach (var kv in flat)
            {
                var (folder, prefix) = Classify(kv.Value.item.WorkItemType);
                pathById[kv.Key] = (folder, prefix, $"{folder}/{prefix}-{kv.Key}.json", kv.Value.item.WorkItemType ?? "");
            }

            // 3) Escrever cada item na pasta do seu tipo.
            var countsByFolder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in flat.OrderBy(k => k.Key))
            {
                int id = kv.Key;
                var item = kv.Value.item;
                var meta = pathById[id];
                Directory.CreateDirectory(Path.Combine(outDir, meta.folder));

                string? rawPath = null;
                if (!string.IsNullOrEmpty(item.RawHtml))
                {
                    File.WriteAllText(Path.Combine(rawDir, $"{id}.html"), item.RawHtml);
                    rawPath = $"raw/{id}.html";
                }

                var assets = ExtractAssets(item, assetsDir, logHelper);
                var attachments = (item.Attachments ?? new List<Attachment>())
                    .Where(a => a != null)
                    .Select(a => new { url = a.Url, fileName = a.FileName, contentType = a.ContentType, size = a.Size })
                    .ToList();

                var childRefs = ChildRefs(item, pathById);

                int? parentId = kv.Value.parentId;
                string? parentPath = parentId.HasValue && pathById.TryGetValue(parentId.Value, out var pm) ? pm.path : null;

                var node = new
                {
                    id,
                    workItemType = item.WorkItemType,
                    title = item.Title,
                    state = item.State,
                    description = item.SanitizedText,
                    acceptanceCriteria = item.AcceptanceCriteria,
                    // Mapa completo dos campos de conteúdo do VO (varia por tipo) — nada se perde.
                    contentFields = (item.ContentFields != null && item.ContentFields.Count > 0) ? item.ContentFields : null,
                    parentId,
                    parentPath,
                    childIds = childRefs.Select(c => c.id).ToList(),
                    children = childRefs,
                    attachments,
                    assets,
                    rawPath
                };

                File.WriteAllText(Path.Combine(outDir, meta.folder, $"{meta.prefix}-{id}.json"),
                    JsonConvert.SerializeObject(node, Formatting.Indented));

                countsByFolder.TryGetValue(meta.folder, out var n);
                countsByFolder[meta.folder] = n + 1;
            }

            // 4) index.json — counts por tipo + roots(árvore) + items(lookup id→path).
            var roots = flat.Where(kv => kv.Value.parentId == null).OrderBy(kv => kv.Key)
                .Select(kv => new
                {
                    id = kv.Key,
                    workItemType = kv.Value.item.WorkItemType,
                    title = kv.Value.item.Title,
                    state = kv.Value.item.State,
                    path = pathById[kv.Key].path,
                    children = ChildRefs(kv.Value.item, pathById)
                }).ToList();

            var items = flat.Keys.OrderBy(x => x)
                .Select(id => new { id, workItemType = pathById[id].type, path = pathById[id].path })
                .ToList();

            var index = new
            {
                area = handoff.Request?.AreaPath,
                exportedAtUtc = handoff.ExportedAtUtc,
                generator = handoff.Handoff?.Generator ?? "HandoffExporter",
                version = "3.0",
                source = new { collection = handoff.Source?.Collection, project = handoff.Source?.Project },
                request = new
                {
                    areaPath = handoff.Request?.AreaPath,
                    pbiId = handoff.Request?.PbiId,
                    mode = handoff.Request?.Mode,
                    includeIssues = handoff.Request?.IncludeIssues
                },
                total = flat.Count,
                counts = countsByFolder,
                roots,
                items
            };

            File.WriteAllText(Path.Combine(outDir, "index.json"),
                JsonConvert.SerializeObject(index, Formatting.Indented));

            int assetCount = Directory.GetFiles(assetsDir).Length;
            int rawCount = Directory.GetFiles(rawDir).Length;
            logHelper?.Info("Split concluido: {0} item(s) em {1} pasta(s) por tipo, {2} asset(s), {3} raw -> {4}",
                flat.Count, countsByFolder.Count, assetCount, rawCount, outDir);
        }

        /// <summary>
        /// Apaga o snapshot anterior (pastas por tipo, assets/, raw/, index.json), preservando
        /// repos/ (gerida pelo RepoWriter). Guarda de segurança: um diretório não-vazio SEM
        /// index.json não é um snapshot nosso — aborta em vez de apagar dados alheios.
        /// </summary>
        private static void PrepareOutputDirectory(string outDir, ILogHelper logHelper)
        {
            if (!Directory.Exists(outDir)) return;
            var entries = Directory.GetFileSystemEntries(outDir);
            if (entries.Length == 0) return;

            if (!File.Exists(Path.Combine(outDir, "index.json")))
                throw new IOException(
                    $"Diretório de split '{outDir}' não está vazio e não contém um snapshot (index.json). " +
                    "Aponte --split para um diretório novo ou para um snapshot existente.");

            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                if (string.Equals(name, "repos", StringComparison.OrdinalIgnoreCase)) continue;
                if (Directory.Exists(entry)) Directory.Delete(entry, true);
                else File.Delete(entry);
            }
            logHelper?.Info("Snapshot anterior limpo em {0} (repos/ preservada)", outDir);
        }

        private static List<ChildRef> ChildRefs(Item item, Dictionary<int, (string folder, string prefix, string path, string type)> pathById)
            => (item.Children ?? new List<Item>())
                .Where(c => c != null).Select(c => c.Id).Distinct().OrderBy(x => x)
                .Where(cid => pathById.ContainsKey(cid))
                .Select(cid => new ChildRef { id = cid, workItemType = pathById[cid].type, path = pathById[cid].path })
                .ToList();

        // Mapa de tipos conhecidos → (pasta, prefixo). Demais tipos: slug do próprio WorkItemType.
        private static (string folder, string prefix) Classify(string? workItemType)
        {
            var t = (workItemType ?? "").Trim();
            var l = t.ToLowerInvariant();
            if (l.Length == 0) return ("unknown", "WI");
            if (l.StartsWith("product backlog item")) return ("pbi", "PBI");
            if (l == "user story") return ("us", "US");
            if (l == "sprint task") return ("st", "ST");
            if (l == "spike") return ("spike", "SPIKE");
            if (l == "bug") return ("bug", "BUG");
            if (l == "task") return ("task", "TASK");
            if (l == "issue") return ("issue", "ISSUE");
            if (l == "feature") return ("feature", "FEATURE");
            if (l == "epic") return ("epic", "EPIC");
            if (l == "test case") return ("testcase", "TC");
            if (l == "impediment") return ("impediment", "IMP");
            var slug = Slug(t);
            return (slug, slug.ToUpperInvariant());
        }

        private static string Slug(string s)
        {
            var sb = new StringBuilder();
            bool lastDash = false;
            foreach (var ch in s.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch)) { sb.Append(ch); lastDash = false; }
                else if (!lastDash) { sb.Append('-'); lastDash = true; }
            }
            var r = sb.ToString().Trim('-');
            return r.Length == 0 ? "unknown" : r;
        }

        private static List<object> ExtractAssets(Item item, string assetsDir, ILogHelper? logHelper)
        {
            var result = new List<object>();
            var assets = (item.Assets ?? new List<Asset>()).Where(a => a != null).ToList();
            int n = 0;
            foreach (var a in assets)
            {
                n++;
                if (!string.IsNullOrEmpty(a.DataUri) && a.DataUri.StartsWith("data:") && a.DataUri.Contains(','))
                {
                    try
                    {
                        int comma = a.DataUri.IndexOf(',');
                        string header = a.DataUri.Substring(5, comma - 5);
                        string payload = a.DataUri.Substring(comma + 1);
                        string mime = header.Split(';')[0];
                        byte[] bytes = Convert.FromBase64String(payload);
                        string fileName = $"{item.Id}-asset-{n}.{MimeToExt(mime)}";
                        File.WriteAllBytes(Path.Combine(assetsDir, fileName), bytes);
                        result.Add(new { fileName, path = $"assets/{fileName}", contentType = mime, size = bytes.Length });
                        continue;
                    }
                    catch
                    {
                        logHelper?.Warn("Asset base64 malformado no item {0} (asset {1}) - mantido como referencia", item.Id, n);
                    }
                }
                string? safeUrl = (a.Url != null && a.Url.StartsWith("data:")) ? null : a.Url;
                result.Add(new { url = safeUrl, contentType = a.ContentType, fileName = a.FileName });
            }
            return result;
        }

        private static string MimeToExt(string mime) => mime switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "image/jpg" => "jpg",
            "image/gif" => "gif",
            "image/bmp" => "bmp",
            "image/svg+xml" => "svg",
            "image/webp" => "webp",
            _ => "bin"
        };

        private class ChildRef
        {
            public int id { get; set; }
            public string? workItemType { get; set; }
            public string path { get; set; } = "";
        }
    }
}
