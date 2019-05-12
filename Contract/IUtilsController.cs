using Contract.Dto;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using wRPC;

namespace Contract
{
    [ControllerContract("Utils")]
    public interface IUtilsController
    {
        Task<byte[]> ShrinkImage(ShrinkImageRequest shrinkImage);
    }
}
