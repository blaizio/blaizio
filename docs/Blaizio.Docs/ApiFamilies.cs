namespace Blaizio.Docs;

/// <summary>
/// The component families documented by <c>ApiReference</c> on each docs page: the parent first,
/// then its children, in usage order. Types are fully qualified so the field names (which mirror
/// the root component) don't shadow the component types. Add a family when a new component lands.
/// </summary>
public static class ApiFamilies
{
    public static readonly Type[] Accordion =
    [
        typeof(global::Blaizio.Ui.BzAccordion), typeof(global::Blaizio.Ui.BzAccordionItem),
        typeof(global::Blaizio.Ui.BzAccordionTrigger), typeof(global::Blaizio.Ui.BzAccordionContent),
    ];

    public static readonly Type[] Alert =
    [
        typeof(global::Blaizio.Ui.BzAlert), typeof(global::Blaizio.Ui.BzAlertTitle),
        typeof(global::Blaizio.Ui.BzAlertDescription), typeof(global::Blaizio.Ui.BzAlertAction),
    ];

    public static readonly Type[] AlertDialog =
    [
        typeof(global::Blaizio.Ui.BzAlertDialog), typeof(global::Blaizio.Ui.BzAlertDialogTrigger),
        typeof(global::Blaizio.Ui.BzAlertDialogContent), typeof(global::Blaizio.Ui.BzAlertDialogHeader),
        typeof(global::Blaizio.Ui.BzAlertDialogFooter), typeof(global::Blaizio.Ui.BzAlertDialogMedia),
        typeof(global::Blaizio.Ui.BzAlertDialogTitle), typeof(global::Blaizio.Ui.BzAlertDialogDescription),
        typeof(global::Blaizio.Ui.BzAlertDialogAction), typeof(global::Blaizio.Ui.BzAlertDialogCancel),
    ];

    public static readonly Type[] AspectRatio = [typeof(global::Blaizio.Ui.BzAspectRatio)];

    public static readonly Type[] Attachment =
    [
        typeof(global::Blaizio.Ui.BzAttachment), typeof(global::Blaizio.Ui.BzAttachmentMedia),
        typeof(global::Blaizio.Ui.BzAttachmentContent), typeof(global::Blaizio.Ui.BzAttachmentTitle),
        typeof(global::Blaizio.Ui.BzAttachmentDescription), typeof(global::Blaizio.Ui.BzAttachmentActions),
        typeof(global::Blaizio.Ui.BzAttachmentAction), typeof(global::Blaizio.Ui.BzAttachmentTrigger),
        typeof(global::Blaizio.Ui.BzAttachmentGroup), typeof(global::Blaizio.Ui.BzAttachmentDropzone),
    ];

    public static readonly Type[] Avatar =
    [
        typeof(global::Blaizio.Ui.BzAvatar), typeof(global::Blaizio.Ui.BzAvatarImage),
        typeof(global::Blaizio.Ui.BzAvatarFallback), typeof(global::Blaizio.Ui.BzAvatarBadge),
        typeof(global::Blaizio.Ui.BzAvatarGroup), typeof(global::Blaizio.Ui.BzAvatarGroupCount),
    ];

    public static readonly Type[] Badge = [typeof(global::Blaizio.Ui.BzBadge)];

    public static readonly Type[] Breadcrumb =
    [
        typeof(global::Blaizio.Ui.BzBreadcrumb), typeof(global::Blaizio.Ui.BzBreadcrumbList),
        typeof(global::Blaizio.Ui.BzBreadcrumbItem), typeof(global::Blaizio.Ui.BzBreadcrumbLink),
        typeof(global::Blaizio.Ui.BzBreadcrumbPage), typeof(global::Blaizio.Ui.BzBreadcrumbSeparator),
        typeof(global::Blaizio.Ui.BzBreadcrumbEllipsis),
    ];

    public static readonly Type[] Button = [typeof(global::Blaizio.Ui.BzButton)];

    public static readonly Type[] Calendar = [typeof(global::Blaizio.Ui.BzCalendar)];

