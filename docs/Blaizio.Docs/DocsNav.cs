namespace Blaizio.Docs;

/// <summary>
/// One documented component: its route slug, sidebar label, a one-line blurb (shown on the
/// prev/next cards), its category (the badge on its doc page and the index filter chips), and the
/// API family rendered on its dedicated API page. A null <see cref="Api"/> means there is no
/// generated API page (e.g. the imperative Toast service documents its own interface inline), so
/// its API link falls back to the doc page.
/// </summary>
public sealed record DocEntry(string Slug, string Label, string Blurb, string Category, Type[]? Api)
{
    /// <summary>Route to the component's documentation page.</summary>
    public string DocHref => $"docs/components/{Slug}";

    /// <summary>Route to the component's API page when it has one, else its doc page.</summary>
    public string ApiHref => Api is not null ? $"docs/components/{Slug}/api" : DocHref;

    /// <summary>True when this component has a generated API page.</summary>
    public bool HasApiPage => Api is not null;
}

/// <summary>A cross-cutting guide in the "Getting started" group (not a component).</summary>
public sealed record GuideEntry(string Href, string Label, string Match = "Prefix");

/// <summary>A top-level site header item (also mirrored into the mobile nav sheet).</summary>
public sealed record SiteNavEntry(string Href, string Label);

/// <summary>
/// The docs navigation registry - the single source of truth behind the sidebar (both the
/// Components and API Reference tabs), the per-component API pages, and the prev/next footer.
/// Keep <see cref="Components"/> in display order; add an entry when a new component lands.
/// </summary>
public static class DocsNav
{
    /// <summary>The site header items (Docs points at the Introduction, the /docs root). "Themes"
    /// is the /create composer - a regular nav item on desktop and in the mobile menu sheet.</summary>
    public static readonly SiteNavEntry[] SiteNav =
    [
        new("docs", "Docs"),
        new("docs/components", "Components"),
        new("examples", "Examples"),
        new("charts", "Charts"),
        new("themes", "Themes"),
    ];

    public static readonly GuideEntry[] Guides =
    [
        new("docs", "Introduction", Match: "All"),
        new("docs/installation", "Installation"),
        new("docs/base", "Blaizio.Base"),
        new("docs/components", "Components"),
        new("docs/cli", "CLI"),
        new("docs/theming", "Theming"),
        new("docs/direction", "Direction (RTL)"),
        new("docs/dialog-service", "Dialog Service"),
    ];

