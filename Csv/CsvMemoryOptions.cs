#if NET8_0_OR_GREATER

using System;
using System.Buffers;

namespace Csv
{
    /// <summary>
    /// Configuration options for memory-efficient CSV operations using pooled buffers.
    /// </summary>
    public sealed class CsvMemoryOptions
    {
        /// <summary>
        /// Gets or sets the memory pool to use for buffer allocation. If null, uses the default ArrayPool.
        /// </summary>
        public MemoryPool<char>? MemoryPool { get; set; }

        /// <summary>
        /// Gets or sets the initial buffer size for pooled operations. Default is 4096 characters.
        /// </summary>
        public int InitialBufferSize { get; set; } = 4096;

        /// <summary>
        /// Gets or sets whether to reuse buffers across operations. Default is true.
        /// </summary>
        public bool ReuseBuffers { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum buffer size that can be allocated. Default is 1MB (524,288 characters).
        /// </summary>
        public int MaxBufferSize { get; set; } = 524_288;

        /// <summary>
        /// Gets or sets whether to use vectorized operations when available. Default is true.
        /// </summary>
        public bool UseVectorization { get; set; } = true;

        /// <summary>
        /// Gets or sets the threshold for switching to streaming mode for large data. Default is 64KB.
        /// </summary>
        public int StreamingThreshold { get; set; } = 65_536;

        /// <summary>
        /// Gets or sets whether to enable zero-copy parsing when possible. Default is true.
        /// </summary>
        public bool EnableZeroCopy { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to clear rented buffers for security. Default is false for performance.
        /// </summary>
        public bool ClearBuffers { get; set; } = false;

        /// <summary>
        /// Gets or sets the threshold below which direct allocation is used instead of buffer pooling.
        /// This reduces memory overhead for small datasets. Default is 2048 characters.
        /// </summary>
        public int DirectAllocationThreshold { get; set; } = 2048;

        /// <summary>
        /// Gets the ArrayPool instance to use for char buffer allocation.
        /// </summary>
        internal ArrayPool<char> CharArrayPool => ArrayPool<char>.Shared;

        /// <summary>
        /// Creates a copy of the current options.
        /// </summary>
        public CsvMemoryOptions Clone()
        {
            return new CsvMemoryOptions
            {
                MemoryPool = MemoryPool,
                InitialBufferSize = InitialBufferSize,
                ReuseBuffers = ReuseBuffers,
                MaxBufferSize = MaxBufferSize,
                UseVectorization = UseVectorization,
                StreamingThreshold = StreamingThreshold,
                EnableZeroCopy = EnableZeroCopy,
                ClearBuffers = ClearBuffers,
                DirectAllocationThreshold = DirectAllocationThreshold
            };
        }

        /// <summary>
        /// Validates the current configuration and throws if invalid.
        /// </summary>
        internal void Validate()
        {
            if (InitialBufferSize <= 0)
                throw new ArgumentException("InitialBufferSize must be positive", nameof(InitialBufferSize));

            if (MaxBufferSize <= 0)
                throw new ArgumentException("MaxBufferSize must be positive", nameof(MaxBufferSize));

            if (InitialBufferSize > MaxBufferSize)
                throw new ArgumentException("InitialBufferSize cannot be larger than MaxBufferSize");

            if (StreamingThreshold <= 0)
                throw new ArgumentException("StreamingThreshold must be positive", nameof(StreamingThreshold));

            if (DirectAllocationThreshold <= 0)
                throw new ArgumentException("DirectAllocationThreshold must be positive", nameof(DirectAllocationThreshold));
        }
    }
}

#endif