namespace Blazeo.Docs;

/// <summary>
/// The component families documented by <c>ApiReference</c> on each docs page: the parent first,
/// then its children, in usage order. Types are fully qualified so the field names (which mirror
/// the root component) don't shadow the component types. Add a family when a new component lands.
/// </summary>
public static class ApiFamilies
{
    public static readonly Type[] Accordion =
    [
        typeof(global::Blazeo.Ui.BzAccordion), typeof(global::Blazeo.Ui.BzAccordionItem),
        typeof(global::Blazeo.Ui.BzAccordionTrigger), typeof(global::Blazeo.Ui.BzAccordionContent),
    ];

    public static readonly Type[] Alert =
    [
        typeof(global::Blazeo.Ui.BzAlert), typeof(global::Blazeo.Ui.BzAlertTitle),
        typeof(global::Blazeo.Ui.BzAlertDescription), typeof(global::Blazeo.Ui.BzAlertAction),
    ];

    public static readonly Type[] AlertDialog =
    [
        typeof(global::Blazeo.Ui.BzAlertDialog), typeof(global::Blazeo.Ui.BzAlertDialogTrigger),
        typeof(global::Blazeo.Ui.BzAlertDialogContent), typeof(global::Blazeo.Ui.BzAlertDialogHeader),
        typeof(global::Blazeo.Ui.BzAlertDialogFooter), typeof(global::Blazeo.Ui.BzAlertDialogMedia),
        typeof(global::Blazeo.Ui.BzAlertDialogTitle), typeof(global::Blazeo.Ui.BzAlertDialogDescription),
        typeof(global::Blazeo.Ui.BzAlertDialogAction), typeof(global::Blazeo.Ui.BzAlertDialogCancel),
    ];

    public static readonly Type[] AspectRatio = [typeof(global::Blazeo.Ui.BzAspectRatio)];

    public static readonly Type[] Avatar =
    [
        typeof(global::Blazeo.Ui.BzAvatar), typeof(global::Blazeo.Ui.BzAvatarImage),
        typeof(global::Blazeo.Ui.BzAvatarFallback), typeof(global::Blazeo.Ui.BzAvatarBadge),
        typeof(global::Blazeo.Ui.BzAvatarGroup), typeof(global::Blazeo.Ui.BzAvatarGroupCount),
    ];

    public static readonly Type[] Badge = [typeof(global::Blazeo.Ui.BzBadge)];

    public static readonly Type[] Breadcrumb =
    [
        typeof(global::Blazeo.Ui.BzBreadcrumb), typeof(global::Blazeo.Ui.BzBreadcrumbList),
        typeof(global::Blazeo.Ui.BzBreadcrumbItem), typeof(global::Blazeo.Ui.BzBreadcrumbLink),
        typeof(global::Blazeo.Ui.BzBreadcrumbPage), typeof(global::Blazeo.Ui.BzBreadcrumbSeparator),
        typeof(global::Blazeo.Ui.BzBreadcrumbEllipsis),
    ];

    public static readonly Type[] Button = [typeof(global::Blazeo.Ui.BzButton)];

    public static readonly Type[] Calendar = [typeof(global::Blazeo.Ui.BzCalendar)];

    public static readonly Type[] Card =
    [
        typeof(global::Blazeo.Ui.BzCard), typeof(global::Blazeo.Ui.BzCardHeader),
        typeof(global::Blazeo.Ui.BzCardTitle), typeof(global::Blazeo.Ui.BzCardDescription),
        typeof(global::Blazeo.Ui.BzCardAction), typeof(global::Blazeo.Ui.BzCardContent),
        typeof(global::Blazeo.Ui.BzCardFooter),
    ];

    public static readonly Type[] Checkbox = [typeof(global::Blazeo.Ui.BzCheckbox)];

    public static readonly Type[] Collapsible =
    [
        typeof(global::Blazeo.Ui.BzCollapsible), typeof(global::Blazeo.Ui.BzCollapsibleTrigger),
        typeof(global::Blazeo.Ui.BzCollapsibleContent),
    ];

