using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using wRPC;
using Contract;
using Contract.Dto;

namespace MessengerServer.Controllers
{
    public sealed class UtilsController : ServerController, IUtilsController
    {
        public UtilsController()
        {

        }

        public async Task<byte[]> ShrinkImage(ShrinkImageRequest shrinkImage)
        {
            using (Bitmap bitmap = await ResizeImageAsync(shrinkImage.ImageUri, shrinkImage.Size))
            {
                using (var mem = new MemoryStream())
                {
                    bitmap.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
                    return mem.ToArray();
                }
            }
        }

        private async Task<Bitmap> ResizeImageAsync(Uri imageUri, int size)
        {
            using (var httpClient = new HttpClient())
            {
                var respStream = await httpClient.GetStreamAsync(imageUri);
                using (var img = Image.FromStream(respStream))
                {
                    int height = size;
                    int width = size;

                    if (img.Width > img.Height)
                    {
                        double prop = (double)img.Width / img.Height;
                        width = (int)(height * prop);
                    }
                    else if (img.Height > img.Width)
                    {
                        double prop = (double)img.Height / img.Width;
                        height = (int)(width * prop);
                    }

                    var bitmap = new Bitmap(width, height);
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.DrawImage(img, 0, 0, width, height);
                        return bitmap;
                    }
                }
            }
        }
    }
}
