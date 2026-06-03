using HandoffExporter.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace HandoffExporter.Mcp
{
    /// <summary>
    /// Tools MCP — wrappers finos sobre <see cref="HandoffStore"/> (lê o snapshot do split).
    /// Itens ficam em pastas por WorkItemType (pbi/us/st/spike/...); a resolução id→arquivo
    /// é feita pelo index.json. O <see cref="HandoffStore"/> é injetado via DI.
    /// </summary>
    [McpServerToolType]
    public static class HandoffTools
    {
        [McpServerTool(Name = "list_items"), Description("Catálogo do snapshot do MacGyver (index.json): counts por tipo, roots (árvore) e items (id→path).")]
        public static string ListItems(HandoffStore store)
            => store.GetIndex() ?? "{\"error\":\"index.json não encontrado — rode o export --split antes\"}";

        [McpServerTool(Name = "get_item"), Description("Retorna um work item completo (PBI, User Story, Sprint Task, Spike, etc.) pelo id — resolvido pelo tipo via index.")]
        public static string GetItem(HandoffStore store, [Description("Id do work item")] int id)
            => store.GetItem(id) ?? $"{{\"error\":\"work item {id} não encontrado\"}}";

        [McpServerTool(Name = "search"), Description("Busca por texto (case-insensitive) em title/description/acceptanceCriteria de qualquer item. Retorna hits {type,id,title,path}.")]
        public static string Search(HandoffStore store, [Description("Texto a buscar")] string query)
            => JsonSerializer.Serialize(store.Search(query));

        [McpServerTool(Name = "list_repos"), Description("Lista os repositórios do snapshot (repos/index.json).")]
        public static string ListRepos(HandoffStore store)
            => store.GetReposIndex() ?? "{\"error\":\"repos/index.json não encontrado — rode o export --includeRepos true\"}";

        [McpServerTool(Name = "get_repo"), Description("Retorna o repositório (metadata + branches) pelo nome.")]
        public static string GetRepo(HandoffStore store, [Description("Nome do repositório")] string name)
            => store.GetRepo(name) ?? $"{{\"error\":\"repo {name} não encontrado\"}}";

        [McpServerTool(Name = "get_links"), Description("Vínculos work item ↔ repositório (via PR) de um work item id: [{workItemId,repo,prId,prTitle}].")]
        public static string GetLinks(HandoffStore store, [Description("Id do work item")] int workItemId)
            => store.GetLinksForWorkItem(workItemId);
    }
}