    public static readonly Type[] ScrollArea = [typeof(global::Blazeo.Ui.BzScrollArea)];

    public static readonly Type[] Combobox =
    [
        typeof(global::Blazeo.Ui.BzCombobox<>), typeof(global::Blazeo.Ui.BzComboboxInput),
        typeof(global::Blazeo.Ui.BzComboboxContent), typeof(global::Blazeo.Ui.BzComboboxList<>),
        typeof(global::Blazeo.Ui.BzComboboxItem<>), typeof(global::Blazeo.Ui.BzComboboxEmpty),
        typeof(global::Blazeo.Ui.BzComboboxGroup), typeof(global::Blazeo.Ui.BzComboboxLabel),
        typeof(global::Blazeo.Ui.BzComboboxCollection<>), typeof(global::Blazeo.Ui.BzComboboxSeparator),
        typeof(global::Blazeo.Ui.BzComboboxValue<>), typeof(global::Blazeo.Ui.BzComboboxTrigger),
        typeof(global::Blazeo.Ui.BzComboboxClear), typeof(global::Blazeo.Ui.BzComboboxChips),
        typeof(global::Blazeo.Ui.BzComboboxChip<>), typeof(global::Blazeo.Ui.BzComboboxChipRemove),
        typeof(global::Blazeo.Ui.BzComboboxChipsInput),
    ];

    public static readonly Type[] Command =
    [
        typeof(global::Blazeo.Ui.BzCommand), typeof(global::Blazeo.Ui.BzCommandInput),
        typeof(global::Blazeo.Ui.BzCommandList), typeof(global::Blazeo.Ui.BzCommandEmpty),
        typeof(global::Blazeo.Ui.BzCommandGroup), typeof(global::Blazeo.Ui.BzCommandItem),
        typeof(global::Blazeo.Ui.BzCommandSeparator), typeof(global::Blazeo.Ui.BzCommandShortcut),
        typeof(global::Blazeo.Ui.BzCommandDialog),
    ];

    public static readonly Type[] ContextMenu =
    [
        typeof(global::Blazeo.Ui.BzContextMenu), typeof(global::Blazeo.Ui.BzContextMenuTrigger),
        typeof(global::Blazeo.Ui.BzContextMenuContent), typeof(global::Blazeo.Ui.BzContextMenuGroup),
        typeof(global::Blazeo.Ui.BzContextMenuItem), typeof(global::Blazeo.Ui.BzContextMenuCheckboxItem),
        typeof(global::Blazeo.Ui.BzContextMenuRadioGroup), typeof(global::Blazeo.Ui.BzContextMenuRadioItem),
        typeof(global::Blazeo.Ui.BzContextMenuLabel), typeof(global::Blazeo.Ui.BzContextMenuSeparator),
        typeof(global::Blazeo.Ui.BzContextMenuShortcut), typeof(global::Blazeo.Ui.BzContextMenuSub),
        typeof(global::Blazeo.Ui.BzContextMenuSubTrigger), typeof(global::Blazeo.Ui.BzContextMenuSubContent),
    ];

    public static readonly Type[] Dialog =
    [
        typeof(global::Blazeo.Ui.BzDialog), typeof(global::Blazeo.Ui.BzDialogTrigger),
        typeof(global::Blazeo.Ui.BzDialogContent), typeof(global::Blazeo.Ui.BzDialogHeader),
        typeof(global::Blazeo.Ui.BzDialogFooter), typeof(global::Blazeo.Ui.BzDialogTitle),
        typeof(global::Blazeo.Ui.BzDialogDescription), typeof(global::Blazeo.Ui.BzDialogClose),
        typeof(global::Blazeo.Ui.BzDialogOverlay),
    ];

    public static readonly Type[] DirectionProvider = [typeof(global::Blazeo.Ui.BzDirectionProvider)];

