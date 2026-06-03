using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HandoffExporter.Services
{
    /// <summary>
    /// Leitor read-only do snapshot de split (contraparte do <see cref="HandoffSplitter"/>).
    /// É a base do MCP server (Fase 4): cada tool é um wrapper fino sobre estes métodos.
    /// Não toca o TFS — só lê os arquivos em {exportDir}.
    /// </summary>
    public class HandoffStore
    {
        private readonly string _root;
        public HandoffStore(string exportDir) => _root = exportDir;

        public bool Exists => File.Exists(Path.Combine(_root, "index.json"));

        public string? GetIndex() => ReadIfExists(Path.Combine(_root, "index.json"));
        public string? GetPbi(int id) => ReadIfExists(Path.Combine(_root, "pbi", $"PBI-{id}.json"));
        public string? GetUs(int id) => ReadIfExists(Path.Combine(_root, "us", $"US-{id}.json"));
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

        /// <summary>Busca case-insensitive em title/description/acceptanceCriteria de PBIs e US.</summary>
        public List<SearchHit> Search(string query)
        {
            var hits = new List<SearchHit>();
            if (string.IsNullOrWhiteSpace(query)) return hits;
            SearchDir(Path.Combine(_root, "pbi"), "pbi", query, hits);
            SearchDir(Path.Combine(_root, "us"), "us", query, hits);
            return hits.OrderBy(h => h.Id).ToList();
        }

        private static void SearchDir(string dir, string type, string query, List<SearchHit> hits)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*.json").OrderBy(x => x, StringComparer.Ordinal))
            {
                JObject o;
                try { o = JObject.Parse(File.ReadAllText(f)); }
                catch { continue; }

                var hay = string.Join("\n", new[]
                {
                    (string?)o["title"], (string?)o["description"], (string?)o["acceptanceCriteria"]
                }.Where(s => !string.IsNullOrEmpty(s)));

                if (hay.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    hits.Add(new SearchHit
                    {
                        Type = type,
                        Id = (int?)o["id"] ?? 0,
                        Title = (string?)o["title"] ?? "",
                        Path = $"{type}/{Path.GetFileName(f)}"
                    });
            }
        }

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
