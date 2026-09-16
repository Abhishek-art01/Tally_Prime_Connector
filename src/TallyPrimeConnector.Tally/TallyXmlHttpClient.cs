using System.Net.Http.Headers;
using TallyPrimeConnector.Contracts;

namespace TallyPrimeConnector.Tally;

public sealed class TallyXmlHttpClient(HttpClient? httpClient = null) : ITallyXmlClient
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<string> SendAsync(string requestXml, ConnectionProfile profile, CancellationToken cancellationToken)
    {
        if (profile.Protocol != TallyProtocol.HttpXml || profile.Method != TallyConnectionMethod.XmlHttp)
            throw new TallyProtocolException("The configured profile is not an XML/HTTP profile.");
        if (string.IsNullOrWhiteSpace(profile.Host) || profile.Port is < 1 or > 65535)
            throw new ArgumentException("Host and port must be valid.", nameof(profile));

        var endpoint = new UriBuilder(Uri.UriSchemeHttp, profile.Host, profile.Port).Uri;
        using var content = new StringContent(requestXml, System.Text.Encoding.UTF8, "text/xml");
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/xml; charset=utf-8");
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new TallyConnectionException($"TallyPrime returned HTTP {(int)response.StatusCode}.");
        return responseBody;
    }
}
