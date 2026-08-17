using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EarthquakeWaring.App.Infrastructure.Models.EarthQuakeModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using Microsoft.Extensions.Logging;

namespace EarthquakeWaring.App.Services.EarthQuakeApis;

public class MiuiEarthQuakeApi : IEarthQuakeApi
{
    private const string ApiUrlBase64 =
        "aHR0cHM6Ly9zcnYuc2VjLm1pdWkuY29tL2VhcnRocXVha2Uvd2FybmluZy9yZWNvcmRz";
    private const string SigningKeyBase64 = "N2h0cjUyMzgtYThjZi0zazc5LWVjNzMtNzUzODIxNDVuczVj";

    private static readonly string ApiUrl = DecodeBase64(ApiUrlBase64);
    private static readonly string SigningKey = DecodeBase64(SigningKeyBase64);

    private readonly IHttpRequester _httpRequester;
    private readonly IJsonConvertService _jsonConvertService;
    private readonly ILogger<MiuiEarthQuakeApi> _logger;

    public MiuiEarthQuakeApi(IHttpRequester httpRequester, IJsonConvertService jsonConvertService,
        ILogger<MiuiEarthQuakeApi> logger)
    {
        _httpRequester = httpRequester;
        _jsonConvertService = jsonConvertService;
        _logger = logger;
    }

    public async Task<List<EarthQuakeInfoBase>> GetEarthQuakeList(long startTimePointer,
        CancellationToken cancellationToken)
    {
        try
        {
            var records = await GetRecords(cancellationToken).ConfigureAwait(false);
            return records
                .Where(record => record.UpdateAt >= startTimePointer)
                .OrderByDescending(record => record.UpdateAt)
                .Select(record => record.MapToEarthQuakeInfo())
                .ToList();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error while getting earthquake list from MiuiEarthQuakeApi");
            return new List<EarthQuakeInfoBase>();
        }
    }

    public async Task<List<EarthQuakeInfoBase>> GetEarthQuakeInfo(string earthQuakeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var records = await GetRecords(cancellationToken).ConfigureAwait(false);
            return records
                .Where(record => record.EventId.ToString() == earthQuakeId || record.Id.ToString() == earthQuakeId)
                .OrderBy(record => record.UpdateAt)
                .Select(record => record.MapToEarthQuakeInfo())
                .ToList();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error while getting earthquake info from MiuiEarthQuakeApi");
            return new List<EarthQuakeInfoBase>();
        }
    }

    private async Task<List<MiuiEarthQuakeRecord>> GetRecords(CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(5));

        var parameters = new Dictionary<string, string> { ["version"] = "2" };
        parameters["sign"] = MakeSignature(parameters);

        using var content = new FormUrlEncodedContent(parameters);
        var result = await _httpRequester.PostString(ApiUrl, content, timeoutSource.Token).ConfigureAwait(false);
        var response = _jsonConvertService.ConvertTo<MiuiEarthQuakeResponse>(result);

        if (response?.Code != 0)
            throw new InvalidOperationException($"MIUI API returned {response?.Code}: {response?.Description}");

        return response.Data ?? new List<MiuiEarthQuakeRecord>();
    }

    internal static string MakeSignature(IReadOnlyDictionary<string, string> parameters)
    {
        var canonical = string.Join("&", parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
        var payload = Encoding.UTF8.GetBytes($"{canonical}&{SigningKey}");
        var encoded = Convert.ToBase64String(payload);
        return Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(encoded)));
    }

    private static string DecodeBase64(string value)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }
}

public class MiuiEarthQuakeResponse
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("desc")] public string? Description { get; set; }
    [JsonPropertyName("data")] public List<MiuiEarthQuakeRecord>? Data { get; set; }
}

public class MiuiEarthQuakeRecord
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("eventId")] public long EventId { get; set; }
    [JsonPropertyName("update")] public int Update { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("startAt")] public long StartAt { get; set; }
    [JsonPropertyName("updateAt")] public long UpdateAt { get; set; }
    [JsonPropertyName("magnitude")] public double Magnitude { get; set; }
    [JsonPropertyName("depth")] public double Depth { get; set; }
    [JsonPropertyName("longitude")] public double Longitude { get; set; }
    [JsonPropertyName("latitude")] public double Latitude { get; set; }
    [JsonPropertyName("epicenter")] public string? Epicenter { get; set; }
    [JsonPropertyName("signature")] public string? Signature { get; set; }
}

public static class MiuiEarthQuakeRecordToEarthQuakeInfoMapper
{
    public static EarthQuakeInfoBase MapToEarthQuakeInfo(this MiuiEarthQuakeRecord record)
    {
        return new EarthQuakeInfoBase
        {
            Id = record.EventId.ToString(),
            StartAt = DateTimeOffset.FromUnixTimeMilliseconds(record.StartAt).LocalDateTime,
            UpdateAt = DateTimeOffset.FromUnixTimeMilliseconds(record.UpdateAt).LocalDateTime,
            Latitude = record.Latitude,
            Longitude = record.Longitude,
            Magnitude = record.Magnitude,
            Depth = record.Depth,
            PlaceName = record.Epicenter
        };
    }
}
