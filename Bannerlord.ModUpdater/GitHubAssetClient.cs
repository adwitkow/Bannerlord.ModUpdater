namespace Bannerlord.ModUpdater
{
    internal class GitHubAssetClient
    {
        private readonly HttpClient _client;

        public GitHubAssetClient(HttpClient client)
        {
            _client = client;
        }

        public async Task<Stream> GetAssetStream(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Remove("Accept");
            request.Headers.Add("Accept", "application/octet-stream");

            var response = await _client.SendAsync(request);
            return await response.Content.ReadAsStreamAsync();
        }
    }
}
