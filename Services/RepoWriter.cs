using HandoffExporter.Logging;
using HandoffExporter.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HandoffExporter.Services
{
    /// <summary>
    /// Escreve os repositórios segmentados sob {outDir}/repos/ :
    ///   repos/index.json            catálogo {name, path, defaultBranch}
    ///   repos/&lt;name&gt;/repo.json     metadata do repo + branches
    /// Puro (sem HTTP) e determinístico (ordenado por nome) — fácil de testar.
    /// </summary>
    public static class RepoWriter
    {
        public static void Write(IReadOnlyList<RepoInfo>? repos, string outDir, ILogHelper? logHelper = null)
        {
            if (repos == null) return;

            string reposDir = Path.Combine(outDir, "repos");
            Directory.CreateDirectory(reposDir);

            var indexEntries = new List<object>();
            foreach (var r in repos.OrderBy(r => r.Name, System.StringComparer.Ordinal))
            {
                string safe = SafeName(r.Name);
                string repoDir = Path.Combine(reposDir, safe);
                Directory.CreateDirectory(repoDir);

                var node = new
                {
                    id = r.Id,
                    name = r.Name,
                    project = r.Project,
                    defaultBranch = r.DefaultBranch,
                    size = r.Size,
                    remoteUrl = r.RemoteUrl,
                    webUrl = r.WebUrl,
                    branches = (r.Branches ?? new List<string>()).OrderBy(b => b, System.StringComparer.Ordinal).ToList()
                };
                File.WriteAllText(Path.Combine(repoDir, "repo.json"),
                    JsonConvert.SerializeObject(node, Formatting.Indented));

                indexEntries.Add(new { name = r.Name, path = $"repos/{safe}/repo.json", defaultBranch = r.DefaultBranch });
            }

            File.WriteAllText(Path.Combine(reposDir, "index.json"),
                JsonConvert.SerializeObject(new { count = indexEntries.Count, repos = indexEntries }, Formatting.Indented));

            logHelper?.Info("Repos: {0} repositorio(s) -> {1}", repos.Count, reposDir);
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "repo";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '-');
            return name.Trim();
        }
    }
}
