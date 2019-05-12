using System;

namespace Contract.Dto
{
    public sealed class ShrinkImageRequest
    {
        public Uri ImageUri { get; set; }
        public int Size { get; set; }
    }
}