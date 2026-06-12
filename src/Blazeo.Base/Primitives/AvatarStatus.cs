namespace Blazeo;

/// <summary>Loading state of a <see cref="BlazeAvatarImage"/>, shared with the fallback.</summary>
public enum AvatarStatus
{
    /// <summary>The image hasn't loaded yet.</summary>
    Loading,

    /// <summary>The image loaded successfully - show it, hide the fallback.</summary>
    Loaded,

    /// <summary>The image failed to load - keep the fallback.</summary>
    Error,
}
