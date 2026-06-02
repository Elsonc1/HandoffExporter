using HandoffExporter.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static HandoffExporter.Models.WorkItemVO;

namespace HandoffExporter.Services
{
    public class WorkItemQueryService
    {
        private readonly TFSAplicationProcess _tfsService;
        private readonly ILogHelper _logHelper;

        public WorkItemQueryService(TFSAplicationProcess tfsService, ILogHelper logHelper)
        {
            _tfsService = tfsService;
            _logHelper = logHelper;
        }

        public string GetWorkItemsByStateAsync(List<string> areas, string workItemTypeName)
        {
            try
            {
                string areaCondition = string.Join(", ", areas.Select(area => $"'Central de Soluções\\{area}'"));

                if (areas == null || areas.Count == 0)
                {
                    _logHelper.Error("Areas listadas não podem ser nulas ou vazias");
                    return string.Empty;
                }

                var query = new
                {
                    query = $"SELECT [System.Id], [System.Title], [System.State] FROM WorkItems WHERE [System.State] IN ('In Test', 'In Development', 'New', 'Code Review', 'Awaiting Test', 'Awaiting Code Review') AND [System.AreaPath] IN ({areaCondition}) AND [System.WorkItemType] = '{workItemTypeName}'"
                };

                if (workItemTypeName == "Issue")
                {
                    query = new
                    {
                        query = $"SELECT [System.Id], [System.Title], [System.State] FROM WorkItems WHERE [System.State] IN ('New', 'Awaiting Analysis') AND [System.AreaPath] IN ({areaCondition}) AND [System.WorkItemType] = '{workItemTypeName}'"
                    };
                }

                var url = $"{_tfsService._baseUrl}/_apis/wit/wiql?api-version=6.0";
                var content = new StringContent(JsonConvert.SerializeObject(query), Encoding.UTF8, "application/json");

                var response = _tfsService._httpClient.PostAsync(url, content);

                if (response.Result.StatusCode != HttpStatusCode.OK)
                {
                    _logHelper.Info($"Não foi possivel acessar ou comunicar com o TFS: {response.Result.StatusCode}");

                    return response.Result.StatusCode.ToString();
                }

                string result = response.Result.Content.ReadAsStringAsync().Result;

                return result;
            }
            catch (Exception ex)
            {
                _logHelper.Error($"Erro ao buscar work items: {ex.Message} {ex}");
                return string.Empty;
            }
        }