    public static readonly Type[] Drawer =
    [
        typeof(global::Blazeo.Ui.BzDrawer), typeof(global::Blazeo.Ui.BzDrawerProvider),
        typeof(global::Blazeo.Ui.BzDrawerTrigger),
        typeof(global::Blazeo.Ui.BzDrawerContent), typeof(global::Blazeo.Ui.BzDrawerHeader),
        typeof(global::Blazeo.Ui.BzDrawerFooter), typeof(global::Blazeo.Ui.BzDrawerTitle),
        typeof(global::Blazeo.Ui.BzDrawerDescription), typeof(global::Blazeo.Ui.BzDrawerClose),
    ];

    public static readonly Type[] DropdownMenu =
    [
        typeof(global::Blazeo.Ui.BzDropdownMenu), typeof(global::Blazeo.Ui.BzDropdownMenuTrigger),
        typeof(global::Blazeo.Ui.BzDropdownMenuContent), typeof(global::Blazeo.Ui.BzDropdownMenuGroup),
        typeof(global::Blazeo.Ui.BzDropdownMenuItem), typeof(global::Blazeo.Ui.BzDropdownMenuCheckboxItem),
        typeof(global::Blazeo.Ui.BzDropdownMenuRadioGroup), typeof(global::Blazeo.Ui.BzDropdownMenuRadioItem),
        typeof(global::Blazeo.Ui.BzDropdownMenuLabel), typeof(global::Blazeo.Ui.BzDropdownMenuSeparator),
        typeof(global::Blazeo.Ui.BzDropdownMenuShortcut), typeof(global::Blazeo.Ui.BzDropdownMenuSub),
        typeof(global::Blazeo.Ui.BzDropdownMenuSubTrigger), typeof(global::Blazeo.Ui.BzDropdownMenuSubContent),
    ];

    public static readonly Type[] Field =
    [
        typeof(global::Blazeo.Ui.BzField), typeof(global::Blazeo.Ui.BzFieldSet),
        typeof(global::Blazeo.Ui.BzFieldLegend), typeof(global::Blazeo.Ui.BzFieldGroup),
        typeof(global::Blazeo.Ui.BzFieldContent), typeof(global::Blazeo.Ui.BzFieldLabel),
        typeof(global::Blazeo.Ui.BzFieldTitle), typeof(global::Blazeo.Ui.BzFieldControl),
        typeof(global::Blazeo.Ui.BzFieldDescription), typeof(global::Blazeo.Ui.BzFieldError),
        typeof(global::Blazeo.Ui.BzFieldSeparator),
    ];

    public static readonly Type[] HoverCard =
    [
        typeof(global::Blazeo.Ui.BzHoverCard), typeof(global::Blazeo.Ui.BzHoverCardTrigger),
        typeof(global::Blazeo.Ui.BzHoverCardContent), typeof(global::Blazeo.Ui.BzHoverCardArrow),
    ];

    public static readonly Type[] InputGroup =
    [
        typeof(global::Blazeo.Ui.BzInputGroup), typeof(global::Blazeo.Ui.BzInputGroupAddon),
        typeof(global::Blazeo.Ui.BzInputGroupButton), typeof(global::Blazeo.Ui.BzInputGroupText),
        typeof(global::Blazeo.Ui.BzInputGroupInput), typeof(global::Blazeo.Ui.BzInputGroupTextarea),
    ];

    public static readonly Type[] Kbd = [typeof(global::Blazeo.Ui.BzKbd), typeof(global::Blazeo.Ui.BzKbdGroup)];

    public static readonly Type[] Label = [typeof(global::Blazeo.Ui.BzLabel)];

