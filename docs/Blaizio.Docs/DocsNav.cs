namespace Blaizio.Docs;

/// <summary>
/// One documented component: its route slug, sidebar label, a one-line blurb (shown on the
/// prev/next cards), and the API family rendered on its dedicated API page. A null <see cref="Api"/>
/// means there is no generated API page (e.g. the imperative Toast service documents its own
/// interface inline), so its API link falls back to the doc page.
/// </summary>
public sealed record DocEntry(string Slug, string Label, string Blurb, Type[]? Api)
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
    /// <summary>The site header items (Docs points at the Introduction, the /docs root). The /create
    /// page is a full-page tool outside the docs chrome - it gets the header's "+ Create" button,
    /// not a nav item.</summary>
    public static readonly SiteNavEntry[] SiteNav =
    [
        new("docs", "Docs"),
        new("docs/components", "Components"),
        new("examples", "Examples"),
        new("charts", "Charts"),
    ];

    public static readonly GuideEntry[] Guides =
    [
        new("docs", "Introduction", Match: "All"),
        new("docs/components", "Components"),
        new("docs/installation", "Installation"),
        new("docs/cli", "CLI"),
        new("docs/theming", "Theming"),
        new("docs/direction", "Direction (RTL)"),
        new("docs/dialog-service", "Dialog Service"),
    ];

    public static readonly DocEntry[] Components =
    [
        new("accordion", "Accordion", "Vertically stacked, collapsible sections.", ApiFamilies.Accordion),
        new("alert", "Alert", "A callout for important messages.", ApiFamilies.Alert),
        new("alert-dialog", "Alert Dialog", "A modal that interrupts for a response.", ApiFamilies.AlertDialog),
        new("aspect-ratio", "Aspect Ratio", "Constrain content to a fixed ratio.", ApiFamilies.AspectRatio),
        new("attachment", "Attachment", "File cards with a drag-and-drop zone.", ApiFamilies.Attachment),
        new("avatar", "Avatar", "An image with a graceful fallback.", ApiFamilies.Avatar),
        new("badge", "Badge", "A small label for status or counts.", ApiFamilies.Badge),
        new("breadcrumb", "Breadcrumb", "The path to the current page.", ApiFamilies.Breadcrumb),
        new("button", "Button", "Triggers an action. Variants and sizes.", ApiFamilies.Button),
        new("calendar", "Calendar", "A culture-aware date-grid picker.", ApiFamilies.Calendar),
        new("card", "Card", "A bordered surface that groups content.", ApiFamilies.Card),
        new("carousel", "Carousel", "A scroll-snap slideshow of items.", ApiFamilies.Carousel),
        new("chart", "Chart", "Pure-SVG bar, line, area, scatter, pie, radar, and radial charts.", ApiFamilies.Chart),
        new("checkbox", "Checkbox", "A toggle for a single option.", ApiFamilies.Checkbox),
        new("collapsible", "Collapsible", "Show and hide a section of content.", ApiFamilies.Collapsible),
        new("combobox", "Combobox", "An input with an autocomplete list.", ApiFamilies.Combobox),
        new("command", "Command", "A command palette for fast actions.", ApiFamilies.Command),
        new("context-menu", "Context Menu", "A menu opened on right-click.", ApiFamilies.ContextMenu),
        new("dialog", "Dialog", "A window overlaid on the page.", ApiFamilies.Dialog),
        new("drawer", "Drawer", "A panel that slides from an edge.", ApiFamilies.Drawer),
        new("dropdown-menu", "Dropdown Menu", "A menu of actions from a trigger.", ApiFamilies.DropdownMenu),
        new("empty", "Empty", "A placeholder for states with nothing to show.", ApiFamilies.Empty),
        new("field", "Field", "Label, control, hint and error together.", ApiFamilies.Field),
        new("hover-card", "Hover Card", "Preview content on hover.", ApiFamilies.HoverCard),
        new("icons", "Icons", "The full Tabler icon set.", ApiFamilies.Icon),
        new("image", "Image", "A content image with load states.", ApiFamilies.Image),
        new("input-date", "Input Date", "An inline date field.", ApiFamilies.InputDate),
        new("input-group", "Input Group", "Group inputs with addons and buttons.", ApiFamilies.InputGroup),
        new("input-number", "Input Number", "A numeric input with steppers.", ApiFamilies.InputNumber),
        new("input-otp", "Input OTP", "A one-time-passcode entry field.", ApiFamilies.InputOtp),
        new("input-tags", "Input Tags", "Free-text tags as removable chips.", ApiFamilies.InputTags),
        new("input-text", "Input Text", "A single-line text input.", ApiFamilies.InputText),
        new("input-time", "Input Time", "An inline time field.", ApiFamilies.InputTime),
        new("kbd", "Kbd", "Renders a keyboard key.", ApiFamilies.Kbd),
        new("label", "Label", "An accessible label for a control.", ApiFamilies.Label),
        new("menubar", "Menubar", "A desktop-style application menu bar.", ApiFamilies.Menubar),
        new("navigation-menu", "Navigation Menu", "A bar with rich dropdown panels.", ApiFamilies.NavigationMenu),
        new("pagination", "Pagination", "Navigate between pages of content.", ApiFamilies.Pagination),
        new("popover", "Popover", "Floating content anchored to a trigger.", ApiFamilies.Popover),
        new("progress", "Progress", "Shows the completion of a task.", ApiFamilies.Progress),
        new("radio-group", "Radio Group", "Pick one option from a set.", ApiFamilies.RadioGroup),
        new("resizable", "Resizable", "Drag-to-resize panel groups.", ApiFamilies.Resizable),
        new("scroll-area", "Scroll Area", "A styled, cross-browser scroll container.", ApiFamilies.ScrollArea),
        new("select", "Select", "Choose one value from a dropdown.", ApiFamilies.Select),
        new("separator", "Separator", "A visual divider between content.", ApiFamilies.Separator),
        new("sheet", "Sheet", "A panel that slides in from an edge.", ApiFamilies.Sheet),
        new("sidebar", "Sidebar", "A composable application sidebar.", ApiFamilies.Sidebar),
        new("skeleton", "Skeleton", "A placeholder while content loads.", ApiFamilies.Skeleton),
        new("slider", "Slider", "Pick a value from a range.", ApiFamilies.Slider),
        new("sortable", "Sortable", "Drag to reorder or swap items.", ApiFamilies.Sortable),
        new("switch", "Switch", "Toggle between on and off.", ApiFamilies.Switch),
        new("table", "Table", "A virtualized data table.", ApiFamilies.Table),
        new("toc", "Table of Contents", "An on-this-page navigation rail.", ApiFamilies.TableOfContents),
        new("tabs", "Tabs", "Layered sections shown one at a time.", ApiFamilies.Tabs),
        new("theme-switcher", "Theme Switcher", "Light / dark / system pickers.", ApiFamilies.ThemeSwitcher),
        new("toast", "Toast", "Imperative notification toasts.", null),
        new("toggle", "Toggle", "A two-state button.", ApiFamilies.Toggle),
        new("toggle-group", "Toggle Group", "A set of toggle buttons.", ApiFamilies.ToggleGroup),
        new("tooltip", "Tooltip", "A popup label on hover or focus.", ApiFamilies.Tooltip),
        new("tree", "Tree", "A hierarchical tree view with drag and drop.", ApiFamilies.Tree),
        new("virtualizer", "Virtualizer", "Render only the items in view.", ApiFamilies.Virtualizer),
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
