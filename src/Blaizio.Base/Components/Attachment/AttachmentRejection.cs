using Microsoft.AspNetCore.Components.Forms;

namespace Blaizio;

/// <summary>Why <see cref="BaseAttachmentDropzone"/> refused a selected or dropped file.</summary>
public enum AttachmentRejectionReason
{
    /// <summary>The file's extension/content type doesn't match the dropzone's <c>Accept</c> list.</summary>
    UnsupportedType,

    /// <summary>The file is larger than <c>MaxFileSize</c>.</summary>
    TooLarge,

    /// <summary>The selection exceeded <c>MaxFiles</c> (or more than one file on a single-file dropzone).</summary>
    TooMany,
}

/// <summary>One refused file with the reason, reported via <c>OnFilesRejected</c>.</summary>
/// <param name="File">The offending browser file (name/size/type still readable).</param>
/// <param name="Reason">Why it was refused.</param>
public sealed record AttachmentRejection(IBrowserFile File, AttachmentRejectionReason Reason);