    public static readonly Type[] Menubar =
    [
        typeof(global::Blazeo.Ui.BzMenubar), typeof(global::Blazeo.Ui.BzMenubarMenu),
        typeof(global::Blazeo.Ui.BzMenubarTrigger), typeof(global::Blazeo.Ui.BzMenubarContent),
        typeof(global::Blazeo.Ui.BzMenubarGroup), typeof(global::Blazeo.Ui.BzMenubarItem),
        typeof(global::Blazeo.Ui.BzMenubarCheckboxItem), typeof(global::Blazeo.Ui.BzMenubarRadioGroup),
        typeof(global::Blazeo.Ui.BzMenubarRadioItem), typeof(global::Blazeo.Ui.BzMenubarLabel),
        typeof(global::Blazeo.Ui.BzMenubarSeparator), typeof(global::Blazeo.Ui.BzMenubarShortcut),
        typeof(global::Blazeo.Ui.BzMenubarSub), typeof(global::Blazeo.Ui.BzMenubarSubTrigger),
        typeof(global::Blazeo.Ui.BzMenubarSubContent),
    ];

    public static readonly Type[] InputNumber = [typeof(global::Blazeo.Ui.BzInputNumber)];

    public static readonly Type[] Pagination =
    [
        typeof(global::Blazeo.Ui.BzPagination), typeof(global::Blazeo.Ui.BzPaginationContent),
        typeof(global::Blazeo.Ui.BzPaginationItem), typeof(global::Blazeo.Ui.BzPaginationLink),
        typeof(global::Blazeo.Ui.BzPaginationFirst), typeof(global::Blazeo.Ui.BzPaginationPrevious),
        typeof(global::Blazeo.Ui.BzPaginationNext), typeof(global::Blazeo.Ui.BzPaginationLast),
        typeof(global::Blazeo.Ui.BzPaginationEllipsis),
    ];

    public static readonly Type[] Popover =
    [
        typeof(global::Blazeo.Ui.BzPopover), typeof(global::Blazeo.Ui.BzPopoverTrigger),
        typeof(global::Blazeo.Ui.BzPopoverContent), typeof(global::Blazeo.Ui.BzPopoverArrow),
        typeof(global::Blazeo.Ui.BzPopoverClose), typeof(global::Blazeo.Ui.BzPopoverHeader),
        typeof(global::Blazeo.Ui.BzPopoverTitle), typeof(global::Blazeo.Ui.BzPopoverDescription),
    ];

    public static readonly Type[] Progress = [typeof(global::Blazeo.Ui.BzProgress)];

    public static readonly Type[] RadioGroup =
        [typeof(global::Blazeo.Ui.BzRadioGroup), typeof(global::Blazeo.Ui.BzRadioGroupItem)];

    public static readonly Type[] Select =
    [
        typeof(global::Blazeo.Ui.BzSelect), typeof(global::Blazeo.Ui.BzSelectTrigger),
        typeof(global::Blazeo.Ui.BzSelectValue), typeof(global::Blazeo.Ui.BzSelectContent),
        typeof(global::Blazeo.Ui.BzSelectGroup), typeof(global::Blazeo.Ui.BzSelectLabel),
        typeof(global::Blazeo.Ui.BzSelectItem), typeof(global::Blazeo.Ui.BzSelectSeparator),
    ];

    public static readonly Type[] Separator = [typeof(global::Blazeo.Ui.BzSeparator)];

    public static readonly Type[] Sheet =
    [
        typeof(global::Blazeo.Ui.BzSheet), typeof(global::Blazeo.Ui.BzSheetTrigger),
        typeof(global::Blazeo.Ui.BzSheetContent), typeof(global::Blazeo.Ui.BzSheetHeader),
        typeof(global::Blazeo.Ui.BzSheetFooter), typeof(global::Blazeo.Ui.BzSheetTitle),
        typeof(global::Blazeo.Ui.BzSheetDescription), typeof(global::Blazeo.Ui.BzSheetClose),
    ];

