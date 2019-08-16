using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using vRPC;

namespace MessengerServer.Controllers
{
    public sealed class ProfileController : ServerController
    {
        public ProfileController()
        {

        }

        public Uri UpdateAvatar(byte[] image)
        {
            Image img;
            using (var mem = new MemoryStream(image))
                img = Image.FromStream(mem);

            return new Uri("https://theburningmonk.com/wp-content/uploads/2011/12/image3.png");
        }
    }
}
