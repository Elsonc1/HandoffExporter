using HtmlAgilityPack;
using HandoffExporter.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace HandoffExporter.Services
{
    public class TFSAplicationProcess
    {
        public readonly HttpClient _httpClient;
        public readonly string _baseUrl;
        public readonly string _pat;

        public TFSAplicationProcess(string organization, string project, string pat)
        {
            _baseUrl = $"https://tfs.ndd.tech/{organization}/{project}";
            _pat = pat;

            _httpClient = new HttpClient();
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_pat}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public string GetWorkItemAsync(int workItemId, string expand = null, string fields = null, ILogHelper logHelper = null)
        {
            try
            {
                var url = $"{_baseUrl}/_apis/wit/workitems/{workItemId}?api-version=6.0";
                if (!string.IsNullOrEmpty(expand))
                {
                    url += $"&$expand={expand}";
                }
                if (!string.IsNullOrEmpty(fields))
                {
                    url += $"&fields={fields}";
                }

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using (var response = _httpClient.SendAsync(request).Result)
                {
                    response.EnsureSuccessStatusCode();

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = response.Content.ReadAsStringAsync();
                        logHelper?.Error($"Erro na requisição: {response.StatusCode}. Detalhes: {errorContent}");
                        return response.StatusCode.ToString();
                    }

                    return response.Content.ReadAsStringAsync().Result;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public string ExtractTextFromHtml(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var text = doc.DocumentNode.InnerText;
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            return string.Join(Environment.NewLine, lines);
        }
    }
}