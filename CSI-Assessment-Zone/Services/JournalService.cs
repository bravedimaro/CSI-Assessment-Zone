using CSI_Assessment_Zone.Models;
using Newtonsoft.Json;
using System.Text;

namespace CSI_Assessment_Zone.Services
{
    public class JournalService
    {
        private readonly HttpClient _httpClient;
        private const string API_ENDPOINT = "http://52.234.156.59:31000/pushjournal/api/push-journal/";
        private const string API_KEY = "zsLive_8748261147813940309";

        public JournalService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", API_KEY);
        }

        public async Task<bool> PushJournal(JournalEntry entry)
        {
            
            var json = JsonConvert.SerializeObject(entry);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
           
            try
            {
                var response = await _httpClient.PostAsync(API_ENDPOINT, content);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Error pushing journal: {e.Message}");
                return false;
            }
        }
    }
}
