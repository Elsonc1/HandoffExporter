using HandoffExporter.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HandoffExporter.Services
{
    /// <summary>
    /// Quebra um <see cref="HandoffJson"/> (árvore PBI → User Story) em sub-arquivos
    /// pequenos e legíveis, para consumo por agents (Read/Grep) ou por um MCP server local.
    ///
    /// Layout (todos os paths nos JSONs são relativos à raiz do diretório de split):
    ///   {outDir}/index.json            catálogo da area
    ///   {outDir}/pbi/PBI-&lt;id&gt;.json     item raiz (pai) + lista de filhas
    ///   {outDir}/us/US-&lt;id&gt;.json       item filho (User Story) + ref ao pai
    ///   {outDir}/assets/&lt;...&gt;          imagens/base64 extraídas dos data-URIs
    ///   {outDir}/raw/&lt;id&gt;.html         RawHtml original, fora do arquivo do agent
    ///
    /// Determinístico: coleções ordenadas por Id; nomes de arquivo estáveis.
    /// Não serializa segredos; tira data-URIs base64 e RawHtml do caminho do agent.
    /// Todo o progresso é logado via <see cref="ILogHelper"/> (logs.json), quando fornecido.
    /// </summary>
    public static class HandoffSplitter
    {
        public static void Split(HandoffJson handoff, string outDir, ILogHelper logHelper = null)
        {
            if (handoff == null) throw new ArgumentNullException(nameof(handoff));
            if (string.IsNullOrWhiteSpace(outDir)) throw new ArgumentException("outDir vazio", nameof(outDir));

            logHelper?.Info("Split iniciado -> {0}", outDir);

            string pbiDir = Path.Combine(outDir, "pbi");
            string usDir = Path.Combine(outDir, "us");
            string assetsDir = Path.Combine(outDir, "assets");
            string rawDir = Path.Combine(outDir, "raw");
            Directory.CreateDirectory(pbiDir);
            Directory.CreateDirectory(usDir);
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(rawDir);

            var roots = (handoff.Items ?? new List<Item>())
                .Where(i => i != null)
                .OrderBy(i => i.Id)
                .ToList();

            int usCount = 0;
            var indexPbis = new List<object>();

            foreach (var root in roots)
            {
                var childUsIds = (root.Children ?? new List<Item>())
                    .Where(c => c != null).Select(c => c.Id).OrderBy(x => x).ToList();

                WriteNodeFile(root, pbiDir, "PBI", null, assetsDir, rawDir, logHelper);
                usCount += WriteDescendants(root, usDir, assetsDir, rawDir, logHelper);

                indexPbis.Add(new
                {
                    id = root.Id,
                    workItemType = root.WorkItemType,
                    title = root.Title,
                    state = root.State,
                    path = $"pbi/PBI-{root.Id}.json",
                    childUsIds
                });
            }

            var index = new
            {
                area = handoff.Request?.AreaPath,
                exportedAtUtc = handoff.ExportedAtUtc,
                generator = handoff.Handoff?.Generator ?? "HandoffExporter",
                version = "2.0",
                source = new { collection = handoff.Source?.Collection, project = handoff.Source?.Project },
                request = new
                {
                    areaPath = handoff.Request?.AreaPath,
                    pbiId = handoff.Request?.PbiId,
                    mode = handoff.Request?.Mode,
                    includeIssues = handoff.Request?.IncludeIssues
                },
                counts = new { pbi = roots.Count, us = usCount },
                pbis = indexPbis
            };

            File.WriteAllText(Path.Combine(outDir, "index.json"),
                JsonConvert.SerializeObject(index, Formatting.Indented));

            int assetCount = Directory.GetFiles(assetsDir).Length;
            int rawCount = Directory.GetFiles(rawDir).Length;
            logHelper?.Info("Split concluido: {0} PBI(s), {1} US, {2} asset(s), {3} raw -> {4}",
                roots.Count, usCount, assetCount, rawCount, outDir);
        }

        private static int WriteDescendants(Item parent, string usDir, string assetsDir, string rawDir, ILogHelper logHelper)
        {
            int count = 0;
            foreach (var child in (parent.Children ?? new List<Item>())
                         .Where(c => c != null).OrderBy(c => c.Id))
            {
                WriteNodeFile(child, usDir, "US", parent.Id, assetsDir, rawDir, logHelper);
                count++;
                count += WriteDescendants(child, usDir, assetsDir, rawDir, logHelper);
            }
            return count;
        }

        private static void WriteNodeFile(Item item, string dir, string prefix, int? parentId, string assetsDir, string rawDir, ILogHelper logHelper)
        {
            // RawHtml → /raw/<id>.html (preservado, fora do arquivo do agent)
            string? rawPath = null;
            if (!string.IsNullOrEmpty(item.RawHtml))
            {
                File.WriteAllText(Path.Combine(rawDir, $"{item.Id}.html"), item.RawHtml);
                rawPath = $"raw/{item.Id}.html";
            }

            var assets = ExtractAssets(item, assetsDir, logHelper);

            var attachments = (item.Attachments ?? new List<Attachment>())
                .Where(a => a != null)
                .Select(a => new { url = a.Url, fileName = a.FileName, contentType = a.ContentType, size = a.Size })
                .ToList();

            var childIds = (item.Children ?? new List<Item>())
                .Where(c => c != null).Select(c => c.Id).OrderBy(x => x).ToList();

            object node;
            if (prefix == "PBI")
            {
                node = new
                {
                    id = item.Id,
                    workItemType = item.WorkItemType,
                    title = item.Title,
                    state = item.State,
                    description = item.SanitizedText,
                    acceptanceCriteria = item.AcceptanceCriteria,
                    childUsIds = childIds,
                    children = childIds.Select(cid => new { id = cid, path = $"us/US-{cid}.json" }).ToList(),
                    attachments,
                    assets,
                    rawPath
                };
            }
            else
            {
                node = new
                {
                    id = item.Id,
                    workItemType = item.WorkItemType,
                    title = item.Title,
                    state = item.State,
                    parentPbiId = parentId,
                    description = item.SanitizedText,
                    acceptanceCriteria = item.AcceptanceCriteria,
                    childIds,
                    attachments,
                    assets,
                    rawPath
                };
            }

            File.WriteAllText(Path.Combine(dir, $"{prefix}-{item.Id}.json"),
                JsonConvert.SerializeObject(node, Formatting.Indented));
        }

        /// <summary>
        /// Extrai data-URIs base64 para /assets e referencia por path; URLs externas
        /// viram referência (sem download — offline). Nunca emite o payload data: inline.
        /// </summary>
        private static List<object> ExtractAssets(Item item, string assetsDir, ILogHelper logHelper)
        {
            var result = new List<object>();
            var assets = (item.Assets ?? new List<Asset>()).Where(a => a != null).ToList();
            int n = 0;
            foreach (var a in assets)
            {
                n++;
                if (!string.IsNullOrEmpty(a.DataUri) && a.DataUri.StartsWith("data:") && a.DataUri.Contains(','))
                {
                    try
                    {
                        int comma = a.DataUri.IndexOf(',');
                        string header = a.DataUri.Substring(5, comma - 5); // depois de "data:"
                        string payload = a.DataUri.Substring(comma + 1);
                        string mime = header.Split(';')[0];
                        byte[] bytes = Convert.FromBase64String(payload);
                        string fileName = $"{item.Id}-asset-{n}.{MimeToExt(mime)}";
                        File.WriteAllBytes(Path.Combine(assetsDir, fileName), bytes);
                        result.Add(new { fileName, path = $"assets/{fileName}", contentType = mime, size = bytes.Length });
                        continue;
                    }
                    catch
                    {
                        logHelper?.Warn("Asset base64 malformado no item {0} (asset {1}) - mantido como referencia", item.Id, n);
                    }
                }

                // URL externa. Nunca emitir uma data: URI inline (re-infla o JSON).
                string? safeUrl = (a.Url != null && a.Url.StartsWith("data:")) ? null : a.Url;
                result.Add(new { url = safeUrl, contentType = a.ContentType, fileName = a.FileName });
            }
            return result;
        }

        private static string MimeToExt(string mime) => mime switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "image/jpg" => "jpg",
            "image/gif" => "gif",
            "image/bmp" => "bmp",
            "image/svg+xml" => "svg",
            "image/webp" => "webp",
            _ => "bin"
        };
    }
}
