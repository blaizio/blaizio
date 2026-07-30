namespace Blaizio;

/// <summary>Load status of a <see cref="BaseImage"/>'s img element (also the avatar image status).</summary>
public enum ImageStatus
{
    /// <summary>No source to load (empty <c>Src</c>).</summary>
    Idle,

    /// <summary>The request is in flight.</summary>
    Loading,

    /// <summary>The image loaded successfully.</summary>
    Loaded,

    /// <summary>The source failed to load.</summary>
    Error
}