        public async Task<List<WorkItem>> GetPBIWorkItemsAsync(string areaCondition, List<int> listIds)
        {
            try
            {
                dynamic query;

                if (listIds != null && listIds.Any())
                {
                    string textCondition = string.Join(", ", listIds.Select(tc => $"'{tc}'"));

                    query = new { query = $@"SELECT [System.Id], [System.Title]  
                                FROM WorkItems  
                                WHERE  
                                    [System.Id] IN ({textCondition})  
                                ORDER BY [System.ChangedDate] DESC" };
                }
                else if (areaCondition == "ALL")
                {
                    query = new { query = $@"SELECT [System.Id], [System.Title]  
                                FROM WorkItems  
                                WHERE  
                                    [System.WorkItemType] IN ('Product Backlog Item', 'Product Backlog Item Desenvolvimento', 'Product Backlog Item Municipio', 'Product Backlog Item Habilitacao', 'Request Produto', 'Product Backlog Item Pesquisa', 'Product Backlog Item Compliance')  
                                ORDER BY [System.ChangedDate] DESC" };
                }
                else
                {
                    query = new { query = $@"SELECT [System.Id], [System.Title]  
                                FROM WorkItems  
                                WHERE  
                                    [System.AreaPath] = 'Central de Soluções\{areaCondition}'  
                                AND [System.WorkItemType] IN ('Product Backlog Item', 'Product Backlog Item Desenvolvimento', 'Product Backlog Item Municipio', 'Product Backlog Item Habilitacao', 'Request Produto', 'Product Backlog Item Pesquisa', 'Product Backlog Item Compliance')  
                                ORDER BY [System.ChangedDate] DESC" };
                }

                _logHelper.Info($"WIQL for area '{areaCondition}': {query.query}");

                var url = $"{_tfsService._baseUrl}/_apis/wit/wiql?api-version=6.0";
                var content = new StringContent(JsonConvert.SerializeObject(query), Encoding.UTF8, "application/json");

                var response = await _tfsService._httpClient.PostAsync(url, content);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logHelper.Info($"Não foi possível acessar ou comunicar com o TFS: {response.StatusCode}");
                    return new List<WorkItem>();
                }

                string result = await response.Content.ReadAsStringAsync();

                var wiqlResult = JsonConvert.DeserializeObject<WiqlResult>(result);

                _logHelper.Info($"WIQL returned {wiqlResult.WorkItems.Length} WorkItemReferences for area '{areaCondition}'");

                if (wiqlResult.WorkItems.Length == 0)
                {
                    return new List<WorkItem>();
                }

                var ids = wiqlResult.WorkItems.Select(w => w.Id).ToArray();

                var workItems = new List<WorkItem>();
                int batchSize = 100;
                for (int i = 0; i < ids.Length; i += batchSize)
                {
                    var batchIds = ids.Skip(i).Take(batchSize).ToArray();
                    var workItemsUrl = $"{_tfsService._baseUrl}/_apis/wit/workitems?ids={string.Join(",", batchIds)}&api-version=6.0&$expand=relations";

                    var workItemsResponse = await _tfsService._httpClient.GetAsync(workItemsUrl);

                    if (workItemsResponse.StatusCode != HttpStatusCode.OK)
                    {
                        _logHelper.Info($"Erro ao buscar detalhes dos work items (batch {i / batchSize + 1}): {workItemsResponse.StatusCode}");
                        continue; // or return empty, but let's try to get as many as possible
                    }

                    string workItemsResult = await workItemsResponse.Content.ReadAsStringAsync();

                    var workItemResult = JsonConvert.DeserializeObject<WorkItemResult>(workItemsResult);
                    if (workItemResult?.WorkItems != null)
                    {
                        workItems.AddRange(workItemResult.WorkItems);
                    }
                }

                return workItems;
            }
            catch (Exception ex)
            {
                _logHelper.Error($"Erro ao buscar work items: {ex.Message}");
                return new List<WorkItem>();
            }
        }

        public async Task<List<WorkItem>> GetAllArtifactsAsync(string areaCondition)
        {
            try
            {
                var query = new { query = $@"SELECT [System.Id], [System.Title]
                                FROM WorkItems
                                WHERE
                                    [System.AreaPath] = 'Central de Soluções\{areaCondition}'
                                ORDER BY [System.ChangedDate] DESC" };

                _logHelper.Info($"WIQL all-artifacts for area '{areaCondition}': {query.query}");

                var url = $"{_tfsService._baseUrl}/_apis/wit/wiql?api-version=6.0";
                var content = new StringContent(JsonConvert.SerializeObject(query), Encoding.UTF8, "application/json");

                var response = await _tfsService._httpClient.PostAsync(url, content);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logHelper.Info($"Não foi possível acessar ou comunicar com o TFS (all-artifacts): {response.StatusCode}");
                    return new List<WorkItem>();
                }

                string result = await response.Content.ReadAsStringAsync();
                var wiqlResult = JsonConvert.DeserializeObject<WiqlResult>(result);
                _logHelper.Info($"WIQL all-artifacts returned {wiqlResult.WorkItems.Length} items for area '{areaCondition}'");

                if (wiqlResult.WorkItems.Length == 0)
                    return new List<WorkItem>();

                var ids = wiqlResult.WorkItems.Select(w => w.Id).ToArray();

                var workItems = new List<WorkItem>();
                int batchSize = 100;
                for (int i = 0; i < ids.Length; i += batchSize)
                {
                    var batchIds = ids.Skip(i).Take(batchSize).ToArray();
                    var workItemsUrl = $"{_tfsService._baseUrl}/_apis/wit/workitems?ids={string.Join(",", batchIds)}&api-version=6.0&$expand=relations";

                    var workItemsResponse = await _tfsService._httpClient.GetAsync(workItemsUrl);
                    if (workItemsResponse.StatusCode != HttpStatusCode.OK)
                    {
                        _logHelper.Info($"Erro ao buscar all-artifacts batch {i / batchSize + 1}: {workItemsResponse.StatusCode}");
                        continue;
                    }

                    string workItemsResult = await workItemsResponse.Content.ReadAsStringAsync();
                    var workItemResult = JsonConvert.DeserializeObject<WorkItemResult>(workItemsResult);
                    if (workItemResult?.WorkItems != null)
                        workItems.AddRange(workItemResult.WorkItems);
                }

                return workItems;
            }
            catch (Exception ex)
            {
                _logHelper.Error($"Erro ao buscar all-artifacts: {ex.Message}");
                return new List<WorkItem>();
            }
        }

        public async Task<List<WorkItem>> GetIssueWorkItemsAsync(string areaCondition)
        {
            try
            {
                var query = new { query = $@"SELECT [System.Id], [System.Title]  
                                FROM WorkItems  
                                WHERE  
                                    [System.AreaPath] = 'Central de Soluções\{areaCondition}'  
                                AND [System.WorkItemType] = 'Issue'  
                                ORDER BY [System.ChangedDate] DESC" };

                _logHelper.Info($"WIQL for issues in area '{areaCondition}': {query.query}");

                var url = $"{_tfsService._baseUrl}/_apis/wit/wiql?api-version=6.0";
                var content = new StringContent(JsonConvert.SerializeObject(query), Encoding.UTF8, "application/json");

                var response = await _tfsService._httpClient.PostAsync(url, content);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logHelper.Info($"Não foi possível acessar ou comunicar com o TFS: {response.StatusCode}");
                    return new List<WorkItem>();
                }

                string result = await response.Content.ReadAsStringAsync();

                var wiqlResult = JsonConvert.DeserializeObject<WiqlResult>(result);

                _logHelper.Info($"WIQL returned {wiqlResult.WorkItems.Length} Issue WorkItemReferences for area '{areaCondition}'");

                if (wiqlResult.WorkItems.Length == 0)
                {
                    return new List<WorkItem>();
                }

                var ids = wiqlResult.WorkItems.Select(w => w.Id).ToArray();

                var workItems = new List<WorkItem>();
                int batchSize = 100;
                for (int i = 0; i < ids.Length; i += batchSize)
                {
                    var batchIds = ids.Skip(i).Take(batchSize).ToArray();
                    var workItemsUrl = $"{_tfsService._baseUrl}/_apis/wit/workitems?ids={string.Join(",", batchIds)}&api-version=6.0&$expand=relations";

                    var workItemsResponse = await _tfsService._httpClient.GetAsync(workItemsUrl);

                    if (workItemsResponse.StatusCode != HttpStatusCode.OK)
                    {
                        _logHelper.Info($"Erro ao buscar detalhes dos issues (batch {i / batchSize + 1}): {workItemsResponse.StatusCode}");
                        continue;
                    }

                    string workItemsResult = await workItemsResponse.Content.ReadAsStringAsync();

                    var workItemResult = JsonConvert.DeserializeObject<WorkItemResult>(workItemsResult);
                    if (workItemResult?.WorkItems != null)
                    {
                        workItems.AddRange(workItemResult.WorkItems);
                    }
                }

                return workItems;
            }
            catch (Exception ex)
            {
                _logHelper.Error($"Erro ao buscar issues: {ex.Message}");
                return new List<WorkItem>();
            }
        }
    }
}