using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// Render + validation tests for the headless dropzone: the data contract (dragging/disabled),
/// the stretched native input, and the Accept/MaxFileSize/MaxFiles/Multiple split into
/// accepted/rejected.
/// </summary>
public class AttachmentDropzoneRenderTests : TestContext
{
    private sealed class FakeFile(string name, string contentType, long size = 10) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => default;
        public long Size => size;
        public string ContentType => contentType;
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => Stream.Null;
    }

    private static Task ChangeAsync(IRenderedComponent<BaseAttachmentDropzone> cut, params IBrowserFile[] files)
        => cut.InvokeAsync(() => cut.FindComponent<InputFile>().Instance.OnChange.InvokeAsync(
            new InputFileChangeEventArgs([.. files])));

    [Fact]
    public void Renders_zone_hook_and_covering_input()
    {
        var cut = RenderComponent<BaseAttachmentDropzone>();

        var zone = cut.Find("[data-slot=attachment-dropzone]");
        Assert.Null(zone.GetAttribute("data-dragging"));
        Assert.Contains("position:relative", zone.GetAttribute("style"));

        var input = cut.Find("input[type=file]");
        Assert.Equal("attachment-dropzone-input", input.GetAttribute("data-slot"));
        Assert.NotNull(input.GetAttribute("multiple"));
        Assert.Equal("Upload files", input.GetAttribute("aria-label"));
    }

    [Fact]
    public void Disabled_emits_data_disabled_and_disables_the_input()
    {
        var cut = RenderComponent<BaseAttachmentDropzone>(p => p.Add(x => x.Disabled, true));

        Assert.NotNull(cut.Find("[data-slot=attachment-dropzone]").GetAttribute("data-disabled"));
        Assert.NotNull(cut.Find("input[type=file]").GetAttribute("disabled"));
    }

    [Fact]
    public void Drag_enter_and_leave_toggle_the_dragging_flag()
    {
        var cut = RenderComponent<BaseAttachmentDropzone>();
        var zone = cut.Find("[data-slot=attachment-dropzone]");

        zone.DragEnter();
        Assert.NotNull(cut.Find("[data-slot=attachment-dropzone]").GetAttribute("data-dragging"));

        zone.DragLeave();
        Assert.Null(cut.Find("[data-slot=attachment-dropzone]").GetAttribute("data-dragging"));
    }

    [Fact]
    public async Task Valid_files_arrive_via_OnFilesAccepted()
    {
        IReadOnlyList<IBrowserFile>? accepted = null;
        var rejectedCalled = false;
        var cut = RenderComponent<BaseAttachmentDropzone>(p => p
            .Add(x => x.OnFilesAccepted, f => accepted = f)
            .Add(x => x.OnFilesRejected, _ => rejectedCalled = true));

        await ChangeAsync(cut, new FakeFile("a.png", "image/png"), new FakeFile("b.pdf", "application/pdf"));

        Assert.NotNull(accepted);
        Assert.Equal(["a.png", "b.pdf"], accepted!.Select(f => f.Name));
        Assert.False(rejectedCalled);
    }

    [Fact]
    public async Task Accept_matches_extension_wildcard_and_exact_mime()
    {
        IReadOnlyList<IBrowserFile>? accepted = null;
        IReadOnlyList<AttachmentRejection>? rejected = null;
        var cut = RenderComponent<BaseAttachmentDropzone>(p => p
            .Add(x => x.Accept, "image/*,.pdf,text/plain")
            .Add(x => x.OnFilesAccepted, f => accepted = f)
            .Add(x => x.OnFilesRejected, r => rejected = r));

        await ChangeAsync(cut,
            new FakeFile("photo.png", "image/png"),          // image/* wildcard
            new FakeFile("doc.PDF", "application/pdf"),      // .pdf extension, case-insensitive
            new FakeFile("notes.txt", "text/plain"),         // exact mime
            new FakeFile("virus.exe", "application/x-msdownload"));

        Assert.Equal(["photo.png", "doc.PDF", "notes.txt"], accepted!.Select(f => f.Name));
        var rejection = Assert.Single(rejected!);
        Assert.Equal("virus.exe", rejection.File.Name);
        Assert.Equal(AttachmentRejectionReason.UnsupportedType, rejection.Reason);
    }

    [Fact]
    public async Task Oversized_files_are_rejected_as_TooLarge()
    {
        IReadOnlyList<AttachmentRejection>? rejected = null;
        var cut = RenderComponent<BaseAttachmentDropzone>(p => p
            .Add(x => x.MaxFileSize, 100L)
            .Add(x => x.OnFilesRejected, r => rejected = r));

        await ChangeAsync(cut, new FakeFile("big.png", "image/png", size: 101));

        Assert.Equal(AttachmentRejectionReason.TooLarge, Assert.Single(rejected!).Reason);
    }

    [Fact]
    public async Task Excess_files_are_rejected_as_TooMany()
    {
        IReadOnlyList<IBrowserFile>? accepted = null;
        IReadOnlyList<AttachmentRejection>? rejected = null;
        var cut = RenderComponent<BaseAttachmentDropzone>(p => p
            .Add(x => x.MaxFiles, 1)
            .Add(x => x.OnFilesAccepted, f => accepted = f)
            .Add(x => x.OnFilesRejected, r => rejected = r));

        await ChangeAsync(cut, new FakeFile("first.png", "image/png"), new FakeFile("second.png", "image/png"));

        Assert.Equal("first.png", Assert.Single(accepted!).Name);
        var rejection = Assert.Single(rejected!);
        Assert.Equal("second.png", rejection.File.Name);
        Assert.Equal(AttachmentRejectionReason.TooMany, rejection.Reason);
    }

    [Fact]
    public async Task Single_file_zone_takes_only_the_first()
    {
        IReadOnlyList<IBrowserFile>? accepted = null;
        var cut = RenderComponent<BaseAttachmentDropzone>(p => p
            .Add(x => x.Multiple, false)
            .Add(x => x.OnFilesAccepted, f => accepted = f));

        await ChangeAsync(cut, new FakeFile("one.png", "image/png"), new FakeFile("two.png", "image/png"));

        Assert.Equal("one.png", Assert.Single(accepted!).Name);
        Assert.Null(cut.Find("input[type=file]").GetAttribute("multiple"));
    }
}
