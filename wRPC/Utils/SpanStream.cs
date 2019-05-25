namespace System.IO
{
    internal sealed class SpanStream : Stream
    {
        private readonly Memory<byte> _memory;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => _memory.Length;
        private int _Position;
        public override long Position
        {
            get => _Position;
            set
            {
                if (value > _memory.Length)
                    throw new InvalidOperationException("Value greater than stream length");

                _Position = (int)value;
            }
        }

        public SpanStream(Memory<byte> memory)
        {
            _memory = memory;
        }

        public override void Flush()
        {
            
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Memory<byte> dst = buffer.AsMemory(offset, count);

            // Сколько осталось байт в буффере.
            int leftCount = _memory.Length - _Position;

            if (count > leftCount)
                count = leftCount;

            Memory<byte> src = _memory.Slice(_Position, count);

            src.CopyTo(dst);

            _Position += count;

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Сколько осталось байт в буффере.
            int leftCount = _memory.Length - _Position;

            if (leftCount >= count)
            {
                Memory<byte> src = buffer.AsMemory(offset, count);
                Memory<byte> dst = _memory.Slice(_Position, count);
                src.CopyTo(dst);
                _Position += count;
            }
            else
            {
                throw new NotSupportedException("Memory stream is not expandable.");
            }
        }
    }
}
