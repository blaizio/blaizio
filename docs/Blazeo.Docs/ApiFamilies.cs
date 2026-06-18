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
        typeof(global::Blazeo.Ui.Accordion), typeof(global::Blazeo.Ui.AccordionItem),
        typeof(global::Blazeo.Ui.AccordionTrigger), typeof(global::Blazeo.Ui.AccordionContent),
    ];

    public static readonly Type[] Alert =
    [
        typeof(global::Blazeo.Ui.Alert), typeof(global::Blazeo.Ui.AlertTitle),
        typeof(global::Blazeo.Ui.AlertDescription), typeof(global::Blazeo.Ui.AlertAction),
    ];

    public static readonly Type[] AlertDialog =
    [
        typeof(global::Blazeo.Ui.AlertDialog), typeof(global::Blazeo.Ui.AlertDialogTrigger),
        typeof(global::Blazeo.Ui.AlertDialogContent), typeof(global::Blazeo.Ui.AlertDialogHeader),
        typeof(global::Blazeo.Ui.AlertDialogFooter), typeof(global::Blazeo.Ui.AlertDialogMedia),
        typeof(global::Blazeo.Ui.AlertDialogTitle), typeof(global::Blazeo.Ui.AlertDialogDescription),
        typeof(global::Blazeo.Ui.AlertDialogAction), typeof(global::Blazeo.Ui.AlertDialogCancel),
    ];

    public static readonly Type[] AspectRatio = [typeof(global::Blazeo.Ui.AspectRatio)];

    public static readonly Type[] Avatar =
    [
        typeof(global::Blazeo.Ui.Avatar), typeof(global::Blazeo.Ui.AvatarImage),
        typeof(global::Blazeo.Ui.AvatarFallback), typeof(global::Blazeo.Ui.AvatarBadge),
        typeof(global::Blazeo.Ui.AvatarGroup), typeof(global::Blazeo.Ui.AvatarGroupCount),
    ];

    public static readonly Type[] Badge = [typeof(global::Blazeo.Ui.Badge)];

    public static readonly Type[] Button = [typeof(global::Blazeo.Ui.Button)];

    public static readonly Type[] Card =
    [
        typeof(global::Blazeo.Ui.Card), typeof(global::Blazeo.Ui.CardHeader),
        typeof(global::Blazeo.Ui.CardTitle), typeof(global::Blazeo.Ui.CardDescription),
        typeof(global::Blazeo.Ui.CardAction), typeof(global::Blazeo.Ui.CardContent),
        typeof(global::Blazeo.Ui.CardFooter),
    ];

    public static readonly Type[] Checkbox = [typeof(global::Blazeo.Ui.Checkbox)];

    public static readonly Type[] Collapsible =
    [
        typeof(global::Blazeo.Ui.Collapsible), typeof(global::Blazeo.Ui.CollapsibleTrigger),
        typeof(global::Blazeo.Ui.CollapsibleContent),
    ];

    public static readonly Type[] Dialog =
    [
        typeof(global::Blazeo.Ui.Dialog), typeof(global::Blazeo.Ui.DialogTrigger),
        typeof(global::Blazeo.Ui.DialogContent), typeof(global::Blazeo.Ui.DialogHeader),
        typeof(global::Blazeo.Ui.DialogFooter), typeof(global::Blazeo.Ui.DialogTitle),
        typeof(global::Blazeo.Ui.DialogDescription), typeof(global::Blazeo.Ui.DialogClose),
        typeof(global::Blazeo.Ui.DialogOverlay),
    ];

    public static readonly Type[] DirectionProvider = [typeof(global::Blazeo.Ui.DirectionProvider)];

    public static readonly Type[] DropdownMenu =
    [
        typeof(global::Blazeo.Ui.DropdownMenu), typeof(global::Blazeo.Ui.DropdownMenuTrigger),
        typeof(global::Blazeo.Ui.DropdownMenuContent), typeof(global::Blazeo.Ui.DropdownMenuGroup),
        typeof(global::Blazeo.Ui.DropdownMenuItem), typeof(global::Blazeo.Ui.DropdownMenuCheckboxItem),
        typeof(global::Blazeo.Ui.DropdownMenuRadioGroup), typeof(global::Blazeo.Ui.DropdownMenuRadioItem),
        typeof(global::Blazeo.Ui.DropdownMenuLabel), typeof(global::Blazeo.Ui.DropdownMenuSeparator),
        typeof(global::Blazeo.Ui.DropdownMenuShortcut), typeof(global::Blazeo.Ui.DropdownMenuSub),
        typeof(global::Blazeo.Ui.DropdownMenuSubTrigger), typeof(global::Blazeo.Ui.DropdownMenuSubContent),
    ];

    public static readonly Type[] Field =
    [
        typeof(global::Blazeo.Ui.Field), typeof(global::Blazeo.Ui.FieldSet),
        typeof(global::Blazeo.Ui.FieldLegend), typeof(global::Blazeo.Ui.FieldGroup),
        typeof(global::Blazeo.Ui.FieldContent), typeof(global::Blazeo.Ui.FieldLabel),
        typeof(global::Blazeo.Ui.FieldTitle), typeof(global::Blazeo.Ui.FieldControl),
        typeof(global::Blazeo.Ui.FieldDescription), typeof(global::Blazeo.Ui.FieldError),
        typeof(global::Blazeo.Ui.FieldSeparator),
    ];

    public static readonly Type[] Input = [typeof(global::Blazeo.Ui.Input)];

    public static readonly Type[] Kbd = [typeof(global::Blazeo.Ui.Kbd), typeof(global::Blazeo.Ui.KbdGroup)];

    public static readonly Type[] Label = [typeof(global::Blazeo.Ui.Label)];

    public static readonly Type[] Popover =
    [
        typeof(global::Blazeo.Ui.Popover), typeof(global::Blazeo.Ui.PopoverTrigger),
        typeof(global::Blazeo.Ui.PopoverContent), typeof(global::Blazeo.Ui.PopoverClose),
        typeof(global::Blazeo.Ui.PopoverHeader), typeof(global::Blazeo.Ui.PopoverTitle),
        typeof(global::Blazeo.Ui.PopoverDescription),
    ];

    public static readonly Type[] Progress = [typeof(global::Blazeo.Ui.Progress)];

    public static readonly Type[] RadioGroup =
        [typeof(global::Blazeo.Ui.RadioGroup), typeof(global::Blazeo.Ui.RadioGroupItem)];

    public static readonly Type[] Select =
    [
        typeof(global::Blazeo.Ui.Select), typeof(global::Blazeo.Ui.SelectTrigger),
        typeof(global::Blazeo.Ui.SelectValue), typeof(global::Blazeo.Ui.SelectContent),
        typeof(global::Blazeo.Ui.SelectGroup), typeof(global::Blazeo.Ui.SelectLabel),
        typeof(global::Blazeo.Ui.SelectItem), typeof(global::Blazeo.Ui.SelectSeparator),
    ];

    public static readonly Type[] Separator = [typeof(global::Blazeo.Ui.Separator)];

    public static readonly Type[] Sheet =
    [
        typeof(global::Blazeo.Ui.Sheet), typeof(global::Blazeo.Ui.SheetTrigger),
        typeof(global::Blazeo.Ui.SheetContent), typeof(global::Blazeo.Ui.SheetHeader),
        typeof(global::Blazeo.Ui.SheetFooter), typeof(global::Blazeo.Ui.SheetTitle),
        typeof(global::Blazeo.Ui.SheetDescription), typeof(global::Blazeo.Ui.SheetClose),
    ];

    public static readonly Type[] Skeleton = [typeof(global::Blazeo.Ui.Skeleton)];

    public static readonly Type[] Switch = [typeof(global::Blazeo.Ui.Switch)];

    public static readonly Type[] TableOfContents = [typeof(global::Blazeo.Ui.TableOfContents)];

    public static readonly Type[] Tabs =
    [
        typeof(global::Blazeo.Ui.Tabs), typeof(global::Blazeo.Ui.TabsList),
        typeof(global::Blazeo.Ui.TabsTrigger), typeof(global::Blazeo.Ui.TabsContent),
    ];

    public static readonly Type[] Textarea = [typeof(global::Blazeo.Ui.Textarea)];

    public static readonly Type[] Toggle = [typeof(global::Blazeo.Ui.Toggle)];

    public static readonly Type[] ToggleGroup =
        [typeof(global::Blazeo.Ui.ToggleGroup), typeof(global::Blazeo.Ui.ToggleGroupItem)];

    public static readonly Type[] Tooltip =
    [
        typeof(global::Blazeo.Ui.TooltipProvider), typeof(global::Blazeo.Ui.Tooltip),
        typeof(global::Blazeo.Ui.TooltipTrigger), typeof(global::Blazeo.Ui.TooltipContent),
        typeof(global::Blazeo.Ui.TooltipArrow),
    ];
}
