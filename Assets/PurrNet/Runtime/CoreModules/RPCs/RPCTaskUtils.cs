using System.Threading.Tasks;

namespace PurrNet.Modules
{
    [UsedByIL]
    public class RPCTaskUtils
    {
        [UsedByIL]
        public static Task<T> WaitTask<T>()
        {
            return Task.FromResult<T>(default);
        }
    }
}
