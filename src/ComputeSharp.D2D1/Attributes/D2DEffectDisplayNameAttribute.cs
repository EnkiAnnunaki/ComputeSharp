using System;

namespace ComputeSharp.D2D1;

/// <summary>
/// An attribute for a D2D1 shader indicating the display name of the shader effect to create.
/// </summary>
/// <remarks>
/// This only applies to effects created from <see cref="Interop.D2D1PixelShaderEffect"/>.
/// </remarks>
/// <param name="value">The value for the display name.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
public sealed class D2DEffectDisplayNameAttribute(string value) : Attribute
{
    /// <summary>
    /// Gets the display name value.
    /// </summary>
    public string Value { get; } = value;
}