    public static readonly Type[] Card =
    [
        typeof(global::Blaizio.Ui.BzCard), typeof(global::Blaizio.Ui.BzCardHeader),
        typeof(global::Blaizio.Ui.BzCardTitle), typeof(global::Blaizio.Ui.BzCardDescription),
        typeof(global::Blaizio.Ui.BzCardAction), typeof(global::Blaizio.Ui.BzCardContent),
        typeof(global::Blaizio.Ui.BzCardFooter),
    ];

    public static readonly Type[] Carousel =
    [
        typeof(global::Blaizio.Ui.BzCarousel), typeof(global::Blaizio.Ui.BzCarouselContent),
        typeof(global::Blaizio.Ui.BzCarouselItem), typeof(global::Blaizio.Ui.BzCarouselPrevious),
        typeof(global::Blaizio.Ui.BzCarouselNext), typeof(global::Blaizio.Ui.BzCarouselDots),
    ];

    public static readonly Type[] Chart =
    [
        typeof(global::Blaizio.Ui.BzChart<>), typeof(global::Blaizio.Ui.BzChartBarSeries<>),
        typeof(global::Blaizio.Ui.BzChartLineSeries<>), typeof(global::Blaizio.Ui.BzChartAreaSeries<>),
        typeof(global::Blaizio.Ui.BzChartScatterSeries<>),
        typeof(global::Blaizio.Ui.BzChartXAxis), typeof(global::Blaizio.Ui.BzChartYAxis),
        typeof(global::Blaizio.Ui.BzChartGrid), typeof(global::Blaizio.Ui.BzChartTooltip),
        typeof(global::Blaizio.Ui.BzChartLegend), typeof(global::Blaizio.Ui.BzPieChart<>),
        typeof(global::Blaizio.Ui.BzRadarChart<>), typeof(global::Blaizio.Ui.BzRadarSeries<>),
        typeof(global::Blaizio.Ui.BzRadialBarChart<>),
    ];

    public static readonly Type[] Checkbox = [typeof(global::Blaizio.Ui.BzCheckbox)];

    public static readonly Type[] Icon = [typeof(global::Blaizio.BzIcon)];

    public static readonly Type[] Collapsible =
    [
        typeof(global::Blaizio.Ui.BzCollapsible), typeof(global::Blaizio.Ui.BzCollapsibleTrigger),
        typeof(global::Blaizio.Ui.BzCollapsibleContent),
    ];

    public static readonly Type[] ScrollArea = [typeof(global::Blaizio.Ui.BzScrollArea)];

    public static readonly Type[] Combobox =
    [
        typeof(global::Blaizio.Ui.BzCombobox<>), typeof(global::Blaizio.Ui.BzComboboxInput),
        typeof(global::Blaizio.Ui.BzComboboxContent), typeof(global::Blaizio.Ui.BzComboboxList<>),
        typeof(global::Blaizio.Ui.BzComboboxItem<>), typeof(global::Blaizio.Ui.BzComboboxEmpty),
        typeof(global::Blaizio.Ui.BzComboboxGroup), typeof(global::Blaizio.Ui.BzComboboxLabel),
        typeof(global::Blaizio.Ui.BzComboboxCollection<>), typeof(global::Blaizio.Ui.BzComboboxSeparator),
        typeof(global::Blaizio.Ui.BzComboboxValue<>), typeof(global::Blaizio.Ui.BzComboboxTrigger),
        typeof(global::Blaizio.Ui.BzComboboxClear), typeof(global::Blaizio.Ui.BzComboboxChips),
        typeof(global::Blaizio.Ui.BzComboboxChip<>), typeof(global::Blaizio.Ui.BzComboboxChipRemove),
        typeof(global::Blaizio.Ui.BzComboboxChipsInput),
    ];

    public static readonly Type[] Command =
    [
        typeof(global::Blaizio.Ui.BzCommand), typeof(global::Blaizio.Ui.BzCommandInput),
        typeof(global::Blaizio.Ui.BzCommandList), typeof(global::Blaizio.Ui.BzCommandEmpty),
        typeof(global::Blaizio.Ui.BzCommandGroup), typeof(global::Blaizio.Ui.BzCommandItem),
        typeof(global::Blaizio.Ui.BzCommandSeparator), typeof(global::Blaizio.Ui.BzCommandShortcut),
        typeof(global::Blaizio.Ui.BzCommandDialog),
    ];