    public static readonly DocEntry[] Components =
    [
        new("accordion", "Accordion", "Vertically stacked, collapsible sections.", "Display", ApiFamilies.Accordion),
        new("alert", "Alert", "A callout for important messages.", "Feedback", ApiFamilies.Alert),
        new("alert-dialog", "Alert Dialog", "A modal that interrupts for a response.", "Overlays", ApiFamilies.AlertDialog),
        new("aspect-ratio", "Aspect Ratio", "Constrain content to a fixed ratio.", "Layout", ApiFamilies.AspectRatio),
        new("attachment", "Attachment", "File cards with a drag-and-drop zone.", "Forms", ApiFamilies.Attachment),
        new("avatar", "Avatar", "An image with a graceful fallback.", "Display", ApiFamilies.Avatar),
        new("badge", "Badge", "A small label for status or counts.", "Display", ApiFamilies.Badge),
        new("breadcrumb", "Breadcrumb", "The path to the current page.", "Navigation", ApiFamilies.Breadcrumb),
        new("bubble", "Bubble", "Framed conversational chat bubbles.", "Display", ApiFamilies.Bubble),
        new("button", "Button", "Triggers an action. Variants and sizes.", "Actions", ApiFamilies.Button),
        new("button-group", "Button Group", "Related buttons with merged edges.", "Actions", ApiFamilies.ButtonGroup),
        new("calendar", "Calendar", "A culture-aware date-grid picker.", "Forms", ApiFamilies.Calendar),
        new("card", "Card", "A bordered surface that groups content.", "Display", ApiFamilies.Card),
        new("carousel", "Carousel", "A scroll-snap slideshow of items.", "Display", ApiFamilies.Carousel),
        new("chart", "Chart", "Pure-SVG bar, line, area, scatter, pie, radar, and radial charts.", "Display", ApiFamilies.Chart),
        new("checkbox", "Checkbox", "A toggle for a single option.", "Forms", ApiFamilies.Checkbox),
        new("collapsible", "Collapsible", "Show and hide a section of content.", "Display", ApiFamilies.Collapsible),
        new("combobox", "Combobox", "An input with an autocomplete list.", "Forms", ApiFamilies.Combobox),
        new("command", "Command", "A command palette for fast actions.", "Actions", ApiFamilies.Command),
        new("context-menu", "Context Menu", "A menu opened on right-click.", "Overlays", ApiFamilies.ContextMenu),
        new("dialog", "Dialog", "A window overlaid on the page.", "Overlays", ApiFamilies.Dialog),
        new("drawer", "Drawer", "A panel that slides from an edge.", "Overlays", ApiFamilies.Drawer),
        new("dropdown-menu", "Dropdown Menu", "A menu of actions from a trigger.", "Overlays", ApiFamilies.DropdownMenu),
        new("empty", "Empty", "A placeholder for states with nothing to show.", "Feedback", ApiFamilies.Empty),
        new("field", "Field", "Label, control, hint and error together.", "Forms", ApiFamilies.Field),
        new("hover-card", "Hover Card", "Preview content on hover.", "Overlays", ApiFamilies.HoverCard),
        new("icons", "Icons", "The full Tabler icon set.", "Utilities", ApiFamilies.Icon),
        new("image", "Image", "A content image with load states.", "Display", ApiFamilies.Image),
        new("input-date", "Input Date", "An inline date field.", "Forms", ApiFamilies.InputDate),
        new("input-group", "Input Group", "Group inputs with addons and buttons.", "Forms", ApiFamilies.InputGroup),
        new("input-number", "Input Number", "A numeric input with steppers.", "Forms", ApiFamilies.InputNumber),
        new("input-otp", "Input OTP", "A one-time-passcode entry field.", "Forms", ApiFamilies.InputOtp),
        new("input-tags", "Input Tags", "Free-text tags as removable chips.", "Forms", ApiFamilies.InputTags),
        new("input-text", "Input Text", "A single-line text input.", "Forms", ApiFamilies.InputText),
        new("input-time", "Input Time", "An inline time field.", "Forms", ApiFamilies.InputTime),
        new("item", "Item", "A media, content and actions row.", "Display", ApiFamilies.Item),
        new("kbd", "Kbd", "Renders a keyboard key.", "Utilities", ApiFamilies.Kbd),
        new("label", "Label", "An accessible label for a control.", "Forms", ApiFamilies.Label),
        new("marker", "Marker", "Inline status rows and separators.", "Display", ApiFamilies.Marker),
        new("menubar", "Menubar", "A desktop-style application menu bar.", "Navigation", ApiFamilies.Menubar),
        new("message", "Message", "A conversation turn with avatar and bubbles.", "Display", ApiFamilies.Message),
        new("message-scroller", "Message Scroller", "A chat transcript scroller for streaming.", "Display", ApiFamilies.MessageScroller),
        new("navigation-menu", "Navigation Menu", "A bar with rich dropdown panels.", "Navigation", ApiFamilies.NavigationMenu),
        new("pagination", "Pagination", "Navigate between pages of content.", "Navigation", ApiFamilies.Pagination),
        new("popover", "Popover", "Floating content anchored to a trigger.", "Overlays", ApiFamilies.Popover),
        new("progress", "Progress", "Shows the completion of a task.", "Feedback", ApiFamilies.Progress),
        new("qr-code", "QR Code", "Themeable SVG QR codes with a center logo.", "Display", ApiFamilies.QrCode),
        new("radio-group", "Radio Group", "Pick one option from a set.", "Forms", ApiFamilies.RadioGroup),
        new("resizable", "Resizable", "Drag-to-resize panel groups.", "Layout", ApiFamilies.Resizable),
        new("scroll-area", "Scroll Area", "A styled, cross-browser scroll container.", "Display", ApiFamilies.ScrollArea),
        new("select", "Select", "Choose one value from a dropdown.", "Forms", ApiFamilies.Select),
        new("separator", "Separator", "A visual divider between content.", "Layout", ApiFamilies.Separator),
        new("sheet", "Sheet", "A panel that slides in from an edge.", "Overlays", ApiFamilies.Sheet),
        new("sidebar", "Sidebar", "A composable application sidebar.", "Navigation", ApiFamilies.Sidebar),
        new("skeleton", "Skeleton", "A placeholder while content loads.", "Feedback", ApiFamilies.Skeleton),
        new("slider", "Slider", "Pick a value from a range.", "Forms", ApiFamilies.Slider),
        new("sortable", "Sortable", "Drag to reorder or swap items.", "Display", ApiFamilies.Sortable),
        new("spinner", "Spinner", "A loading indicator.", "Feedback", ApiFamilies.Spinner),
        new("switch", "Switch", "Toggle between on and off.", "Forms", ApiFamilies.Switch),
        new("table", "Table", "A virtualized data table.", "Display", ApiFamilies.Table),
        new("toc", "Table of Contents", "An on-this-page navigation rail.", "Navigation", ApiFamilies.TableOfContents),
        new("tabs", "Tabs", "Layered sections shown one at a time.", "Navigation", ApiFamilies.Tabs),
        new("theme-switcher", "Theme Switcher", "Light / dark / system pickers.", "Utilities", ApiFamilies.ThemeSwitcher),
        new("toast", "Toast", "Imperative notification toasts.", "Feedback", null),
        new("toggle", "Toggle", "A two-state button.", "Forms", ApiFamilies.Toggle),
        new("toggle-group", "Toggle Group", "A set of toggle buttons.", "Forms", ApiFamilies.ToggleGroup),
        new("tooltip", "Tooltip", "A popup label on hover or focus.", "Overlays", ApiFamilies.Tooltip),
        new("tree", "Tree", "A hierarchical tree view with drag and drop.", "Display", ApiFamilies.Tree),
        new("virtualizer", "Virtualizer", "Render only the items in view.", "Display", ApiFamilies.Virtualizer),
    ];

    private static readonly Dictionary<string, int> _index =
        Components.Select((c, i) => (c.Slug, i)).ToDictionary(t => t.Slug, t => t.i);

    /// <summary>The component whose slug this is, or null.</summary>
    public static DocEntry? Find(string? slug) =>
        slug is not null && _index.TryGetValue(slug, out var i) ? Components[i] : null;

    /// <summary>The component before <paramref name="slug"/> in display order, or null at the start.</summary>
    public static DocEntry? Prev(string slug) =>
        _index.TryGetValue(slug, out var i) && i > 0 ? Components[i - 1] : null;

    /// <summary>The component after <paramref name="slug"/> in display order, or null at the end.</summary>
    public static DocEntry? Next(string slug) =>
        _index.TryGetValue(slug, out var i) && i < Components.Length - 1 ? Components[i + 1] : null;

    /// <summary>Only the components that have a dedicated API page (for the API Reference tab).</summary>
    public static IEnumerable<DocEntry> WithApi => Components.Where(c => c.HasApiPage);
}
