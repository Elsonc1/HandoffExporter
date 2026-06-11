using HandoffExporter.Services;
using Xunit;
using static HandoffExporter.Models.WorkItemVO;

namespace HandoffExporter.Tests
{
    public class ContentResolverTests
    {
        // sanitize de teste: marca a passagem sem alterar o conteúdo
        private static string San(string s) => s.Replace("<b>", "").Replace("</b>", "");

        private static WorkItem Wi(Dictionary<string, object> fields) => new()
        {
            Id = 1,
            Fields = fields,
            Relations = new(),
            Children = new()
        };

        // ── O caso real: PBI Compliance (204055) — sem System.Description ─────────────
        [Fact]
        public void ComplianceVo_ComposesDescription_FromNddFields()
        {
            var wi = Wi(new()
            {
                ["System.WorkItemType"] = "Product Backlog Item Compliance",
                ["System.Title"] = "NT 2025.002",
                ["System.State"] = "PREPARAÇÃO PARA ENTREGA",
                ["System.CreatedDate"] = "2026-01-01",
                ["ndd.PropostaFuncional"] = "<b>Proposta funcional completa</b> com 16k chars",
                ["NDD.Objetivo"] = "Objetivo do PBI",
                ["NDD.BeneficiosCliente"] = "Beneficios ao cliente",
                ["NDD.DiscoveryStakeholders"] = "Stakeholders do discovery",
                ["Microsoft.VSTS.Common.Priority"] = 2,           // não-string → fora
                ["System.BoardColumn"] = "Doing",                  // não permitido → fora
                ["WEF_ABC_Kanban.Column"] = "Doing"                // não permitido → fora
            });

            var r = ContentResolver.Resolve(wi, San);

            Assert.False(string.IsNullOrWhiteSpace(r.SanitizedText));
            Assert.Contains("### ndd.PropostaFuncional", r.SanitizedText);
            Assert.Contains("Proposta funcional completa", r.SanitizedText);
            Assert.Contains("### NDD.Objetivo", r.SanitizedText);
            Assert.Contains("### NDD.BeneficiosCliente", r.SanitizedText);
            Assert.Contains("### NDD.DiscoveryStakeholders", r.SanitizedText);

            // ordem preferida: PropostaFuncional antes de Objetivo antes de Beneficios
            int p = r.SanitizedText.IndexOf("ndd.PropostaFuncional");
            int o = r.SanitizedText.IndexOf("NDD.Objetivo");
            int b = r.SanitizedText.IndexOf("NDD.BeneficiosCliente");
            Assert.True(p < o && o < b);

            // metadados não vazam
            Assert.DoesNotContain("Kanban", r.SanitizedText);
            Assert.DoesNotContain("BoardColumn", r.SanitizedText);

            // mapa completo disponível
            Assert.Equal(4, r.ContentFields.Count);
            Assert.True(r.ContentFields.ContainsKey("ndd.PropostaFuncional"));
        }

        // ── Contrato clássico preservado ───────────────────────────────────────────────
        [Fact]
        public void UserStory_DefinicoesDeNegocio_StillWins_NoComposition()
        {
            var wi = Wi(new()
            {
                ["System.WorkItemType"] = "User Story",
                ["ndd.DefinicoesDeNegocio"] = "Regra de negocio",
                ["ndd.DefinicoesTecnicas"] = "Criterios tecnicos",
                ["NDD.Objetivo"] = "Algo extra"
            });

            var r = ContentResolver.Resolve(wi, San);

            Assert.Equal("Regra de negocio", r.SanitizedText);
            Assert.DoesNotContain("###", r.SanitizedText);          // sem composição
            Assert.Equal("Criterios tecnicos", r.AcceptanceCriteria);
            Assert.True(r.ContentFields.ContainsKey("NDD.Objetivo")); // mas o mapa tem tudo
        }

        [Fact]
        public void SystemDescription_Fallback_ForOtherTypes()
        {
            var wi = Wi(new()
            {
                ["System.WorkItemType"] = "Issue",
                ["System.Description"] = "Descricao da issue",
                ["ndd.ModeloDescricao"] = "Modelo extra"
            });

            var r = ContentResolver.Resolve(wi, San);
            Assert.Equal("Descricao da issue", r.SanitizedText);
            Assert.True(r.ContentFields.ContainsKey("ndd.ModeloDescricao"));
        }

        [Fact]
        public void AcceptanceCriteria_VstsFallback()
        {
            var wi = Wi(new() { ["Microsoft.VSTS.Common.AcceptanceCriteria"] = "AC padrao" });
            Assert.Equal("AC padrao", ContentResolver.Resolve(wi, San).AcceptanceCriteria);
        }

        // ── Robustez / determinismo ────────────────────────────────────────────────────
        [Fact]
        public void EmptyFields_AllNull_NoThrow()
        {
            var r = ContentResolver.Resolve(Wi(new()), San);
            Assert.Null(r.SanitizedText);
            Assert.Null(r.AcceptanceCriteria);
            Assert.Empty(r.ContentFields);
        }

        [Fact]
        public void NdddPrefix_DatesIncluded_InContentFields()
        {
            var wi = Wi(new() { ["nddd.DataPublicacaoNT"] = "2025-08-01T00:00:00Z" });
            Assert.True(ContentResolver.Resolve(wi, San).ContentFields.ContainsKey("nddd.DataPublicacaoNT"));
        }

        [Fact]
        public void IsContentField_Rules()
        {
            Assert.True(ContentResolver.IsContentField("ndd.PropostaFuncional"));
            Assert.True(ContentResolver.IsContentField("NDD.Objetivo"));
            Assert.True(ContentResolver.IsContentField("Ndd.Bloqueio"));
            Assert.True(ContentResolver.IsContentField("nddd.DataProducaoNT"));
            Assert.True(ContentResolver.IsContentField("System.Description"));
            Assert.True(ContentResolver.IsContentField("Microsoft.VSTS.Common.ProductName"));
            Assert.False(ContentResolver.IsContentField("System.Title"));
            Assert.False(ContentResolver.IsContentField("WEF_X_Kanban.Column"));
            Assert.False(ContentResolver.IsContentField("Microsoft.VSTS.Common.Priority"));
            Assert.False(ContentResolver.IsContentField("NDDigital.SLAPause")); // prefixo NDDigital ≠ ndd.
        }

        [Fact]
        public void Composition_Deterministic_TwoRuns()
        {
            var fields = new Dictionary<string, object>
            {
                ["NDD.Zeta"] = "z", ["NDD.Alfa"] = "a", ["ndd.PropostaFuncional"] = "pf"
            };
            var a = ContentResolver.Resolve(Wi(new(fields)), San).SanitizedText;
            var b = ContentResolver.Resolve(Wi(new(fields)), San).SanitizedText;
            Assert.Equal(a, b);
            Assert.True(a!.IndexOf("PropostaFuncional") < a.IndexOf("NDD.Alfa")); // preferido primeiro
            Assert.True(a.IndexOf("NDD.Alfa") < a.IndexOf("NDD.Zeta"));           // resto ordenado
        }
    }
}
