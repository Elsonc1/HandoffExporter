using HandoffExporter.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace HandoffExporter.Mcp
{
    /// <summary>
    /// Tools MCP — wrappers finos sobre <see cref="HandoffStore"/> (lê o snapshot do split).
    /// O <see cref="HandoffStore"/> é injetado via DI (singleton registrado no Program).
    /// Nomes explícitos (snake_case) para ficarem estáveis no tools/list dos clientes.
    /// </summary>
    [McpServerToolType]
    public static class HandoffTools
    {
        [McpServerTool(Name = "list_pbis"), Description("Lista os PBIs do snapshot do MacGyver (index.json): id, título, state, childUsIds.")]
        public static string ListPbis(HandoffStore store)
            => store.GetIndex() ?? "{\"error\":\"index.json não encontrado — rode o export --split antes\"}";

        [McpServerTool(Name = "get_pbi"), Description("Retorna o PBI completo (description, childUsIds, attachments, assets) pelo id.")]
        public static string GetPbi(HandoffStore store, [Description("Id do PBI")] int id)
            => store.GetPbi(id) ?? $"{{\"error\":\"PBI {id} não encontrado\"}}";

        [McpServerTool(Name = "get_us"), Description("Retorna a User Story completa (description, acceptanceCriteria, parentPbiId) pelo id.")]
        public static string GetUs(HandoffStore store, [Description("Id da User Story")] int id)
            => store.GetUs(id) ?? $"{{\"error\":\"US {id} não encontrada\"}}";

        [McpServerTool(Name = "search"), Description("Busca por texto (case-insensitive) em title/description/acceptanceCriteria de PBIs e US. Retorna hits {type,id,title,path}.")]
        public static string Search(HandoffStore store, [Description("Texto a buscar")] string query)
            => JsonSerializer.Serialize(store.Search(query));

        [McpServerTool(Name = "list_repos"), Description("Lista os repositórios do snapshot (repos/index.json).")]
        public static string ListRepos(HandoffStore store)
            => store.GetReposIndex() ?? "{\"error\":\"repos/index.json não encontrado — rode o export --includeRepos true\"}";

        [McpServerTool(Name = "get_repo"), Description("Retorna o repositório (metadata + branches) pelo nome.")]
        public static string GetRepo(HandoffStore store, [Description("Nome do repositório")] string name)
            => store.GetRepo(name) ?? $"{{\"error\":\"repo {name} não encontrado\"}}";
    }
}
