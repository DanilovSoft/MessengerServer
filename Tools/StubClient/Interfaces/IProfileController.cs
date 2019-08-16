using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using vRPC;

namespace StubClient.Interfaces
{
    [ControllerContract("Profile")]
    public interface IProfileController
    {
        Uri UpdateAvatar(byte[] image);
    }
}
