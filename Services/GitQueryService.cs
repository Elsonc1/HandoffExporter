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

        public async Task<List<RepoInfo>> GetRepositoriesWithBranchesAsync()
        {
            var repos = await GetRepositoriesAsync();
            var result = new List<RepoInfo>();
            foreach (var r in repos.OrderBy(r => r.Name, StringComparer.Ordinal))
            {
                var branches = await GetBranchesAsync(r.Id);
                result.Add(new RepoInfo
                {
                    Id = r.Id,
                    Name = r.Name,
                    DefaultBranch = r.DefaultBranch,
                    Size = r.Size,
                    RemoteUrl = r.RemoteUrl,
                    WebUrl = r.WebUrl,
                    Project = r.Project?.Name,
                    Branches = branches
                });
            }
            return result;
        }

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
