using HandoffExporter.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace HandoffExporter.Services
{
    /// <summary>
    /// Baixa os anexos para os caminhos planejados pelo <see cref="AttachmentPlanner"/>
    /// (attachments/&lt;itemId&gt;/&lt;arquivo&gt;), usando o HttpClient autenticado do export.
    /// Roda ANTES do split, gravando num diretório de staging: falha/limite zera o
    /// LocalPath do anexo, então os JSONs escritos pelo split só anunciam arquivos que
    /// existem de fato (a url original permanece como fallback). Depois do split, o
    /// staging é movido para dentro do snapshot. Falha de um anexo não aborta o export.
    /// </summary>
    public class AttachmentDownloader
    {
        private readonly HttpClient _http;
        private readonly ILogHelper _log;
        private readonly long _maxBytes;

        public AttachmentDownloader(HttpClient http, ILogHelper log, long maxBytes = 25L * 1024 * 1024)
        {
            _http = http;
            _log = log;
            _maxBytes = maxBytes;
        }

        public async Task<(int ok, int failed, int skipped)> DownloadAllAsync(IEnumerable<Item> roots, string splitDir)
        {
            int ok = 0, failed = 0, skipped = 0;
            var seen = new HashSet<int>();

            async Task VisitAsync(Item item)
            {
                if (item == null || !seen.Add(item.Id)) return;

                foreach (var att in (item.Attachments ?? new List<Attachment>()).Where(a => a != null))
                {
                    if (string.IsNullOrEmpty(att.LocalPath) || string.IsNullOrEmpty(att.Url)) continue;

                    try
                    {
                        var bytes = await _http.GetByteArrayAsync(att.Url);
                        if (bytes.LongLength > _maxBytes)
                        {
                            _log?.Warn("anexo pulado (>{0} bytes): item {1} '{2}' ({3} bytes)", _maxBytes, item.Id, att.FileName, bytes.LongLength);
                            att.LocalPath = null; // não anuncia arquivo que não existe
                            skipped++;
                            continue;
                        }

                        var full = Path.Combine(splitDir, Path.Combine(att.LocalPath.Split('/')));
                        Directory.CreateDirectory(Path.GetDirectoryName(full));
                        await File.WriteAllBytesAsync(full, bytes);
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        _log?.Warn("falha ao baixar anexo do item {0} '{1}': {2} (url mantida no JSON)", item.Id, att.FileName, ex.Message);
                        att.LocalPath = null;
                        failed++;
                    }
                }

                foreach (var c in (item.Children ?? new List<Item>()).Where(c => c != null))
                    await VisitAsync(c);
            }

            foreach (var root in roots ?? Enumerable.Empty<Item>())
                await VisitAsync(root);

            _log?.Info("Anexos: {0} baixados, {1} falhas, {2} pulados (limite {3} bytes) -> {4}",
                ok, failed, skipped, _maxBytes, Path.Combine(splitDir, "attachments"));
            return (ok, failed, skipped);
        }
    }
}
