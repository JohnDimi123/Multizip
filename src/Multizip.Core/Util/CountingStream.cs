using System;
using System.IO;

namespace Multizip.Core.Util
{
    /// <summary>
    /// Read-only stream wrapper that raises <see cref="BytesRead"/> with the number
    /// of bytes handed out on each read, so a compressor that pulls from a source
    /// file can be tracked for progress reporting.
    /// </summary>
    public sealed class CountingStream : Stream
    {
        private readonly Stream _inner;

        public CountingStream(Stream inner) => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        /// <summary>Invoked with the delta (number of bytes) on every successful read.</summary>
        public event Action<int> BytesRead;

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = _inner.Read(buffer, offset, count);
            if (n > 0) BytesRead?.Invoke(n);
            return n;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() => _inner.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
