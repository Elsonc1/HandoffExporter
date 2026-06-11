using System;
using System.Collections.Generic;
using System.Linq;
using static HandoffExporter.Models.WorkItemVO;

namespace HandoffExporter.Services
{
    /// <summary>
    /// Resolve o conteúdo de um WorkItem de forma **VO-agnóstica**: cada tipo de work item
    /// do TFS da NDD guarda a descrição em campos diferentes (US: ndd.DefinicoesDeNegocio;
    /// Issue: System.Description + ndd.ModeloDescricao; PBI Compliance: ndd.PropostaFuncional,
    /// NDD.Objetivo, NDD.BeneficiosCliente, NDD.Discovery*/Sorting*...).
    ///
    /// Estratégia: coleta TODOS os campos de conteúdo (prefixos ndd./NDD./nddd. + exatos
    /// conhecidos), usa a prioridade clássica para description/acceptanceCriteria e, quando a
    /// descrição primária está vazia, COMPÕE a descrição a partir dos campos coletados.
    /// O mapa completo vai em ContentFields — nada se perde, qualquer que seja o VO.
    /// </summary>
    public static class ContentResolver
    {
        public class Resolved
        {
            public string RawHtml { get; set; }
            public string SanitizedText { get; set; }
            public string AcceptanceCriteria { get; set; }
            public Dictionary<string, string> ContentFields { get; set; } = new();
        }

        static readonly string[] DescriptionPriority = { "ndd.DefinicoesDeNegocio", "System.Description" };
        static readonly string[] AcceptancePriority = { "ndd.DefinicoesTecnicas", "Microsoft.VSTS.Common.AcceptanceCriteria" };

        // Ordem preferida na composição (os campos "grandes" primeiro); o resto sai ordenado.
        static readonly string[] ComposePreferred = { "ndd.PropostaFuncional", "NDD.Objetivo", "NDD.BeneficiosCliente" };

        static readonly string[] ExactAllow =
        {
            "System.Description",
            "Microsoft.VSTS.Common.AcceptanceCriteria",
            "Microsoft.VSTS.Common.ProductName",
            "Microsoft.VSTS.Common.ModuleName",
            "Microsoft.VSTS.Common.ProblemType",
            "Microsoft.VSTS.Common.OperationType",
            "Microsoft.VSTS.Common.EnvironmentType",
        };

        public static bool IsContentField(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (ExactAllow.Contains(key, StringComparer.OrdinalIgnoreCase)) return true;
            return key.StartsWith("ndd.", StringComparison.OrdinalIgnoreCase)   // ndd. / NDD. / Ndd.
                || key.StartsWith("nddd.", StringComparison.OrdinalIgnoreCase); // nddd.Data*NT
        }

        /// <summary>Campos de conteúdo (apenas valores string não-vazios), sanitizados. Ordem determinística.</summary>
        public static Dictionary<string, string> Collect(WorkItem wi, Func<string, string> sanitize)
        {
            var result = new Dictionary<string, string>();
            if (wi?.Fields == null) return result;

            var keys = wi.Fields.Keys.Where(IsContentField)
                .OrderBy(k => PreferredRank(k)).ThenBy(k => k, StringComparer.Ordinal);

            foreach (var k in keys)
            {
                if (wi.Fields[k] is not string s || string.IsNullOrWhiteSpace(s)) continue;
                var text = sanitize != null ? sanitize(s) : s;
                if (!string.IsNullOrWhiteSpace(text)) result[k] = text;
            }
            return result;
        }

        public static Resolved Resolve(WorkItem wi, Func<string, string> sanitize)
        {
            var r = new Resolved { ContentFields = Collect(wi, sanitize) };

            // 1) Descrição primária (prioridade clássica do dev-guide).
            var descKey = DescriptionPriority.FirstOrDefault(k => HasText(wi, k));
            if (descKey != null)
            {
                r.RawHtml = wi.Fields[descKey].ToString();
                r.SanitizedText = sanitize != null ? sanitize(r.RawHtml) : r.RawHtml;
            }

            // 2) AcceptanceCriteria.
            var acKey = AcceptancePriority.FirstOrDefault(k => HasText(wi, k));
            if (acKey != null)
                r.AcceptanceCriteria = sanitize != null ? sanitize(wi.Fields[acKey].ToString()) : wi.Fields[acKey].ToString();

            // 3) VO diferente (sem descrição primária) → compõe dos campos de conteúdo.
            if (string.IsNullOrWhiteSpace(r.SanitizedText) && r.ContentFields.Count > 0)
            {
                var exclude = DescriptionPriority.Concat(AcceptancePriority);
                var parts = r.ContentFields.Keys
                    .Where(k => !exclude.Contains(k, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (parts.Count > 0)
                {
                    r.SanitizedText = string.Join("\n\n", parts.Select(k => $"### {k}\n{r.ContentFields[k]}"));
                    r.RawHtml = string.Join("\n<hr/>\n", parts.Select(k => wi.Fields[k]?.ToString() ?? ""));
                }
            }

            return r;
        }

        static int PreferredRank(string key)
        {
            for (int i = 0; i < ComposePreferred.Length; i++)
                if (string.Equals(ComposePreferred[i], key, StringComparison.OrdinalIgnoreCase)) return i;
            return ComposePreferred.Length;
        }

        static bool HasText(WorkItem wi, string key) =>
            wi?.Fields != null && wi.Fields.ContainsKey(key)
            && wi.Fields[key] is string s && !string.IsNullOrWhiteSpace(s);
    }
}
