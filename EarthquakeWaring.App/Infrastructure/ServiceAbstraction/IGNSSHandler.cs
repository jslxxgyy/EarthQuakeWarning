using System.Threading;
using System.Threading.Tasks;

namespace EarthquakeWaring.App.Infrastructure.ServiceAbstraction
{
    public interface ILocationHandler
    {
        public Task<bool> GetCurrentInfoAsync(CancellationToken token = default);
    }
}
