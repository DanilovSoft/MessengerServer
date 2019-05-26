using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using wRPC;
using Contract;
using System.Drawing.Imaging;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MessengerServer.Controllers
{
    public sealed class UtilsController : ServerController, IUtilsController
    {
        private const long AvatarQuality = 85; // В процентах.
        private readonly ILogger _logger;

        public UtilsController(ILogger<UtilsController> logger)
        {
            _logger = logger;
        }

        [ProducesProtoBuf]
        public async Task<byte[]> ShrinkImage(Uri ImageUri, int pixelSize)
        {
            _logger.LogInformation($"Сжатие изображения до {pixelSize}px");

            using (Bitmap bitmap = await ResizeImageAsync(ImageUri, pixelSize))
            {
                using (var mem = new MemoryPoolStream(4096))
                {
                    BitmapToJpeg(mem, bitmap);
                    byte[] serialized = mem.ToArray();
                    return serialized;
                }
            }
        }

        private void BitmapToJpeg(Stream stream, Bitmap bitmap)
        {
            var qualityEncoder = System.Drawing.Imaging.Encoder.Quality;
            long quality = AvatarQuality; // В процентах.
            using (var ratio = new EncoderParameter(qualityEncoder, quality))
            {
                using (var codecParams = new EncoderParameters(count: 1))
                {
                    codecParams.Param[0] = ratio;
                    ImageCodecInfo jpegCodecInfo = ImageCodecInfo.GetImageEncoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
                    bitmap.Save(stream, jpegCodecInfo, codecParams); // Save to JPG.
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
