using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace eCommerce.Server.Helpers
{
    public static class HttpHelper
    {
        public static async Task<string> PostAsync(HttpClient client, string url, HttpContent content)
        {
            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<string> GetAsync(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
} 