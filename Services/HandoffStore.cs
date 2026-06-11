using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HandoffExporter.Services
{
    /// <summary>
    /// Leitor read-only do snapshot de split (contraparte do <see cref="HandoffSplitter"/>).
    /// Os itens vivem em pastas por WorkItemType; o id→path é resolvido via `items` do index.json.
    /// Base do MCP server (Fase 4). Não toca o TFS.
    /// </summary>
    public class HandoffStore
    {
        private readonly string _root;
        private Dictionary<int, string>? _idToPath;

        public HandoffStore(string exportDir) => _root = exportDir;

        public bool Exists => File.Exists(Path.Combine(_root, "index.json"));

        public string? GetIndex() => ReadIfExists(Path.Combine(_root, "index.json"));

        /// <summary>Qualquer work item por id (PBI, US, Sprint Task, Spike, etc.), resolvido via index.</summary>
        public string? GetItem(int id)
        {
            var map = IdToPath();
            return map.TryGetValue(id, out var rel) ? ReadIfExists(Resolve(rel)) : null;
        }

        public string? GetReposIndex() => ReadIfExists(Path.Combine(_root, "repos", "index.json"));
        public string? GetRepo(string name) => ReadIfExists(Path.Combine(_root, "repos", SafeName(name), "repo.json"));
        public string? GetLinks() => ReadIfExists(Path.Combine(_root, "repos", "links.json"));

        /// <summary>Vínculos (via PR) de um work item: array [{workItemId, repo, prId, prTitle}].</summary>
        public string GetLinksForWorkItem(int workItemId)
        {
            var raw = GetLinks();
            if (raw == null) return "[]";
            try
            {
                var arr = (JArray?)JObject.Parse(raw)["links"] ?? new JArray();
                return new JArray(arr.Where(l => (int?)l["workItemId"] == workItemId)).ToString();
            }
            catch { return "[]"; }
        }

        /// <summary>Busca case-insensitive em title/description/acceptanceCriteria de TODOS os itens (qualquer tipo).</summary>
        public List<SearchHit> Search(string query)
        {
            var hits = new List<SearchHit>();
            if (string.IsNullOrWhiteSpace(query)) return hits;

            foreach (var kv in IdToPath().OrderBy(k => k.Key))
            {
                var full = Resolve(kv.Value);
                if (!File.Exists(full)) continue;
                JObject o;
                try { o = JObject.Parse(File.ReadAllText(full)); }
                catch { continue; }

                var hay = string.Join("\n", new[]
                {
                    (string?)o["title"], (string?)o["description"], (string?)o["acceptanceCriteria"]
                }.Where(s => !string.IsNullOrEmpty(s)));

                if (o["contentFields"] is JObject cf)
                    hay += "\n" + string.Join("\n", cf.Properties().Select(p => (string?)p.Value).Where(s => !string.IsNullOrEmpty(s)));

                if (hay.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    hits.Add(new SearchHit
                    {
                        Type = (string?)o["workItemType"] ?? "",
                        Id = (int?)o["id"] ?? 0,
                        Title = (string?)o["title"] ?? "",
                        Path = kv.Value
                    });
            }
            return hits;
        }

        // id→path (relativo à raiz) a partir do `items` do index.json. Cacheado (snapshot estático na sessão).
        private Dictionary<int, string> IdToPath()
        {
            if (_idToPath != null) return _idToPath;
            var map = new Dictionary<int, string>();
            var idx = GetIndex();
            if (idx != null)
            {
                try
                {
                    foreach (var it in (JArray?)JObject.Parse(idx)["items"] ?? new JArray())
                    {
                        var id = (int?)it["id"];
                        var p = (string?)it["path"];
                        if (id.HasValue && p != null) map[id.Value] = p;
                    }
                }
                catch { /* index inválido → mapa vazio */ }
            }
            _idToPath = map;
            return map;
        }

        private string Resolve(string relPath) => Path.Combine(_root, Path.Combine(relPath.Split('/')));
        private static string? ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        private static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "repo";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
            return name.Trim();
        }
    }

    public class SearchHit
    {
        public string Type { get; set; } = "";
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Path { get; set; } = "";
    }
}
