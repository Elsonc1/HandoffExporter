using HandoffExporter.Config;
using HandoffExporter.Logging;
using HandoffExporter.Services;
using HandoffExporter.Xml;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static HandoffExporter.Models.WorkItemVO;

namespace HandoffExporter
{
    public class HandoffJson
    {
        public Source Source { get; set; }
        public Request Request { get; set; }
        public string ExportedAtUtc { get; set; }
        public List<Item> Items { get; set; }
        public Handoff Handoff { get; set; }
    }

    public class Source
    {
        public string Type { get; set; }
        public string Collection { get; set; }
        public string Project { get; set; }
    }

    public class Request
    {
        public string AreaPath { get; set; }
        public int? PbiId { get; set; }
        public bool IncludeIssues { get; set; }
        public string Mode { get; set; }
    }

    public class Item
    {
        public int Id { get; set; }
        public string WorkItemType { get; set; }
        public string Title { get; set; }
        public string RawHtml { get; set; }
        public string SanitizedText { get; set; }
        public string State { get; set; }
        public string AcceptanceCriteria { get; set; }
        public List<Asset> Assets { get; set; }
        public List<Attachment> Attachments { get; set; }
        public List<Item> Children { get; set; }
    }

    public class Asset
    {
        public string Url { get; set; }
        public string DataUri { get; set; }
        public string ContentType { get; set; }
        public string FileName { get; set; }
        public long Size { get; set; }
        public string LocalPath { get; set; }
    }

    public class Attachment
    {
        public string Url { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long Size { get; set; }
    }

    public class Handoff
    {
        public string Version { get; set; }
        public string Generator { get; set; }
    }

    class Program
    {
        static async Task<int> Main(string[] args)
        {
            ILogHelper logHelper = new LogHelper();

            // Initialize configuration
            IXmlHelper xmlHelper = new XmlHelper();
            IConfigManager configManager;
            try
            {
                configManager = new ConfigManager(xmlHelper);
            }
            catch (Exception ex)
            {
                logHelper.Error("Failed to load configuration: {0}", ex.Message);
                return 1;
            }

            var config = configManager.GetConfig();

            string collection = null, project = null, areaPath = null, output = null, mode = "pbi";
            int? pbiId = null;
            bool includeIssues = false;
            string splitDir = null, splitFrom = null, team = null, reposProject = null;
            bool includeRepos = false;
            int reposTop = 25;

            if (args.Length == 0)
            {
                collection = config.Organization;
                project = config.Project;
                areaPath = config.AreaOrId;
                output = config.OutputFile;
                mode = !string.IsNullOrWhiteSpace(config.Mode) ? config.Mode : "pbi";
                includeIssues = false;
                pbiId = null;
                reposProject = config.ReposProject;
            }
            else
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--collection" && i + 1 < args.Length) collection = args[++i];
                    else if (args[i] == "--project" && i + 1 < args.Length) project = args[++i];
                    else if (args[i] == "--areaPath" && i + 1 < args.Length) areaPath = args[++i];
                    else if (args[i] == "--pbiId" && i + 1 < args.Length) pbiId = int.Parse(args[++i]);
                    else if (args[i] == "--includeIssues" && i + 1 < args.Length) includeIssues = bool.Parse(args[++i]);
                    else if (args[i] == "--mode" && i + 1 < args.Length) mode = args[++i];
                    else if (args[i] == "--output" && i + 1 < args.Length) output = args[++i];
                    else if (args[i] == "--split" && i + 1 < args.Length) splitDir = args[++i];
                    else if (args[i] == "--splitFrom" && i + 1 < args.Length) splitFrom = args[++i];
                    else if (args[i] == "--team" && i + 1 < args.Length) team = args[++i];
                    else if (args[i] == "--includeRepos" && i + 1 < args.Length) includeRepos = bool.Parse(args[++i]);
                    else if (args[i] == "--reposProject" && i + 1 < args.Length) reposProject = args[++i];
                    else if (args[i] == "--reposTop" && i + 1 < args.Length) int.TryParse(args[++i], out reposTop);
                }
            }

            // Fase 2b — escopo de time: --team resolve a area e o diretório de split padrão.
            if (!string.IsNullOrEmpty(team))
            {
                if (string.IsNullOrEmpty(areaPath)) areaPath = team;
                if (string.IsNullOrEmpty(splitDir)) splitDir = $"export/{team.ToLowerInvariant()}";
            }

