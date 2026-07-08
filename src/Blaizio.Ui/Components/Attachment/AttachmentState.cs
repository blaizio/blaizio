using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Upload lifecycle of a <see cref="BzAttachment"/>, flowed to the style sheet via <c>data-state</c>.</summary>
public enum AttachmentState
{
    /// <summary>At rest - nothing in flight.</summary>
    [Description("idle")]
    Idle,

    /// <summary>Bytes are being uploaded.</summary>
    [Description("uploading")]
    Uploading,

    /// <summary>Uploaded; the server is working on it.</summary>
    [Description("processing")]
    Processing,

    /// <summary>The upload or processing failed.</summary>
    [Description("error")]
    Error,

    /// <summary>Finished successfully.</summary>
    [Description("done")]
    Done,
}