    public static readonly Type[] ContextMenu =
    [
        typeof(global::Blaizio.Ui.BzContextMenu), typeof(global::Blaizio.Ui.BzContextMenuTrigger),
        typeof(global::Blaizio.Ui.BzContextMenuContent), typeof(global::Blaizio.Ui.BzContextMenuGroup),
        typeof(global::Blaizio.Ui.BzContextMenuItem), typeof(global::Blaizio.Ui.BzContextMenuCheckboxItem),
        typeof(global::Blaizio.Ui.BzContextMenuRadioGroup), typeof(global::Blaizio.Ui.BzContextMenuRadioItem),
        typeof(global::Blaizio.Ui.BzContextMenuLabel), typeof(global::Blaizio.Ui.BzContextMenuSeparator),
        typeof(global::Blaizio.Ui.BzContextMenuShortcut), typeof(global::Blaizio.Ui.BzContextMenuSub),
        typeof(global::Blaizio.Ui.BzContextMenuSubTrigger), typeof(global::Blaizio.Ui.BzContextMenuSubContent),
    ];

    public static readonly Type[] Dialog =
    [
        typeof(global::Blaizio.Ui.BzDialog), typeof(global::Blaizio.Ui.BzDialogTrigger),
        typeof(global::Blaizio.Ui.BzDialogContent), typeof(global::Blaizio.Ui.BzDialogHeader),
        typeof(global::Blaizio.Ui.BzDialogFooter), typeof(global::Blaizio.Ui.BzDialogTitle),
        typeof(global::Blaizio.Ui.BzDialogDescription), typeof(global::Blaizio.Ui.BzDialogClose),
        typeof(global::Blaizio.Ui.BzDialogOverlay),
    ];

    public static readonly Type[] DirectionProvider = [typeof(global::Blaizio.Ui.BzDirectionProvider)];

    public static readonly Type[] Drawer =
    [
        typeof(global::Blaizio.Ui.BzDrawer), typeof(global::Blaizio.Ui.BzDrawerProvider),
        typeof(global::Blaizio.Ui.BzDrawerTrigger),
        typeof(global::Blaizio.Ui.BzDrawerContent), typeof(global::Blaizio.Ui.BzDrawerHeader),
        typeof(global::Blaizio.Ui.BzDrawerFooter), typeof(global::Blaizio.Ui.BzDrawerTitle),
        typeof(global::Blaizio.Ui.BzDrawerDescription), typeof(global::Blaizio.Ui.BzDrawerClose),
    ];

    public static readonly Type[] DropdownMenu =
    [
        typeof(global::Blaizio.Ui.BzDropdownMenu), typeof(global::Blaizio.Ui.BzDropdownMenuTrigger),
        typeof(global::Blaizio.Ui.BzDropdownMenuContent), typeof(global::Blaizio.Ui.BzDropdownMenuGroup),
        typeof(global::Blaizio.Ui.BzDropdownMenuItem), typeof(global::Blaizio.Ui.BzDropdownMenuCheckboxItem),
        typeof(global::Blaizio.Ui.BzDropdownMenuRadioGroup), typeof(global::Blaizio.Ui.BzDropdownMenuRadioItem),
        typeof(global::Blaizio.Ui.BzDropdownMenuLabel), typeof(global::Blaizio.Ui.BzDropdownMenuSeparator),
        typeof(global::Blaizio.Ui.BzDropdownMenuShortcut), typeof(global::Blaizio.Ui.BzDropdownMenuSub),
        typeof(global::Blaizio.Ui.BzDropdownMenuSubTrigger), typeof(global::Blaizio.Ui.BzDropdownMenuSubContent),
    ];

    public static readonly Type[] Empty =
    [
        typeof(global::Blaizio.Ui.BzEmpty), typeof(global::Blaizio.Ui.BzEmptyHeader),
        typeof(global::Blaizio.Ui.BzEmptyMedia), typeof(global::Blaizio.Ui.BzEmptyTitle),
        typeof(global::Blaizio.Ui.BzEmptyDescription), typeof(global::Blaizio.Ui.BzEmptyContent),
    ];