            // Offline split: lê um HandoffJson existente e o quebra em sub-arquivos, sem chamar o TFS.
            if (!string.IsNullOrEmpty(splitFrom))
            {
                try
                {
                    var existing = JsonConvert.DeserializeObject<HandoffJson>(File.ReadAllText(splitFrom));
                    if (existing == null)
                    {
                        logHelper.Error("Split source vazio/invalido: {0}", splitFrom);
                        return 1;
                    }
                    var dir = !string.IsNullOrEmpty(splitDir) ? splitDir : "export";
                    HandoffSplitter.Split(existing, dir, logHelper);
                    return 0;
                }
                catch (Exception ex)
                {
                    logHelper.Error("Split error: {0}", ex.Message);
                    return 1;
                }
            }

            if (string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(project) || string.IsNullOrEmpty(areaPath) || string.IsNullOrEmpty(output))
            {
                Console.WriteLine("Usage: HandoffExporter [--team <name>] --collection <c> --project <p> [--areaPath <a>] [--pbiId <id>] [--includeIssues <bool>] [--mode pbi|all-artifacts] --output <file> [--split <dir>] [--includeRepos <bool>] [--reposProject <name>]");
                Console.WriteLine("   or: HandoffExporter --splitFrom <file> [--split <dir>]   (offline, sem TFS)");
                return 1;
            }

            string pat = config.Key; // from config

            var tfsService = new TFSAplicationProcess(collection, project, pat);
            var queryService = new WorkItemQueryService(tfsService, logHelper);

            var handoffJson = new HandoffJson
            {
                Source = new Source { Type = "azure-devops", Collection = collection, Project = project },
                Request = new Request { AreaPath = areaPath, PbiId = pbiId, IncludeIssues = includeIssues, Mode = mode },
                ExportedAtUtc = DateTime.UtcNow.ToString("o"),
                Items = new List<Item>(),
                Handoff = new Handoff { Version = "1.0", Generator = "HandoffExporter" }
            };

