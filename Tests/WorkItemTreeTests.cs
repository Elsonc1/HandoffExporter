using HandoffExporter.Services;
using Xunit;
using static HandoffExporter.Models.WorkItemVO;

namespace HandoffExporter.Tests
{
    public class WorkItemTreeTests
    {
        private static WorkItem Wi(int id, params int[] children) => new()
        {
            Id = id,
            Fields = new(),
            Relations = children.Select(c => new WorkItemRelation
            {
                Rel = "System.LinkTypes.Hierarchy-Forward",
                Url = $"https://tfs.ndd.tech/_apis/wit/workItems/{c}"
            }).ToList(),
            Children = new()
        };

        // Simula o que a montagem da árvore alcança a partir do "topo".
        private static HashSet<int> CoveredFrom(List<WorkItem> top, IReadOnlyList<WorkItem> all)
        {
            var map = all.ToDictionary(w => w.Id);
            var seen = new HashSet<int>();
            void R(int id) { if (!map.ContainsKey(id) || !seen.Add(id)) return; foreach (var c in WorkItemTree.ChildIds(map[id])) R(c); }
            foreach (var t in top) R(t.Id);
            return seen;
        }

        [Fact]
        public void SimpleTree_RootOnly()
        {
            var all = new List<WorkItem> { Wi(1, 2), Wi(2) };
            Assert.Equal(new[] { 1 }, WorkItemTree.TopLevel(all).Select(t => t.Id).ToArray());
        }

        [Fact]
        public void ChildOfOutOfScopeParent_BecomesRoot()
        {
            // pai (1) fora de escopo; só o filho 2 veio na WIQL → 2 deve ser exportado
            var all = new List<WorkItem> { Wi(2) };
            Assert.Equal(new[] { 2 }, WorkItemTree.TopLevel(all).Select(t => t.Id).ToArray());
        }

        [Fact]
        public void CycleWithoutRoot_NothingDropped()
        {
            // 2<->3 em ciclo, sem entrada root (cenário 206366 em TFS migrado/re-parent)
            var all = new List<WorkItem> { Wi(2, 3), Wi(3, 2) };
            var top = WorkItemTree.TopLevel(all);
            Assert.NotEmpty(top);
            Assert.Equal(new[] { 2, 3 }, CoveredFrom(top, all).OrderBy(x => x).ToArray());
        }

        [Fact]
        public void Diamond_SingleRoot_AllCovered()
        {
            var all = new List<WorkItem> { Wi(1, 2, 3), Wi(2, 4), Wi(3, 4), Wi(4) };
            Assert.Equal(new[] { 1 }, WorkItemTree.TopLevel(all).Select(t => t.Id).ToArray());
            Assert.Equal(new[] { 1, 2, 3, 4 }, CoveredFrom(WorkItemTree.TopLevel(all), all).OrderBy(x => x).ToArray());
        }

        [Fact]
        public void MixedOrphans_EveryInScopeItemCovered()
        {
            // 1->2 normal; 5<->6 ciclo sem root; 9 filho de pai fora de escopo
            var all = new List<WorkItem> { Wi(1, 2), Wi(2), Wi(5, 6), Wi(6, 5), Wi(9) };
            Assert.Equal(new[] { 1, 2, 5, 6, 9 }, CoveredFrom(WorkItemTree.TopLevel(all), all).OrderBy(x => x).ToArray());
        }
    }
}