    public static readonly Type[] Field =
    [
        typeof(global::Blaizio.Ui.BzField), typeof(global::Blaizio.Ui.BzFieldSet),
        typeof(global::Blaizio.Ui.BzFieldLegend), typeof(global::Blaizio.Ui.BzFieldGroup),
        typeof(global::Blaizio.Ui.BzFieldContent), typeof(global::Blaizio.Ui.BzFieldLabel),
        typeof(global::Blaizio.Ui.BzFieldTitle), typeof(global::Blaizio.Ui.BzFieldControl),
        typeof(global::Blaizio.Ui.BzFieldDescription), typeof(global::Blaizio.Ui.BzFieldError),
        typeof(global::Blaizio.Ui.BzFieldSeparator),
    ];

    public static readonly Type[] HoverCard =
    [
        typeof(global::Blaizio.Ui.BzHoverCard), typeof(global::Blaizio.Ui.BzHoverCardTrigger),
        typeof(global::Blaizio.Ui.BzHoverCardContent), typeof(global::Blaizio.Ui.BzHoverCardArrow),
    ];

    public static readonly Type[] InputGroup =
    [
        typeof(global::Blaizio.Ui.BzInputGroup), typeof(global::Blaizio.Ui.BzInputGroupAddon),
        typeof(global::Blaizio.Ui.BzInputGroupButton), typeof(global::Blaizio.Ui.BzInputGroupText),
        typeof(global::Blaizio.Ui.BzInputGroupInput), typeof(global::Blaizio.Ui.BzInputGroupTextarea),
    ];

    public static readonly Type[] InputOtp =
    [
        typeof(global::Blaizio.Ui.BzInputOtp), typeof(global::Blaizio.Ui.BzInputOtpGroup),
        typeof(global::Blaizio.Ui.BzInputOtpSlot), typeof(global::Blaizio.Ui.BzInputOtpSeparator),
    ];

    public static readonly Type[] InputTags =
    [
        typeof(global::Blaizio.Ui.BzInputTags), typeof(global::Blaizio.Ui.BzInputTagsChips),
        typeof(global::Blaizio.Ui.BzInputTagsChipRemove),
    ];

    public static readonly Type[] Kbd = [typeof(global::Blaizio.Ui.BzKbd), typeof(global::Blaizio.Ui.BzKbdGroup)];

    public static readonly Type[] Label = [typeof(global::Blaizio.Ui.BzLabel)];

    public static readonly Type[] Menubar =
    [
        typeof(global::Blaizio.Ui.BzMenubar), typeof(global::Blaizio.Ui.BzMenubarMenu),
        typeof(global::Blaizio.Ui.BzMenubarTrigger), typeof(global::Blaizio.Ui.BzMenubarContent),
        typeof(global::Blaizio.Ui.BzMenubarGroup), typeof(global::Blaizio.Ui.BzMenubarItem),
        typeof(global::Blaizio.Ui.BzMenubarCheckboxItem), typeof(global::Blaizio.Ui.BzMenubarRadioGroup),
        typeof(global::Blaizio.Ui.BzMenubarRadioItem), typeof(global::Blaizio.Ui.BzMenubarLabel),
        typeof(global::Blaizio.Ui.BzMenubarSeparator), typeof(global::Blaizio.Ui.BzMenubarShortcut),
        typeof(global::Blaizio.Ui.BzMenubarSub), typeof(global::Blaizio.Ui.BzMenubarSubTrigger),
        typeof(global::Blaizio.Ui.BzMenubarSubContent),
    ];

    public static readonly Type[] InputNumber = [typeof(global::Blaizio.Ui.BzInputNumber)];

    public static readonly Type[] NavigationMenu =
    [
        typeof(global::Blaizio.Ui.BzNavigationMenu), typeof(global::Blaizio.Ui.BzNavigationMenuList),
        typeof(global::Blaizio.Ui.BzNavigationMenuItem), typeof(global::Blaizio.Ui.BzNavigationMenuTrigger),
        typeof(global::Blaizio.Ui.BzNavigationMenuContent), typeof(global::Blaizio.Ui.BzNavigationMenuLink),
        typeof(global::Blaizio.Ui.BzNavigationMenuArrow),
    ];

