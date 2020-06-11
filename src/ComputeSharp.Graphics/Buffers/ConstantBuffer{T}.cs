﻿using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ComputeSharp.Exceptions;
using ComputeSharp.Graphics;
using ComputeSharp.Graphics.Buffers.Abstract;
using ComputeSharp.Graphics.Buffers.Enums;
using ComputeSharp.Graphics.Helpers;
using Vortice.Direct3D12;
using CommandList = ComputeSharp.Graphics.Commands.CommandList;

namespace ComputeSharp
{
    /// <summary>
    /// A <see langword="class"/> representing a typed read write buffer stored on GPU memory
    /// </summary>
    /// <typeparam name="T">The type of items stored on the buffer</typeparam>
    public sealed class ConstantBuffer<T> : HlslBuffer<T> where T : unmanaged
    {
        /// <summary>
        /// Creates a new <see cref="ConstantBuffer{T}"/> instance with the specified parameters
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/> associated with the current instance</param>
        /// <param name="size">The number of items to store in the current buffer</param>
        internal ConstantBuffer(GraphicsDevice device, int size) : base(device, size, size * GetPaddedSize(), BufferType.Constant) { }

        /// <summary>
        /// Gets the right padded size for <typeparamref name="T"/> elements to store in the current instance
        /// </summary>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetPaddedSize()
        {
            int size = Unsafe.SizeOf<T>();
            return (size + 15) & ~15;
        }

        /// <summary>
        /// Gets a single <typeparamref name="T"/> value from the current constant buffer
        /// </summary>
        /// <param name="i">The index of the value to get</param>
        /// <remarks>This API can only be used from a compute shader, and will always throw if used anywhere else</remarks>
        public T this[int i] => throw new InvalidExecutionContextException($"{nameof(ConstantBuffer<T>)}<T>[int]");

        /// <inheritdoc/>
        public override unsafe void GetData(Span<T> span, int offset, int count)
        {
            using MappedResource resource = MapResource();
            if (IsPaddingPresent)
            {
                // Copy the padded data to the temporary array
                {
                    ref var dest = ref MemoryMarshal.GetReference(span);
                    ref byte untypedSrc = ref Unsafe.AsRef<byte>((void*)resource.Pointer);
                    untypedSrc = ref Unsafe.Add(ref untypedSrc, offset * GetPaddedSize());

                    for (var i = 0; i < count; i++)
                    {
                        Unsafe.Add(ref dest, i) = Unsafe.As<byte, T>(ref Unsafe.Add(ref untypedSrc, i * GetPaddedSize()));
                    }
                }
            }
            else
            {
                MemoryHelper.Copy(resource.Pointer, offset, span, 0, count);
            }
        }

        /// <inheritdoc/>
        public override unsafe void SetData(ReadOnlySpan<T> span, int offset, int count)
        {
            using MappedResource resource = MapResource();
            if (IsPaddingPresent)
            {
                // Copy element-by-element
                ref var src = ref MemoryMarshal.GetReference(span);
                ref byte untypedDest = ref Unsafe.AsRef<byte>((void*)resource.Pointer);
                untypedDest = ref Unsafe.Add(ref untypedDest, offset * GetPaddedSize());

                for (var i = 0; i < count; i++)
                {
                    Unsafe.As<byte, T>(ref Unsafe.Add(ref untypedDest, i * GetPaddedSize())) = Unsafe.Add(ref src, i);
                }
            }
            // Directly copy as there is no padding
            else MemoryHelper.Copy(span, resource.Pointer, offset, count);
        }

        /// <inheritdoc/>
        public override void SetData(HlslBuffer<T> buffer)
        {
            if (buffer is ConstantBuffer<T>)
            {
                // Directly copy the input buffer, if possible
                using CommandList copyCommandList = new CommandList(GraphicsDevice, CommandListType.Copy);

                copyCommandList.CopyBufferRegion(buffer, 0, this, 0, SizeInBytes);
                copyCommandList.Flush();
            }
            else SetDataWithCpuBuffer(buffer);
        }
    }
}
