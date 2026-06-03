using HandoffExporter.Logging;
using HandoffExporter.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace HandoffExporter.Services
{
    /// <summary>
    /// Lê repositórios Git de um Project da MESMA Collection (multi-project), reaproveitando
    /// o HttpClient autenticado do <see cref="TFSAplicationProcess"/>. Ex.: work items vivem
    /// em NDD-DECollection/Central de Soluções e os repos em NDD-DECollection/Integrações.
    /// </summary>
    public class GitQueryService
    {
        private const string Host = "https://tfs.ndd.tech";
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly ILogHelper? _log;

        public GitQueryService(string collection, string project, HttpClient httpClient, ILogHelper? logHelper = null)
        {
            _http = httpClient;
            _log = logHelper;
            _baseUrl = BuildBaseUrl(collection, project);
        }

        /// <summary>Monta a base URL do project, codificando o segmento (ex.: "Integrações" → "Integra%C3%A7%C3%B5es").</summary>
        public static string BuildBaseUrl(string collection, string project) =>
            $"{Host}/{collection}/{Uri.EscapeDataString(project)}";

        /// <summary>Repos + branches + (opcional) PRs e commits — o pacote completo p/ o snapshot.</summary>
        public async Task<List<RepoInfo>> GetRepositoriesFullAsync(bool includePrs = true, bool includeCommits = true, int top = 25)
        {
            var repos = await GetRepositoriesAsync();
            var result = new List<RepoInfo>();
            foreach (var r in repos.OrderBy(r => r.Name, StringComparer.Ordinal))
            {
                var info = new RepoInfo
                {
                    Id = r.Id,
                    Name = r.Name,
                    DefaultBranch = r.DefaultBranch,
                    Size = r.Size,
                    RemoteUrl = r.RemoteUrl,
                    WebUrl = r.WebUrl,
                    Project = r.Project?.Name,
                    Branches = await GetBranchesAsync(r.Id)
                };
                if (includePrs) info.PullRequests = await GetPullRequestsAsync(r.Id, top);
                if (includeCommits) info.Commits = await GetCommitsAsync(r.Id, top);
                result.Add(info);
            }
            return result;
        }

        public async Task<List<RepoInfo>> GetRepositoriesWithBranchesAsync()
            => await GetRepositoriesFullAsync(includePrs: false, includeCommits: false);

        public async Task<List<GitRepo>> GetRepositoriesAsync()
        {
            var json = await GetAsync($"{_baseUrl}/_apis/git/repositories?api-version=6.0");
            var parsed = JsonConvert.DeserializeObject<GitRepoResult>(json);
            _log?.Info("git: {0} repositorio(s) em {1}", parsed?.Value?.Count ?? 0, _baseUrl);
            return parsed?.Value ?? new List<GitRepo>();
        }

        public async Task<List<string>> GetBranchesAsync(string repoId)
        {
            var json = await GetAsync($"{_baseUrl}/_apis/git/repositories/{repoId}/refs?filter=heads&api-version=6.0");
            var parsed = JsonConvert.DeserializeObject<GitRefResult>(json);
            return (parsed?.Value ?? new List<GitRef>())
                .Select(r => r.Name.StartsWith("refs/heads/") ? r.Name.Substring("refs/heads/".Length) : r.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>PRs (todos os status) + os work items vinculados a cada um (o "join" pedido).</summary>
        public async Task<List<PrInfo>> GetPullRequestsAsync(string repoId, int top = 25)
        {
            var json = await GetAsync($"{_baseUrl}/_apis/git/repositories/{repoId}/pullrequests?searchCriteria.status=all&$top={top}&api-version=6.0");
            var parsed = JsonConvert.DeserializeObject<GitPrResult>(json);
            var result = new List<PrInfo>();
            foreach (var pr in parsed?.Value ?? new List<GitPullRequest>())
            {
                result.Add(new PrInfo
                {
                    Id = pr.PullRequestId,
                    Title = pr.Title,
                    Status = pr.Status,
                    SourceRefName = pr.SourceRefName,
                    TargetRefName = pr.TargetRefName,
                    CreatedBy = pr.CreatedBy?.DisplayName,
                    CreationDate = pr.CreationDate,
                    WorkItemIds = await GetPrWorkItemsAsync(repoId, pr.PullRequestId)
                });
            }
            return result.OrderBy(p => p.Id).ToList();
        }

        public async Task<List<int>> GetPrWorkItemsAsync(string repoId, int prId)
        {
            var json = await GetAsync($"{_baseUrl}/_apis/git/repositories/{repoId}/pullRequests/{prId}/workitems?api-version=6.0");
            var parsed = JsonConvert.DeserializeObject<ResourceRefResult>(json);
            return (parsed?.Value ?? new List<ResourceRef>())
                .Select(r => int.TryParse(r.Id, out var n) ? n : 0)
                .Where(n => n > 0)
                .OrderBy(n => n)
                .ToList();
        }

        public async Task<List<CommitInfo>> GetCommitsAsync(string repoId, int top = 25)
        {
            var json = await GetAsync($"{_baseUrl}/_apis/git/repositories/{repoId}/commits?searchCriteria.$top={top}&api-version=6.0");
            var parsed = JsonConvert.DeserializeObject<GitCommitResult>(json);
            return (parsed?.Value ?? new List<GitCommit>())
                .Select(c => new CommitInfo
                {
                    CommitId = c.CommitId,
                    Comment = c.Comment,
                    Author = c.Author?.Name,
                    Date = c.Author?.Date
                })
                .ToList();
        }

        private async Task<string> GetAsync(string url)
        {
            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                _log?.Error("git GET {0} -> {1}", url, resp.StatusCode);
                return "{}";
            }
            return await resp.Content.ReadAsStringAsync();
        }
    }
}