    public static readonly Type[] Pagination =
    [
        typeof(global::Blaizio.Ui.BzPagination), typeof(global::Blaizio.Ui.BzPaginationContent),
        typeof(global::Blaizio.Ui.BzPaginationItem), typeof(global::Blaizio.Ui.BzPaginationLink),
        typeof(global::Blaizio.Ui.BzPaginationFirst), typeof(global::Blaizio.Ui.BzPaginationPrevious),
        typeof(global::Blaizio.Ui.BzPaginationNext), typeof(global::Blaizio.Ui.BzPaginationLast),
        typeof(global::Blaizio.Ui.BzPaginationEllipsis),
    ];

    public static readonly Type[] Popover =
    [
        typeof(global::Blaizio.Ui.BzPopover), typeof(global::Blaizio.Ui.BzPopoverTrigger),
        typeof(global::Blaizio.Ui.BzPopoverContent), typeof(global::Blaizio.Ui.BzPopoverArrow),
        typeof(global::Blaizio.Ui.BzPopoverClose), typeof(global::Blaizio.Ui.BzPopoverHeader),
        typeof(global::Blaizio.Ui.BzPopoverTitle), typeof(global::Blaizio.Ui.BzPopoverDescription),
    ];

    public static readonly Type[] Progress =
        [typeof(global::Blaizio.Ui.BzProgress), typeof(global::Blaizio.Ui.BzCircularProgress)];

    public static readonly Type[] RadioGroup =
        [typeof(global::Blaizio.Ui.BzRadioGroup), typeof(global::Blaizio.Ui.BzRadioGroupItem)];

    public static readonly Type[] Resizable =
    [
        typeof(global::Blaizio.Ui.BzResizablePanelGroup), typeof(global::Blaizio.Ui.BzResizablePanel),
        typeof(global::Blaizio.Ui.BzResizableHandle),
    ];

    public static readonly Type[] Select =
    [
        typeof(global::Blaizio.Ui.BzSelect), typeof(global::Blaizio.Ui.BzSelectTrigger),
        typeof(global::Blaizio.Ui.BzSelectValue), typeof(global::Blaizio.Ui.BzSelectContent),
        typeof(global::Blaizio.Ui.BzSelectGroup), typeof(global::Blaizio.Ui.BzSelectLabel),
        typeof(global::Blaizio.Ui.BzSelectItem), typeof(global::Blaizio.Ui.BzSelectSeparator),
    ];

    public static readonly Type[] Separator = [typeof(global::Blaizio.Ui.BzSeparator)];

    public static readonly Type[] Sheet =
    [
        typeof(global::Blaizio.Ui.BzSheet), typeof(global::Blaizio.Ui.BzSheetTrigger),
        typeof(global::Blaizio.Ui.BzSheetContent), typeof(global::Blaizio.Ui.BzSheetHeader),
        typeof(global::Blaizio.Ui.BzSheetFooter), typeof(global::Blaizio.Ui.BzSheetTitle),
        typeof(global::Blaizio.Ui.BzSheetDescription), typeof(global::Blaizio.Ui.BzSheetClose),
    ];

    public static readonly Type[] Sidebar =
    [
        typeof(global::Blaizio.Ui.BzSidebarProvider), typeof(global::Blaizio.Ui.BzSidebar),
        typeof(global::Blaizio.Ui.BzSidebarTrigger), typeof(global::Blaizio.Ui.BzSidebarRail),
        typeof(global::Blaizio.Ui.BzSidebarInset), typeof(global::Blaizio.Ui.BzSidebarInput),
        typeof(global::Blaizio.Ui.BzSidebarHeader), typeof(global::Blaizio.Ui.BzSidebarFooter),
        typeof(global::Blaizio.Ui.BzSidebarContent), typeof(global::Blaizio.Ui.BzSidebarSeparator),
        typeof(global::Blaizio.Ui.BzSidebarGroup), typeof(global::Blaizio.Ui.BzSidebarGroupLabel),
        typeof(global::Blaizio.Ui.BzSidebarGroupAction), typeof(global::Blaizio.Ui.BzSidebarGroupContent),
        typeof(global::Blaizio.Ui.BzSidebarMenu), typeof(global::Blaizio.Ui.BzSidebarMenuItem),
        typeof(global::Blaizio.Ui.BzSidebarMenuButton), typeof(global::Blaizio.Ui.BzSidebarMenuAction),
        typeof(global::Blaizio.Ui.BzSidebarMenuBadge), typeof(global::Blaizio.Ui.BzSidebarMenuSkeleton),
        typeof(global::Blaizio.Ui.BzSidebarMenuSub), typeof(global::Blaizio.Ui.BzSidebarMenuSubItem),
        typeof(global::Blaizio.Ui.BzSidebarMenuSubButton),
    ];

