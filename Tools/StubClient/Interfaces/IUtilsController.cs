using System;
using System.Threading.Tasks;
using vRPC;

namespace Contract
{
    [ControllerContract("Utils")]
    public interface IUtilsController
    {
        Task<byte[]> ShrinkImage(Uri ImageUri, int pixelSize);
    }
}
