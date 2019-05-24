using System;
using System.Threading.Tasks;
using wRPC;

namespace Contract
{
    [ControllerContract("Utils")]
    public interface IUtilsController
    {
        Task<byte[]> ShrinkImage(Uri ImageUri, int pixelSize);
    }
}
