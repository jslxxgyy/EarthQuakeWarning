using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using EarthquakeWaring.App.Infrastructure.Models.EarthQuakeModels;
using EarthquakeWaring.App.Infrastructure.Models.SettingModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;

namespace EarthquakeWaring.App.Infrastructure.Models.ApiModels;

public interface IEarthQuakeApiWrapper : IEarthQuakeApi
{
    
}

public class EarthQuakeApiWrapper : IEarthQuakeApiWrapper
{

    private readonly List<IEarthQuakeApi> _earthQuakeApis;
    private readonly ISetting<UpdaterSetting> _updaterSetting;
    
    public EarthQuakeApiWrapper(IEnumerable<IEarthQuakeApi> earthQuakeApis, ISetting<UpdaterSetting> updaterSetting)
    {
        _updaterSetting = updaterSetting;
        _earthQuakeApis = earthQuakeApis.ToList();
    }
    
    public async Task<List<EarthQuakeInfoBase>> GetEarthQuakeList(long startTimePointer, CancellationToken cancellationToken)
    {
        return await _earthQuakeApis[ResolveApiIndex()].GetEarthQuakeList(startTimePointer, cancellationToken);
    }

    public async Task<List<EarthQuakeInfoBase>> GetEarthQuakeInfo(string earthQuakeId, CancellationToken cancellationToken)
    {
        return await _earthQuakeApis[ResolveApiIndex()].GetEarthQuakeInfo(earthQuakeId, cancellationToken);
    }

    /// <summary>把 ApiType 限制在合法范围内，避免配置文件越界导致后台服务崩溃</summary>
    private int ResolveApiIndex()
    {
        var index = _updaterSetting.Setting?.ApiType ?? 0;
        return Math.Clamp(index, 0, _earthQuakeApis.Count - 1);
    }
}