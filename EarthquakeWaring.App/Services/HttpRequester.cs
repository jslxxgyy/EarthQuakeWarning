using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace EarthquakeWaring.App.Services;

public class HttpRequester : IHttpRequester
{
    // 复用 HttpClient 避免端口耗尽；设置超时避免长时间挂起
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };


    public async Task<string> GetString(string url, Dictionary<string, string>? data,
        CancellationToken cancellationToken = default)
    {
        if (data != null)
        {
            url += "?" + string.Join("&",
                data.Select(t => UrlEncoder.Default.Encode(t.Key) + "=" + UrlEncoder.Default.Encode(t.Value)));
        }

        var responseMessage = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        responseMessage.EnsureSuccessStatusCode();
        return await responseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }


    public async Task<string> PostString(string url, HttpContent content, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}