using System;

namespace ComputeSharp.Exceptions;

/// <summary>
/// A custom <see cref="Exception"/> type that indicates when a shader compilation with the DXC compiler has failed.
/// </summary>
public sealed class DxcCompilationException : Exception
{
    /// <summary>
    /// Creates a new <see cref="DxcCompilationException"/> instance.
    /// </summary>
    /// <param name="error">The error message produced by the DXC compiler.</param>
    internal DxcCompilationException(string error)
        : base(GetExceptionMessage(error))
    {
    }

    /// <summary>
    /// Gets a formatted exception message for a given compilation error.
    /// </summary>
    /// <param name="error">The input compilatin error message from the DXC compiler.</param>
    /// <returns>A formatted error message for a new <see cref="DxcCompilationException"/> instance.</returns>
    private static string GetExceptionMessage(string error)
    {
#if NET6_0_OR_GREATER
        ReadOnlySpan<char> message = error.AsSpan().Trim();
#else
        string message = error.Trim();
#endif

        return
            $"""The DXC compiler encountered one or more errors while trying to compile the shader: "{message}". """ +
            $"""Make sure to only be using supported features by checking the README file in the ComputeSharp repository: https://github.com/Sergio0694/ComputeSharp. """ +
            $"""If you're sure that your C# shader code is valid, please open an issue an include a working repro and this error message.""";
    }
}