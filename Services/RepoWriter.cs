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
    ///   repos/index.json                 catálogo {name, path, defaultBranch, pullRequests, commits}
    ///   repos/&lt;name&gt;/repo.json          metadata do repo + branches
    ///   repos/&lt;name&gt;/pull-requests.json  PRs + workItemIds (quando houver)
    ///   repos/&lt;name&gt;/commits.json        últimos commits (quando houver)
    ///   repos/links.json                 JOIN work item ↔ repo (via PR): [{workItemId,repo,prId,prTitle}]
    /// Puro (sem HTTP) e determinístico (ordenado) — fácil de testar.
    /// </summary>
    public static class RepoWriter
    {
        public static void Write(IReadOnlyList<RepoInfo>? repos, string outDir, ILogHelper? logHelper = null)
        {
            if (repos == null) return;

            string reposDir = Path.Combine(outDir, "repos");

            // Limpa o snapshot anterior de repos (stale). Guarda: só apaga se for snapshot nosso.
            if (Directory.Exists(reposDir) && Directory.GetFileSystemEntries(reposDir).Length > 0)
            {
                if (!File.Exists(Path.Combine(reposDir, "index.json")))
                    throw new IOException(
                        $"'{reposDir}' não está vazio e não contém um snapshot de repos (index.json). Abortando para não apagar dados alheios.");
                Directory.Delete(reposDir, true);
            }
            Directory.CreateDirectory(reposDir);

            var indexEntries = new List<object>();
            var links = new List<(int workItemId, string repo, int prId, string prTitle)>();

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
                File.WriteAllText(Path.Combine(repoDir, "repo.json"), JsonConvert.SerializeObject(node, Formatting.Indented));

                var prs = (r.PullRequests ?? new List<PrInfo>()).OrderBy(p => p.Id).ToList();
                if (prs.Count > 0)
                {
                    var prNodes = prs.Select(p => new
                    {
                        id = p.Id,
                        title = p.Title,
                        status = p.Status,
                        sourceRefName = p.SourceRefName,
                        targetRefName = p.TargetRefName,
                        createdBy = p.CreatedBy,
                        creationDate = p.CreationDate,
                        workItemIds = (p.WorkItemIds ?? new List<int>()).OrderBy(x => x).ToList()
                    }).ToList();
                    File.WriteAllText(Path.Combine(repoDir, "pull-requests.json"),
                        JsonConvert.SerializeObject(new { count = prNodes.Count, pullRequests = prNodes }, Formatting.Indented));

                    foreach (var p in prs)
                        foreach (var wid in (p.WorkItemIds ?? new List<int>()))
                            links.Add((wid, r.Name, p.Id, p.Title));
                }

                var commits = (r.Commits ?? new List<CommitInfo>()).ToList();
                if (commits.Count > 0)
                {
                    var commitNodes = commits.Select(c => new { commitId = c.CommitId, comment = c.Comment, author = c.Author, date = c.Date }).ToList();
                    File.WriteAllText(Path.Combine(repoDir, "commits.json"),
                        JsonConvert.SerializeObject(new { count = commitNodes.Count, commits = commitNodes }, Formatting.Indented));
                }

                indexEntries.Add(new { name = r.Name, path = $"repos/{safe}/repo.json", defaultBranch = r.DefaultBranch, pullRequests = prs.Count, commits = commits.Count });
            }

            File.WriteAllText(Path.Combine(reposDir, "index.json"),
                JsonConvert.SerializeObject(new { count = indexEntries.Count, repos = indexEntries }, Formatting.Indented));

            // links.json — o join work item ↔ repo (via PR). Ordenado p/ determinismo.
            var linkNodes = links
                .OrderBy(l => l.workItemId).ThenBy(l => l.repo, System.StringComparer.Ordinal).ThenBy(l => l.prId)
                .Select(l => new { workItemId = l.workItemId, repo = l.repo, prId = l.prId, prTitle = l.prTitle })
                .ToList();
            File.WriteAllText(Path.Combine(reposDir, "links.json"),
                JsonConvert.SerializeObject(new { count = linkNodes.Count, links = linkNodes }, Formatting.Indented));

            logHelper?.Info("Repos: {0} repositorio(s), {1} vinculo(s) work-item-PR -> {2}", repos.Count, linkNodes.Count, reposDir);
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "repo";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
            return name.Trim();
        }
    }
}
