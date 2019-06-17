using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using wRPC;

namespace StubClient.Interfaces
{
    [ControllerContract("Profile")]
    public interface IProfileController
    {
        Uri UpdateAvatar(byte[] image);
    }
}
