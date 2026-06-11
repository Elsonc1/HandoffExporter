using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HandoffExporter.Services
{
    /// <summary>
    /// Planeja os caminhos locais dos anexos ANTES do split (puro, sem IO/HTTP):
    /// uma pasta por artefato — attachments/&lt;itemId&gt;/&lt;nome-sanitizado&gt; — com dedupe
    /// determinístico (arquivo.txt, arquivo-2.txt). Assim os JSONs dos itens já saem
    /// com o localPath correto e o download só preenche os bytes depois.
    /// </summary>
    public static class AttachmentPlanner
    {
        public static void AssignLocalPaths(IEnumerable<Item> roots)
        {
            foreach (var item in roots ?? Enumerable.Empty<Item>())
                Visit(item, new HashSet<int>());
        }

        private static void Visit(Item item, HashSet<int> seen)
        {
            if (item == null || !seen.Add(item.Id)) return;

            var atts = (item.Attachments ?? new List<Attachment>()).Where(a => a != null).ToList();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var att in atts)
            {
                var name = SafeFileName(att.FileName);
                var final = name;
                int n = 2;
                while (!used.Add(final))
                {
                    var ext = Path.GetExtension(name);
                    final = $"{Path.GetFileNameWithoutExtension(name)}-{n}{ext}";
                    n++;
                }
                att.LocalPath = $"attachments/{item.Id}/{final}";
            }

            foreach (var c in (item.Children ?? new List<Item>()).Where(c => c != null))
                Visit(c, seen);
        }

        /// <summary>Sanitiza o nome do arquivo (chars inválidos e path traversal) preservando a extensão.</summary>
        public static string SafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "attachment";
            // só o nome final — derruba qualquer tentativa de caminho (\, /, ..)
            name = name.Replace('\\', '/').Split('/').Last().Trim();
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
            name = name.Trim('.', ' ');
            return name.Length == 0 ? "attachment" : name;
        }
    }
}
