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

    // ── Pull requests ──────────────────────────────────────────────────────────
    public class GitPrResult
    {
        [JsonProperty("count")] public int Count { get; set; }
        [JsonProperty("value")] public List<GitPullRequest> Value { get; set; } = new();
    }

    public class GitPullRequest
    {
        [JsonProperty("pullRequestId")] public int PullRequestId { get; set; }
        [JsonProperty("title")] public string Title { get; set; } = "";
        [JsonProperty("status")] public string? Status { get; set; }
        [JsonProperty("sourceRefName")] public string? SourceRefName { get; set; }
        [JsonProperty("targetRefName")] public string? TargetRefName { get; set; }
        [JsonProperty("createdBy")] public GitIdentityRef? CreatedBy { get; set; }
        [JsonProperty("creationDate")] public string? CreationDate { get; set; }
    }

    public class GitIdentityRef
    {
        [JsonProperty("displayName")] public string? DisplayName { get; set; }
    }

    // PR → work items (a "value" traz refs com id como STRING)
    public class ResourceRefResult
    {
        [JsonProperty("count")] public int Count { get; set; }
        [JsonProperty("value")] public List<ResourceRef> Value { get; set; } = new();
    }

    public class ResourceRef
    {
        [JsonProperty("id")] public string? Id { get; set; }
    }

    // ── Commits ────────────────────────────────────────────────────────────────
    public class GitCommitResult
    {
        [JsonProperty("count")] public int Count { get; set; }
        [JsonProperty("value")] public List<GitCommit> Value { get; set; } = new();
    }

    public class GitCommit
    {
        [JsonProperty("commitId")] public string CommitId { get; set; } = "";
        [JsonProperty("comment")] public string? Comment { get; set; }
        [JsonProperty("author")] public GitCommitUser? Author { get; set; }
    }

    public class GitCommitUser
    {
        [JsonProperty("name")] public string? Name { get; set; }
        [JsonProperty("date")] public string? Date { get; set; }
    }

    // ── Projeções gravadas pelo RepoWriter ─────────────────────────────────────
    public class PrInfo
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Status { get; set; }
        public string? SourceRefName { get; set; }
        public string? TargetRefName { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreationDate { get; set; }
        public List<int> WorkItemIds { get; set; } = new();
    }

    public class CommitInfo
    {
        public string CommitId { get; set; } = "";
        public string? Comment { get; set; }
        public string? Author { get; set; }
        public string? Date { get; set; }
    }

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
        public List<PrInfo> PullRequests { get; set; } = new();
        public List<CommitInfo> Commits { get; set; } = new();
    }
}