            try
            {
                if (pbiId.HasValue)
                {
                    var wi = JsonConvert.DeserializeObject<WorkItem>(tfsService.GetWorkItemAsync(pbiId.Value, "relations", "System.Id,System.Title,System.Description,System.AreaPath,System.IterationPath,System.State,System.WorkItemType,System.Tags,System.ChangedDate,ndd.DefinicoesDeNegocio,ndd.DefinicoesTecnicas", logHelper));
                    var item = await GetPBIWithChildren(tfsService, wi, logHelper);
                    handoffJson.Items.Add(item);
                }
                else if (mode == "all-artifacts")
                {
                    var allItems = await queryService.GetAllArtifactsAsync(areaPath);
                    logHelper.Info("all-artifacts: fetched {0} items total", allItems.Count);

                    // Build set of child IDs referenced via Hierarchy-Forward relations
                    var childIds = new HashSet<int>();
                    foreach (var wi in allItems)
                    {
                        if (wi.Relations == null) continue;
                        foreach (var rel in wi.Relations.Where(r => r.Rel == "System.LinkTypes.Hierarchy-Forward"))
                        {
                            var parts = rel.Url.Split('/');
                            if (int.TryParse(parts.Last(), out int cid))
                                childIds.Add(cid);
                        }
                    }

                    // Build in-memory map for dedup and child resolution without extra API calls
                    var itemMap = allItems.ToDictionary(wi => wi.Id);

                    // Root items = not a child of any other item in the result set
                    var roots = allItems.Where(wi => !childIds.Contains(wi.Id)).ToList();
                    logHelper.Info("all-artifacts: {0} root items, {1} child references", roots.Count, childIds.Count);

                    foreach (var root in roots)
                    {
                        // Each root gets its own visited set to avoid cycle guard affecting sibling trees
                        var item = BuildItemWithChildren(root, itemMap, tfsService, logHelper, new HashSet<int>());
                        handoffJson.Items.Add(item);
                    }
                }
                else
                {
                    var pbis = await queryService.GetPBIWorkItemsAsync(areaPath, null);
                    foreach (var wi in pbis)
                    {
                        var item = await GetPBIWithChildren(tfsService, wi, logHelper);
                        handoffJson.Items.Add(item);
                    }
                    if (includeIssues)
                    {
                        var issues = await queryService.GetIssueWorkItemsAsync(areaPath);
                        foreach (var wi in issues)
                        {
                            var item = CreateItem(wi, tfsService, logHelper);
                            handoffJson.Items.Add(item);
                        }
                    }
                }

                string json = JsonConvert.SerializeObject(handoffJson, Formatting.Indented);
                var outputDirectory = Path.GetDirectoryName(output);
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(output, json);
                logHelper.Info("Exported to {0}", output);

                if (!string.IsNullOrEmpty(splitDir))
                {
                    HandoffSplitter.Split(handoffJson, splitDir, logHelper);

                    // Fase 5 — repos do MacGyver vivem em outro Project (mesma Collection): NDD-DECollection/Integrações.
                    if (includeRepos)
                    {
                        var rp = !string.IsNullOrEmpty(reposProject) ? reposProject : "Integrações";
                        var git = new GitQueryService(collection, rp, tfsService._httpClient, logHelper);
                        var repos = await git.GetRepositoriesFullAsync(includePrs: true, includeCommits: true, top: reposTop);
                        RepoWriter.Write(repos, splitDir, logHelper);
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                logHelper.Error("Error: {0}", ex.Message);
                return 1;
            }
        }

        static Item BuildItemWithChildren(WorkItem wi, Dictionary<int, WorkItem> itemMap, TFSAplicationProcess tfsService, ILogHelper logHelper, HashSet<int> visited)
        {
            var item = CreateItem(wi, tfsService, logHelper);
            item.Children = new List<Item>();

            if (visited.Contains(wi.Id))
                return item; // cycle guard
            visited.Add(wi.Id);

            if (wi.Relations != null)
            {
                foreach (var rel in wi.Relations.Where(r => r.Rel == "System.LinkTypes.Hierarchy-Forward"))
                {
                    var parts = rel.Url.Split('/');
                    if (!int.TryParse(parts.Last(), out int childId)) continue;
                    if (!itemMap.TryGetValue(childId, out var childWi))
                    {
                        logHelper.Info($"Child {childId} of {wi.Id} not in current fetch scope (may be outside area), skipping.");
                        continue;
                    }
                    var childItem = BuildItemWithChildren(childWi, itemMap, tfsService, logHelper, visited);
                    item.Children.Add(childItem);
                }
            }

            return item;
        }

        static async Task<Item> GetPBIWithChildren(TFSAplicationProcess tfsService, WorkItem pbi, ILogHelper logHelper)
        {
            var item = CreateItem(pbi, tfsService, logHelper);
            item.Children = new List<Item>();

            if (pbi.Relations != null)
            {
                logHelper.Info($"PBI {pbi.Id} has {pbi.Relations.Count} relations");
                foreach (var rel in pbi.Relations.Where(r => r.Rel == "System.LinkTypes.Hierarchy-Forward"))
                {
                    // Extract ID from URL
                    var urlParts = rel.Url.Split('/');
                    int childId = int.Parse(urlParts.Last());
                    var childJson = tfsService.GetWorkItemAsync(childId, null, "System.Id,System.Title,System.Description,Microsoft.VSTS.Common.AcceptanceCriteria,System.AreaPath,System.IterationPath,System.State,System.WorkItemType,System.Tags,System.ChangedDate,ndd.DefinicoesDeNegocio,ndd.DefinicoesTecnicas", logHelper);
                    if (childJson.StartsWith("{"))
                    {
                        var child = JsonConvert.DeserializeObject<WorkItem>(childJson);
                        var childItem = CreateItem(child, tfsService, logHelper);
                        // The resolver in CreateItem now handles the content
                        item.Children.Add(childItem);
                    }
                    else
                    {
                        logHelper.Error($"Failed to fetch child work item {childId}: {childJson}");
                    }
                }
                logHelper.Info($"PBI {pbi.Id} has {item.Children.Count} User Stories");
            }
            else
            {
                logHelper.Info($"PBI {pbi.Id} has no relations");
            }

            return item;
        }

        static (string rawHtml, string sanitizedText) ResolveContent(WorkItem wi, TFSAplicationProcess tfsService, ILogHelper logHelper)
        {
            string rawHtml = null;
            string sanitizedText = null;

            // For User Stories, prefer ndd.DefinicoesDeNegocio, fallback to System.Description
            if (wi.Fields?.ContainsKey("System.WorkItemType") == true && wi.Fields["System.WorkItemType"].ToString() == "User Story")
            {
                if (wi.Fields.ContainsKey("ndd.DefinicoesDeNegocio") && !string.IsNullOrEmpty(wi.Fields["ndd.DefinicoesDeNegocio"].ToString()))
                {
                    rawHtml = wi.Fields["ndd.DefinicoesDeNegocio"].ToString();
                    sanitizedText = tfsService.ExtractTextFromHtml(rawHtml);
                }
                else if (wi.Fields.ContainsKey("System.Description") && !string.IsNullOrEmpty(wi.Fields["System.Description"].ToString()))
                {
                    rawHtml = wi.Fields["System.Description"].ToString();
                    sanitizedText = tfsService.ExtractTextFromHtml(rawHtml);
                }
                else
                {
                    // Fields not present, fetch individually
                    var fieldsJson = tfsService.GetWorkItemAsync(wi.Id, null, "ndd.DefinicoesDeNegocio,System.Description", logHelper);
                    if (fieldsJson.StartsWith("{"))
                    {
                        var fieldsWi = JsonConvert.DeserializeObject<WorkItem>(fieldsJson);
                        if (fieldsWi?.Fields != null)
                        {
                            if (fieldsWi.Fields.ContainsKey("ndd.DefinicoesDeNegocio") && !string.IsNullOrEmpty(fieldsWi.Fields["ndd.DefinicoesDeNegocio"].ToString()))
                            {
                                rawHtml = fieldsWi.Fields["ndd.DefinicoesDeNegocio"].ToString();
                                sanitizedText = tfsService.ExtractTextFromHtml(rawHtml);
                            }
                            else if (fieldsWi.Fields.ContainsKey("System.Description") && !string.IsNullOrEmpty(fieldsWi.Fields["System.Description"].ToString()))
                            {
                                rawHtml = fieldsWi.Fields["System.Description"].ToString();
                                sanitizedText = tfsService.ExtractTextFromHtml(rawHtml);
                            }
                        }
                    }
                }
            }
            else
            {
                // For other work item types, use System.Description
                if (wi.Fields?.ContainsKey("System.Description") == true && !string.IsNullOrEmpty(wi.Fields["System.Description"].ToString()))
                {
                    rawHtml = wi.Fields["System.Description"].ToString();
                    sanitizedText = tfsService.ExtractTextFromHtml(rawHtml);
                }
                else
                {
                    // Fetch individually if needed
                    var fieldsJson = tfsService.GetWorkItemAsync(wi.Id, null, "System.Description", logHelper);
                    if (fieldsJson.StartsWith("{"))
                    {
                        var fieldsWi = JsonConvert.DeserializeObject<WorkItem>(fieldsJson);
                        if (fieldsWi?.Fields?.ContainsKey("System.Description") == true && !string.IsNullOrEmpty(fieldsWi.Fields["System.Description"].ToString()))
                        {
                            rawHtml = fieldsWi.Fields["System.Description"].ToString();
                            sanitizedText = tfsService.ExtractTextFromHtml(rawHtml);
                        }
                    }
                }
            }

            return (rawHtml, sanitizedText);
        }

        static string ResolveAcceptanceCriteria(WorkItem wi, TFSAplicationProcess tfsService)
        {
            string GetField(string key) =>
                wi.Fields?.ContainsKey(key) == true && !string.IsNullOrEmpty(wi.Fields[key]?.ToString())
                    ? wi.Fields[key].ToString() : null;

            // US guarda critérios em ndd.DefinicoesTecnicas; fallback para o campo padrão.
            var raw = GetField("ndd.DefinicoesTecnicas") ?? GetField("Microsoft.VSTS.Common.AcceptanceCriteria");
            return raw != null ? tfsService.ExtractTextFromHtml(raw) : null;
        }

        static Item CreateItem(WorkItem wi, TFSAplicationProcess tfsService, ILogHelper logHelper)
        {
            var (rawHtml, sanitizedText) = ResolveContent(wi, tfsService, logHelper);

            var item = new Item
            {
                Id = wi.Id,
                WorkItemType = wi.Fields?.ContainsKey("System.WorkItemType") == true ? wi.Fields["System.WorkItemType"].ToString() : null,
                Title = wi.Fields?.ContainsKey("System.Title") == true ? wi.Fields["System.Title"].ToString() : null,
                RawHtml = rawHtml,
                SanitizedText = sanitizedText,
                State = wi.Fields?.ContainsKey("System.State") == true ? wi.Fields["System.State"]?.ToString() : null,
                AcceptanceCriteria = ResolveAcceptanceCriteria(wi, tfsService),
                Assets = new List<Asset>(),
                Attachments = new List<Attachment>(),
                Children = new List<Item>()
            };

            // Parse assets from RawHtml
            if (!string.IsNullOrEmpty(item.RawHtml))
            {
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(item.RawHtml);
                var imgs = doc.DocumentNode.SelectNodes("//img");
                if (imgs != null)
                {
                    foreach (var img in imgs)
                    {
                        var src = img.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(src))
                        {
                            item.Assets.Add(new Asset { Url = src, DataUri = src.StartsWith("data:") ? src : null, ContentType = "image", FileName = "image" });
                        }
                    }
                }
            }

            // Attachments from relations
            if (wi.Relations != null)
            {
                foreach (var rel in wi.Relations)
                {
                    if (rel.Rel == "AttachedFile")
                    {
                        item.Attachments.Add(new Attachment { Url = rel.Url, FileName = "attachment", ContentType = "file" });
                    }
                }
            }

            return item;
        }
    }
}