    public static readonly Type[] Skeleton = [typeof(global::Blaizio.Ui.BzSkeleton)];

    public static readonly Type[] Slider = [typeof(global::Blaizio.Ui.BzSlider)];

    public static readonly Type[] Sortable =
    [
        typeof(global::Blaizio.Ui.BzSortable), typeof(global::Blaizio.Ui.BzSortableItem),
        typeof(global::Blaizio.Ui.BzSortableHandle), typeof(global::Blaizio.Ui.BzSortableList<>),
    ];

    public static readonly Type[] Switch = [typeof(global::Blaizio.Ui.BzSwitch)];

    public static readonly Type[] Table =
    [
        typeof(global::Blaizio.Ui.BzTable), typeof(global::Blaizio.Ui.BzTableHeader),
        typeof(global::Blaizio.Ui.BzTableBody), typeof(global::Blaizio.Ui.BzTableFooter),
        typeof(global::Blaizio.Ui.BzTableRow), typeof(global::Blaizio.Ui.BzTableHead),
        typeof(global::Blaizio.Ui.BzTableCell), typeof(global::Blaizio.Ui.BzTableCaption),
        typeof(global::Blaizio.Ui.BzDataTable<>), typeof(global::Blaizio.Ui.BzColumnBase<>),
        typeof(global::Blaizio.Ui.BzPropertyColumn<,>), typeof(global::Blaizio.Ui.BzTemplateColumn<>),
    ];

    public static readonly Type[] TableOfContents = [typeof(global::Blaizio.Ui.BzTableOfContents)];

    public static readonly Type[] Tabs =
    [
        typeof(global::Blaizio.Ui.BzTabs), typeof(global::Blaizio.Ui.BzTabsList),
        typeof(global::Blaizio.Ui.BzTabsTrigger), typeof(global::Blaizio.Ui.BzTabsContent),
    ];

    public static readonly Type[] ThemeSwitcher =
    [
        typeof(global::Blaizio.Ui.BzThemeSwitcher), typeof(global::Blaizio.Ui.BzThemeMenu),
        typeof(global::Blaizio.Ui.BzThemeToggle),
    ];

    public static readonly Type[] Image =
    [
        typeof(global::Blaizio.Ui.BzImage), typeof(global::Blaizio.Ui.BzImagePlaceholder),
        typeof(global::Blaizio.Ui.BzImageFallback), typeof(global::Blaizio.Ui.BzImageSource),
    ];

    public static readonly Type[] InputText = [typeof(global::Blaizio.Ui.BzInputText)];

    public static readonly Type[] InputTime = [typeof(global::Blaizio.Ui.BzInputTime)];

    public static readonly Type[] InputDate = [typeof(global::Blaizio.Ui.BzInputDate)];

    public static readonly Type[] Toggle = [typeof(global::Blaizio.Ui.BzToggle)];

    public static readonly Type[] ToggleGroup =
        [typeof(global::Blaizio.Ui.BzToggleGroup), typeof(global::Blaizio.Ui.BzToggleGroupItem)];

    public static readonly Type[] Tooltip =
    [
        typeof(global::Blaizio.Ui.BzTooltipProvider), typeof(global::Blaizio.Ui.BzTooltip),
        typeof(global::Blaizio.Ui.BzTooltipTrigger), typeof(global::Blaizio.Ui.BzTooltipContent),
        typeof(global::Blaizio.Ui.BzTooltipArrow),
    ];

    public static readonly Type[] Tree = [typeof(global::Blaizio.Ui.BzTree<>)];

    public static readonly Type[] Virtualizer = [typeof(global::Blaizio.Ui.BzVirtualizer<>)];
}
