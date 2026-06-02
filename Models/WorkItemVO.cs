using Newtonsoft.Json;

namespace HandoffExporter.Models
{
    public class WorkItemVO
    {
        public class WiqlResult
        {
            public WorkItemReference[] WorkItems { get; set; }
        }

        public class WorkItemReference
        {
            public int Id { get; set; }
        }

        public class WorkItemResult
        {
            [JsonProperty("count")]
            public int Count { get; set; }

            [JsonProperty("value")]
            public List<WorkItem> WorkItems { get; set; }
        }

        public class WorkItemDetail
        {
            public WorkItemField Fields { get; set; }
            public WorkItemRelation[] Relations { get; set; }
        }

        public class WorkItem
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("fields")]
            public Dictionary<string, object> Fields { get; set; }

            [JsonProperty("relations")]
            public List<WorkItemRelation> Relations { get; set; }

            public List<WorkItem> Children { get; set; }
        }

        public class WorkItemRelation
        {
            [JsonProperty("rel")]
            public string Rel { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }
        }

        public class WorkItemField
        {
            public string SystemTitle { get; set; }
        }
    }
}