using System.Collections.Generic;
using System.Linq;
using static HandoffExporter.Models.WorkItemVO;

namespace HandoffExporter.Services
{
    /// <summary>
    /// Decide quais WorkItems são "topo" para montar a árvore de export — garantindo que
    /// **todo item em escopo seja exportado**: roots (sem pai em escopo) + órfãos (não
    /// alcançáveis a partir dos roots, ex.: ciclos de migração ou re-parent por novos
    /// níveis como "Sub Módulo"). Sem essa rede, um item que é filho (Hierarchy-Forward)
    /// cuja cadeia de pais não chega a um root somia silenciosamente.
    /// </summary>
    public static class WorkItemTree
    {
        public static List<int> ChildIds(WorkItem wi)
        {
            var result = new List<int>();
            if (wi?.Relations == null) return result;
            foreach (var rel in wi.Relations)
            {
                if (rel?.Rel != "System.LinkTypes.Hierarchy-Forward") continue;
                var parts = (rel.Url ?? "").Split('/');
                if (parts.Length > 0 && int.TryParse(parts[^1], out var cid)) result.Add(cid);
            }
            return result;
        }

        public static List<WorkItem> TopLevel(IReadOnlyList<WorkItem> allItems)
        {
            var map = new Dictionary<int, WorkItem>();
            foreach (var w in allItems) if (w != null) map[w.Id] = w;

            var childIds = new HashSet<int>();
            foreach (var w in map.Values)
                foreach (var cid in ChildIds(w))
                    childIds.Add(cid);

            var reached = new HashSet<int>();
            void Reach(int id, HashSet<int> vis)
            {
                if (!map.ContainsKey(id) || !vis.Add(id)) return;
                reached.Add(id);
                foreach (var cid in ChildIds(map[id])) Reach(cid, vis);
            }

            var top = new List<WorkItem>();
            var addedTop = new HashSet<int>();
            void AddTop(WorkItem w) { if (addedTop.Add(w.Id)) { top.Add(w); Reach(w.Id, new HashSet<int>()); } }

            // 1) roots: nenhum item EM ESCOPO os referencia como filho.
            foreach (var w in allItems) if (w != null && !childIds.Contains(w.Id)) AddTop(w);
            // 2) órfãos: em escopo mas não alcançados (ciclos / re-parent) — entram no topo.
            foreach (var w in allItems) if (w != null && !reached.Contains(w.Id)) AddTop(w);

            return top;
        }
    }
}
