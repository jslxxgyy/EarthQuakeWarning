using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EarthquakeWaring.App.Infrastructure.Models.EarthQuakeModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using Microsoft.Extensions.Logging;

namespace EarthquakeWaring.App.Services.EarthQuakeApis;

public class WolfxCencApi : IEarthQuakeApi
{
    // 中国地震台网 地震速报（实时 EEW）
    public string ApiUrl = "https://api.wolfx.jp/cenc_eew.json";

    private readonly IJsonConvertService _jsonConvertService;
    private readonly IHttpRequester _httpRequester;
    private readonly ILogger<WolfxCencApi> _logger;

    public WolfxCencApi(IHttpRequester httpRequester, IJsonConvertService jsonConvertService,
        ILogger<WolfxCencApi> logger)
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
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(5000);
            var result = await _httpRequester.GetString(ApiUrl, null, cts.Token);
            var ret = _jsonConvertService.ConvertTo<WolfxCencEewResponse>(result);
            if (ret is null)
                return new List<EarthQuakeInfoBase>();
            // EEW 速报源返回"最近一条"：用报告时间判断是否为新事件，避免过去的数据被游标过滤导致不可用
            if (DateTimeOffset.FromFileTime(ret.UpdateTime.ToFileTime()).ToUnixTimeMilliseconds() < startTimePointer)
                return new List<EarthQuakeInfoBase>();
            return new List<EarthQuakeInfoBase> { ret.MapToEarthQuakeInfo() };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting earthquake list from WolfxCencApi");
        }

        return new List<EarthQuakeInfoBase>();
    }

    public async Task<List<EarthQuakeInfoBase>> GetEarthQuakeInfo(string earthQuakeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _httpRequester.GetString(ApiUrl, null, cancellationToken);
            var ret = _jsonConvertService.ConvertTo<WolfxCencEewResponse>(result);
            if (ret?.Id != earthQuakeId)
                return new List<EarthQuakeInfoBase>();
            return new List<EarthQuakeInfoBase> { ret.MapToEarthQuakeInfo() };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting earthquake info from WolfxCencApi");
        }

        return new List<EarthQuakeInfoBase>();
    }
}

public class WolfxCencEewResponse
{
    [JsonPropertyName("ID")] public string Id { get; set; } = null!;

    [JsonPropertyName("EventID")] public string EventId { get; set; } = null!;

    [JsonPropertyName("ReportTime")] public DateTime UpdateTime { get; set; }

    [JsonPropertyName("ReportNum")] public int ReportNum { get; set; }

    [JsonPropertyName("OriginTime")] public DateTime StartTime { get; set; }

    [JsonPropertyName("HypoCenter")] public string? PlaceName { get; set; }

    [JsonPropertyName("Latitude")] public double Latitude { get; set; }

    [JsonPropertyName("Longitude")] public double Longitude { get; set; }

    [JsonPropertyName("Magnitude")] public double Magnitude { get; set; }

    [JsonPropertyName("Depth")] public double? Depth { get; set; }

    [JsonPropertyName("MaxIntensity")] public double MaxIntensity { get; set; }
}

public static class WolfxCencResponseToEarthQuakeInfoBaseMapper
{
    public static EarthQuakeInfoBase MapToEarthQuakeInfo(this WolfxCencEewResponse res)
    {
        return new EarthQuakeInfoBase
        {
            Id = res.Id,
            StartAt = res.StartTime,
            UpdateAt = res.UpdateTime,
            Latitude = res.Latitude,
            Longitude = res.Longitude,
            Magnitude = res.Magnitude,
            Depth = res.Depth ?? 0,
            PlaceName = res.PlaceName
        };
    }
}