    public static readonly Type[] Sidebar =
    [
        typeof(global::Blazeo.Ui.BzSidebarProvider), typeof(global::Blazeo.Ui.BzSidebar),
        typeof(global::Blazeo.Ui.BzSidebarTrigger), typeof(global::Blazeo.Ui.BzSidebarRail),
        typeof(global::Blazeo.Ui.BzSidebarInset), typeof(global::Blazeo.Ui.BzSidebarInput),
        typeof(global::Blazeo.Ui.BzSidebarHeader), typeof(global::Blazeo.Ui.BzSidebarFooter),
        typeof(global::Blazeo.Ui.BzSidebarContent), typeof(global::Blazeo.Ui.BzSidebarSeparator),
        typeof(global::Blazeo.Ui.BzSidebarGroup), typeof(global::Blazeo.Ui.BzSidebarGroupLabel),
        typeof(global::Blazeo.Ui.BzSidebarGroupAction), typeof(global::Blazeo.Ui.BzSidebarGroupContent),
        typeof(global::Blazeo.Ui.BzSidebarMenu), typeof(global::Blazeo.Ui.BzSidebarMenuItem),
        typeof(global::Blazeo.Ui.BzSidebarMenuButton), typeof(global::Blazeo.Ui.BzSidebarMenuAction),
        typeof(global::Blazeo.Ui.BzSidebarMenuBadge), typeof(global::Blazeo.Ui.BzSidebarMenuSkeleton),
        typeof(global::Blazeo.Ui.BzSidebarMenuSub), typeof(global::Blazeo.Ui.BzSidebarMenuSubItem),
        typeof(global::Blazeo.Ui.BzSidebarMenuSubButton),
    ];

    public static readonly Type[] Skeleton = [typeof(global::Blazeo.Ui.BzSkeleton)];

    public static readonly Type[] Slider = [typeof(global::Blazeo.Ui.BzSlider)];

    public static readonly Type[] Switch = [typeof(global::Blazeo.Ui.BzSwitch)];

    public static readonly Type[] Table =
    [
        typeof(global::Blazeo.Ui.BzTable), typeof(global::Blazeo.Ui.BzTableHeader),
        typeof(global::Blazeo.Ui.BzTableBody), typeof(global::Blazeo.Ui.BzTableFooter),
        typeof(global::Blazeo.Ui.BzTableRow), typeof(global::Blazeo.Ui.BzTableHead),
        typeof(global::Blazeo.Ui.BzTableCell), typeof(global::Blazeo.Ui.BzTableCaption),
        typeof(global::Blazeo.Ui.BzDataTable<>), typeof(global::Blazeo.Ui.BzColumnBase<>),
        typeof(global::Blazeo.Ui.BzPropertyColumn<,>), typeof(global::Blazeo.Ui.BzTemplateColumn<>),
    ];

    public static readonly Type[] TableOfContents = [typeof(global::Blazeo.Ui.BzTableOfContents)];

    public static readonly Type[] Tabs =
    [
        typeof(global::Blazeo.Ui.BzTabs), typeof(global::Blazeo.Ui.BzTabsList),
        typeof(global::Blazeo.Ui.BzTabsTrigger), typeof(global::Blazeo.Ui.BzTabsContent),
    ];

    public static readonly Type[] InputText = [typeof(global::Blazeo.Ui.BzInputText)];

    public static readonly Type[] TimePicker = [typeof(global::Blazeo.Ui.BzTimePicker)];

    public static readonly Type[] Toggle = [typeof(global::Blazeo.Ui.BzToggle)];

    public static readonly Type[] ToggleGroup =
        [typeof(global::Blazeo.Ui.BzToggleGroup), typeof(global::Blazeo.Ui.BzToggleGroupItem)];

    public static readonly Type[] Tooltip =
    [
        typeof(global::Blazeo.Ui.BzTooltipProvider), typeof(global::Blazeo.Ui.BzTooltip),
        typeof(global::Blazeo.Ui.BzTooltipTrigger), typeof(global::Blazeo.Ui.BzTooltipContent),
        typeof(global::Blazeo.Ui.BzTooltipArrow),
    ];

    public static readonly Type[] Virtualizer = [typeof(global::Blazeo.Ui.BzVirtualizer<>)];
}
