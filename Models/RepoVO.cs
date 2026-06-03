using Newtonsoft.Json;
using System.Collections.Generic;

namespace HandoffExporter.Models
{
    // ── DTOs da REST API Git (ADO Server 2022.2) ──────────────────────────────
    public class GitRepoResult
    {
        [JsonProperty("count")] public int Count { get; set; }
        [JsonProperty("value")] public List<GitRepo> Value { get; set; } = new();
    }

    public class GitRepo
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("defaultBranch")] public string? DefaultBranch { get; set; }
        [JsonProperty("size")] public long Size { get; set; }
        [JsonProperty("remoteUrl")] public string? RemoteUrl { get; set; }
        [JsonProperty("webUrl")] public string? WebUrl { get; set; }
        [JsonProperty("project")] public GitProjectRef? Project { get; set; }
    }

    public class GitProjectRef
    {
        [JsonProperty("name")] public string? Name { get; set; }
    }

    public class GitRefResult
    {
        [JsonProperty("count")] public int Count { get; set; }
        [JsonProperty("value")] public List<GitRef> Value { get; set; } = new();
    }

    public class GitRef
    {
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("objectId")] public string? ObjectId { get; set; }
    }

    // ── Projeção que o RepoWriter grava (independe do shape da API) ────────────
    public class RepoInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? DefaultBranch { get; set; }
        public long Size { get; set; }
        public string? RemoteUrl { get; set; }
        public string? WebUrl { get; set; }
        public string? Project { get; set; }
        public List<string> Branches { get; set; } = new();
    }
}
