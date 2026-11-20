# Changelog

All notable changes to the Blaizio packages (`Blaizio.Base`, `Blaizio.Icons`, `Blaizio.Syntax`,
`Blaizio.Syntax.Languages`, `Blaizio.Cli`, `Blaizio.Cli.Core`, `Blaizio.Cli.Contracts`) and the
registry-distributed `Blaizio.Ui` source.
Format loosely follows [Keep a Changelog](https://keepachangelog.com); the packages release in
lockstep under one version.

## Unreleased

### Fixed
- **Color Picker**: a `Gradient` handed in on the first render opens the Gradient surface. The
  picker's starting color (its default, or `DefaultValue`) was read after it and put the picker
  back on Solid. A `Value` set next to it still decides, as before.
- **Color Picker**: an image source with a space, a bracket or a quote in it makes a valid paint.
  `ImageFillValue.ToCss` wrote every source bare, so `url(/photos/sunset (1).png)` was dropped by
  the browser and read back cut at the first bracket. Such a source is now written quoted
  (`url("/photos/sunset (1).png")`) and `TryParse` reads a quoted source to its closing quote.
  A source without those characters is written bare, as before.
- **Color Picker**: an `oklab()` axis that rounds to zero from below is written `0`, not `-0`
  (`oklab(0.263 0 -0.035)`).
- **Color Picker**: a color function with a word where a number belongs is refused. A channel
  that could not be read counted as 0, so `rgb(a b c)` parsed as black. A percentage, a hue in
  `deg` and `none` (read as 0) are still accepted; a hue in another unit (`turn`, `rad`) was read
  as 0 and is now refused too.
- **Image**: an image with no `Src` at all (null from the first render) is idle. It stayed at
  `data-state="loading"` and `StatusChanged` never reported `Idle`; an empty string did.
- **Script modules**: an import that the runtime refuses at once is tried again by the next
  caller. `JsModules` evicted a failed import before it had stored it, so that failure stayed
  cached and every later caller on the same runtime got it back.
- **Input Tags**: a disabled field says `aria-disabled="true"` on its group. Only `data-disabled`
  was set, so nothing told assistive technology that the dimmed tags are inactive.
- **Input Tags**: in a bound field (`@bind-Values`) a paste with several delimiters keeps every
  tag. Each segment was added to the list as it was before the paste, so only the last one
  survived. A field left to itself was not affected.
- **Resizable**: a panel that is rendered later takes its place in the markup. A panel and its
  handle inside an `if` joined at the end of the group's order, whatever their position. After a
  first or middle panel was hidden and shown again, its handle had no neighbour and moved
  nothing, and the next handle resized two panels that were no longer side by side. The group now
  reads the order of its panels and handles from the page after such a render and follows it.
  No size changes hands in the reorder.
- **Data Table**: with `Virtualize` and `DynamicRowHeight`, a new sort or search measures the rows
  again. The virtualizer read a cleared search as rows appended and a sort as nothing changed,
  and kept the heights it had measured for other rows, so the scrollbar and the rows drifted
  until each row had been on screen again. Rows of a fixed height were never affected, and a list
  that only grows under the same sort and search is still an append.
- **Select, Popover, Dialog, Alert Dialog, Sheet, Drawer**: a surface opened again while its exit
  animation plays gets the keyboard focus back. The press that closed it (on its trigger, on the
  page, on a dialog's backdrop) or the one that opened it again had moved focus out, and the
  opening focus was placed only for a new surface. A select list reopened by a press on its
  trigger was left with focus on the trigger: Escape and the arrows did nothing until the pointer
  was used again. A popover or a dialog was open with focus behind it, so Tab started from the
  page. Focus now goes where a first opening puts it (the selected option, the first field, or
  the surface itself). Focus that is still inside stays where it is, and `OnOpenAutoFocus` is
  raised again and can prevent the move. The menus already did this.
- **Popover, Hover Card, Tooltip, Select, Combobox and the menus**: a surface opened again while
  its exit animation plays keeps the side it is really on. One that had flipped to stay in view
  (asked for the bottom, placed on top) went back to `data-side="bottom"` and kept saying so for
  as long as it stayed open, because the positioning script reports a side only when it changes.
- **Dropdown Menu, Context Menu, Menubar, Select, Combobox**: a surface says from its first frame
  which side it opens to. `data-side` and `data-align` started as `bottom` and `start` whatever
  was asked for (and, on a later opening, from wherever the last one ended up) until the
  positioning script answered. A submenu, which opens to the side, or a menu with `Side="Top"`
  began its entry slide from the wrong direction and changed it a moment later. Popover, Hover
  Card and Tooltip already started from the side that was asked for.
- **Dialog, Popover, Hover Card, Tooltip, Select, Combobox and the menus**: a surface taken off
  the page while one of its script modules was still loading no longer asks that module for an
  instance. The call was made for an element that was gone, and the error it ended in was raised
  while the component was being disposed.
- **Data Table**: a table fed by `ItemsProvider` can load its rows again. Identical requests are
  deduplicated, so rows added, deleted or edited at the source stayed stale until the user sorted,
  searched or changed page. The new `RefreshDataAsync()` asks for the current page again. And when
  the source has shrunk under the current page, the table moves to the last page and raises
  `PageChanged`; it used to show "No results." under a pager with no current page.
- **Input Number**: leaving a field nobody typed in no longer changes its value. Tabbing through
  snapped a value that sat off the step grid (2.5 with a `Step` of 1 became 3) and clamped one
  outside `Min` and `Max`, raising `ValueChanged` for an edit the user never made. A typed value
  is clamped and snapped on leaving, as before.
- **Dialog, Alert Dialog, Sheet, Drawer, Popover**: one Escape closes one surface. An Escape that
  a control inside had already used (a list it closed, a move it cancelled) went on to close the
  dialog or popover around it. And a dialog opened from a popover no longer loses that popover,
  and with it sometimes itself, to the first press inside the dialog. Dialogs now take their
  place among the popovers and menus: the topmost surface answers Escape, wherever the focus is
  (before, the focus had to be inside the surface).
- **Sidebar**: tooltips inside `BzSidebarProvider` wait the configured delay again. The provider
  set a delay of 0 for its whole content, which is usually the whole app, and that overrode the
  delay in `BlaizioOptions`. The tooltip of a menu button on a collapsed rail still opens at once.
- **Input Date**: pressing the selected day in the calendar popup keeps the date and closes the
  popup. It cleared the field.
- **Tree**: the checkbox of a branch whose children are not loaded yet loads them (`LoadChildren`)
  and checks them, leaving the branch collapsed. The press did nothing. A load that fails raises
  `OnLoadChildrenError` and checks nothing.
- **Tree**: with `Virtualize`, rows that are not `ItemSize` tall are reported to the app's error
  handling (an error boundary), as the parameter says. The exception went back to the script that
  measured the row and ended as a line in the browser console.
- **Code Block**: copying a diff that holds only removed lines copies nothing. It copied the
  removed lines, run together. Removed lines carry a new `data-bz-copy-skip` attribute, which the
  copy script leaves out.
- **Item, Marker** (`blaizio update item marker`): with `OnClick` set, the default element can be
  reached and pressed from the keyboard. It was a `div` with a click handler and nothing else. It
  now has `role="button"`, a tab stop, and Enter and Space press it (a new `data-bz-key-click`
  attribute, served by `core.js`; both components now need Blaizio.Base 0.3.6). An element of
  your own through `RenderAs` is left as it is.
- **Carousel**: the arrow keys inside a slide belong to what is focused there. An arrow pressed in
  a text field, select, slider, tab list, menu or radio group inside a slide moved the caret (or
  the control) and the slide together; the carousel now leaves the key alone there. Where it does
  take the key the page no longer scrolls along, and arrow keys held with Ctrl, Alt or the Command
  key are left to the browser.
- **Carousel**: slides added or removed after the first render are counted. The dots and the
  previous and next buttons kept the old count until the next scroll or resize.
- **Message Scroller**: the transcript stays at the end when the viewport gets shorter (the
  composer grows a line, an on-screen keyboard opens). Only the content was watched, so the last
  lines slid under the fold.
- **Message Scroller**: a wheel-up that scrolls nothing no longer switches follow off. In a
  transcript shorter than the viewport, at the very top, or over a scrolling block inside a
  message, the messages that came later were not followed.
- **Toast**: a toast updated in place (`Show` with the same id, or `Update`) gets its full time on
  screen again. With an unchanged duration the old countdown kept running, so a toast updated late
  in its life closed a moment after the update. Each toast element carries a new
  `data-bz-toast-revision` attribute once it has been updated.
- **Combobox**: Enter no longer submits the surrounding form while the list is open. It did
  whenever no option was highlighted, which with `AutoHighlight` off is the usual state after
  typing a filter.
- **Hotkey**: a hotkey with `Disabled` set leaves the key to the browser. It ignored the press but
  still prevented the default, so the combination did nothing at all.
- **Input Tags**: pasted text that was split into tags is cleared from the box. It stayed there,
  Enter did not clear it, and with `AllowDuplicates` the next keystroke added the same tags again.
- **Tooltip**: a controlled tooltip that its parent keeps closed no longer takes the hover delay
  off every other tooltip under the same provider.
- **Tree**: pressing the same letter again moves to the next node that starts with it. The second
  press used to do nothing.
- **Tree**: the checkbox of a branch with no leaves under it (children not loaded yet, or all
  disabled) no longer raises `CheckedValuesChanged` with an unchanged list.
- **Avatar**: when `Src` is cleared after the image loaded, the fallback shows again, and a new
  `Src` stays hidden until it has loaded. The avatar used to keep an empty image and no fallback.
- **Progress**: a value that is not a number (`0.0 / 0`) is shown as 0. It rendered a full bar and
  `aria-valuenow="NaN"`.
- **Copy Button**: clicking again while an `OnCopied` handler is still running no longer throws.
- **Input Number**: holding a step button no longer runs on after release when `ValueChanged` is
  slow. A release that arrived while the first step's handler was still running was missed, and
  the value kept counting.
- **Chart**: a pie with a single slice and a radial bar track close to a full turn are drawn at
  small sizes. They came out empty once both ends of the arc rounded to the same point. A value
  that is not a number, or is infinite, is drawn as 0 in the cartesian, radar, pie and radial bar
  charts; one such value used to blank the whole chart. A `TickCount` below 1 no longer asks for
  another render on every render.
- **Drawer**: a drag dismissal that the parent refuses (a controlled drawer whose `OpenChanged`
  keeps it open) slides the panel back. The drawer stayed open off screen, with focus trapped and
  nothing to see. `SnapPoints` with no value above 0 and up to 1 (percentages, for example) now
  opens a plain drawer; it opened an invisible one.
- **Input Text, Input Group, Toolbar**: text still waiting on `DebounceDelay` is raised when the
  field loses focus and before `OnKeyDown` hears Enter. A submit-on-Enter handler or a Save button
  used to read the text from before the last burst of typing.
- **Input Time**: with `MinuteStep` above 1, the first Arrow Up on an empty minute lands on 00. It
  landed off the step (14 for a quarter-hour step).
- **Input Date, Input Time**: a letter typed into an empty segment no longer stays in the box. The
  segment's value did not change, so nothing was sent to the browser to take the letter out.
- **Data Table**: a table with sized columns that is scrolled sideways no longer jumps back to its
  first column when a web font finishes loading or a column is added. The column widths are
  measured again then, and the scroll position was lost in the measuring; a cell that held the
  keyboard cursor could end up hidden under a pinned column.
- **Data Table**: the rows are filtered and sorted once when the table first renders, not once per
  child column, and not at all while the table is being removed.
- **Theme**: the font knob leaves the app's own `font-*` classes on `<html>` alone. A font
  utility from your own `@theme` (`font-display`) or one with the important mark (`font-sans!`)
  was read as the selected font and removed when a font was applied, again on every load once
  a font was stored. The knob now touches only the class it added. A host that renders a
  `font-*` overlay class in its own `<html>` markup is no longer picked up; none of the
  templates does. The theme setters also refuse a name with whitespace before storing it; they
  used to store it and then fail.
- **Image**: on a statically rendered page the image is shown. Nothing reports a load there, so
  it stayed transparent for good. It now counts as loaded from the start when the page has no
  render mode; while an interactive component is prerendering it waits as before.
- **Chart**: replacing `Data` with a shorter list while the pointer rests on a point no longer
  throws. The cartesian and radar charts kept the hovered index and read past the end.
- **Menubar, Navigation Menu**: on a touch screen, tapping another menubar trigger switches to
  its menu (it used to close the whole bar), and a tap inside an open navigation menu panel no
  longer closes it unless it lands on a link.
- **Dropdown Menu, Context Menu, Menubar**: a menu reopened while it is still closing takes the
  keyboard again (focus stayed on the page, so Escape and the arrows did nothing). A submenu now
  closes with its menu: it used to come back invisible with the next opening, its trigger still
  reporting `aria-expanded="true"`.
- **Dialog, Sheet, Popover, Hover Card, menus**: a surface whose component is removed while it is
  open no longer leaves a hidden copy of itself in the page (a mobile sidebar sheet open while
  the window grows past the breakpoint, a dialog inside an `@if`).
- **Dialog, Sheet, Popover, Hover Card, Tooltip, menus, Accordion, Collapsible**: closing while
  the opening animation is still running plays the closing animation. It used to be skipped.
- **Accordion, Collapsible**: closing, reopening and closing again within the animation time no
  longer removes the panel partway through the second close.
- **Tree**: a virtualized tree no longer goes blank when a search or a collapse shortens the list
  while it is scrolled far down. After a drag the browser cancelled, the next click on a row is
  no longer swallowed.
- **Resizable**: a resize handle follows `Disabled` when it changes after the first render: one
  that started disabled could never be dragged, and one disabled later still could. A handle
  removed in the middle of a drag no longer leaves the resize cursor on the page.
- **Input Number**: negative numbers work in cultures that do not write the minus sign as a plain
  `-` (Hebrew, Arabic, Persian, Swedish, Norwegian, Finnish). A typed `-5` was refused, and a
  field holding a negative value could not be typed into. The focused field shows a plain `-`.
- **Slider**: with two thumbs on the same value, dragging the pair or clicking the track beyond
  it moves the thumb that can go that way. The pair used to be stuck.
- **Carousel**: asking for a slide the viewport cannot move to (it is already at its end) no
  longer leaves the carousel ignoring the next scroll.
- **Sortable**: `Disabled` changed while the list is starting up now reaches the script.
- **Toolbar**: `Overflow` changed while the toolbar is starting up no longer leaves the previous
  mode wired.
- **Carousel**: a mouse click on a button, link or checkbox inside a slide reaches it again. The
  viewport took hold of the pointer on every press, so the click went to the viewport instead.
  It now takes hold only once the press has become a drag.
- **Drawer**: a drawer can be dragged shut with a finger. On a touch screen the browser took the
  gesture for its own panning after the first move, so the panel never followed the finger and
  the drawer sprang back. Scrolling inside the drawer is unchanged.
- **Theme**: the persisted theme, style, palette, radius, fonts and direction survive enhanced
  navigation. On a statically rendered page the first in-app link put `<html>` back to its markup
  defaults until a full reload. `boot.js` now applies them again after each enhanced page update.
- **Avatar**: on a statically rendered page the image stays visible after enhanced navigation.
  It used to fall back to the initials on the first in-app link.
- **Calendar, Input Date**: the first and the last month a calendar can show (January 0001 and
  December 9999) render instead of throwing, so typing `31/12/9999` and opening the popup no
  longer takes the page down. The padding days past either end are empty cells, paging stops
  there, and the arrow keys stop at the last day. A `Min`, `Max` or value outside the span of the
  culture's own calendar (Umm al-Qura covers 1900 to 2077) no longer throws either.
- **Calendar**: after paging to another month, the arrow keys move from the day that holds the
  tab stop. They used to start from the day focused before paging and jump the view back to it.
- **Input Number**: a `float` field shows the number it holds when focused (`0.1`, not
  `0.100000001490116`), and leaving the field no longer changes the value.
- **Data Table**: in a number cell edited in place, a typed `1.5` is 1.5 in every culture. Where
  `.` groups thousands (de-DE, es-ES and others) it used to commit 15, and a whole-number cell
  took it as 15 instead of refusing it. Pasted numbers with grouping are read as before.
- **Data Table, Virtualizer**: an `ItemsProvider` that fails with a cancellation of its own (a
  client timeout) is reported like any other failure and can be asked again. It used to be taken
  for a superseded request, which left the rows loading for good. A request that really was
  superseded is now ignored whatever it throws; an error raised by a cancelled call used to
  reach the app's error handling.
- **Toolbar, Tabs, Radio Group, Toggle Group, Menubar**: the group always keeps one reachable
  tab stop. A toolbar that fits its container could put it on the hidden scroll button, and a
  group whose tab-stop item was disabled or removed had none, so Tab skipped it. Items added
  later no longer become extra tab stops.
- **Table of Contents**: opening a link in a new tab (Ctrl or middle click) or copying its address
  gives this page at that heading. The links were `#id`, which the browser resolves against
  `<base href>`, so it opened the site root. The links' `href` is now the page path plus `#id`.
- **Drawer**: a drawer closed before its drag script had loaded can be dragged the next time it
  opens.
- **Resizable**: leaving a page while its resize handles are still starting up no longer throws
  `ObjectDisposedException` (and shows the Blazor error banner). A handle disposed before its JS
  setup finishes now skips the setup, and tears down a JS instance that arrives after disposal.
- **Resizable**: a resize handle's `aria-valuenow`, `aria-valuemin` and `aria-valuemax` now follow
  the layout. They used to stay at their first-render values (`0`, then never updated on drag or
  keyboard resize). Panels with no content now resize too.
- **Input Date**: typing `0` as the first digit of the day or month (the start of `05`) no longer
  throws when the other segments are filled. The value stays empty until the segment holds a real
  day or month.
- **Input Color**: the picker button no longer submits a surrounding form. It is now
  `type="button"`.
- **Chart**: a pie chart's tooltip shows the hovered slice when the data has zero or negative
  items. It used to show a neighbouring slice, or nothing.
- **Toast**: the toast provider unsubscribes from the toast store when it is disposed. The store
  used to keep the disposed provider referenced for the rest of the scope.
- **Tooltip, Hover Card**: your own `@onpointerenter`, `@onpointerleave` or `@onpointerdown` on a
  trigger no longer turns off hover-open. Your handler now runs after the component's instead of
  replacing it. The same holds for any event a Blaizio.Base component handles without reading its
  arguments.
- **Accordion, Collapsible**: an open panel measures its height once per content change. After
  the first change it used to measure again on every frame for as long as it stayed open.
- **Combobox**: in a multiple-selection combobox, a chip's remove button still removes that chip
  after an earlier chip was removed. In a loop without `@key` it used to target the value the
  chip held before.
- **Resizable**: panels are laid out from `DefaultSize` on the first frame and on statically
  rendered pages. They used to collapse to zero until the first interactive render.
  `data-panel-size` carries the declared size there too, where it used to read `0`.
- **Data Table**: when an `ItemsProvider` throws, the table leaves its loading state and the same
  request can run again. It used to stay loading and skip the retry.
- **Dialog service**: `ConfirmAsync` and `ShowSheetAsync` called without options now start from
  `BlaizioOptions.DialogDefaults`, as `ShowAsync` does. `IDialogService` gains a `Defaults`
  property with a default implementation, so the updated `DialogServiceExtensions.cs` needs this
  version of Blaizio.Base.
- **Dropdown Menu, Context Menu, Menubar, Select**: moving through a long, scrolling list with the
  arrow keys, Home, End or by typing now scrolls the focused item into view. Focus used to move
  out of sight.
- **Dialog, Sheet, Popover, Hover Card, Tooltip**: a surface reopened while its close animation
  is finishing no longer stays invisible (and, for a dialog, focus-trapped with the page locked).
- **Drawer**: with `ScaleBackground`, content inside an open drawer now updates when the
  component that owns the drawer re-renders. It used to show the state from when it opened.
- **Drawer**: dragging a drawer shut after `PreventDismiss` was turned on while it was open slides
  it back. It used to slide off screen and leave the modal open and invisible.
- **Sortable**: setting `Disabled` on a list after it rendered now stops pointer dragging too.
- **Sortable**: a list whose first item is disabled can be reached with Tab again, and the arrow
  keys step over disabled items instead of stopping on one. A disabled item can still be shifted
  by items dropped around it; its `Disabled` summary no longer says otherwise.
- **Tree**: a node dragged to another tree is moved only when both trees' `CanDrop` allow it.
  When just one refused, the node used to disappear or appear twice. `CanDrop` now runs more
  than once for such a drop, so keep it free of side effects.
- **Carousel**: dragging or wheeling during a Previous / Next / dot scroll no longer freezes the
  selected slide, the dots and `SelectedIndexChanged`. The carousel now takes the slide it came
  to rest on, and raises `SelectedIndexChanged` for it.
- **Input Number**: in cultures that group thousands with `.` (German, Spanish, and others),
  typing `1.5` now gives 1.5. It used to give 15. Typed text is no longer read as grouped, so
  `1.234` there now means 1.234, not 1234.
- **Input OTP**: after the bound value is cleared from code, a character the `Pattern` rejects no
  longer brings the previous code back into the field.
- **Color Picker**: a gradient edited with the format set to HSB or CMYK now produces valid CSS.
  Those stops are written as `rgb()` in `Value`; the gradient model keeps them as picked.
- **Color Picker**: a picked image keeps painting after you switch the picker to another surface
  or close the popover around it, and can be rotated and exported when you come back. Its
  object URL is now released when the picture is replaced, not when the surface goes away.
- **Data Table**: `Virtualize` together with `ItemsProvider` (and no `PageSize`) now shows the
  loaded rows. It used to show "No results.".
- **Data Table**: moving a column no longer throws when the table has a `BzEditActionsColumn`
  that is not showing (Cell edit mode, or nothing editable).
- **Data Table**: `BzPropertyColumn` no longer recompiles `Property="x => x.Name"` each time the
  page re-renders, which made large or busy tables slower than they needed to be.
- **Resizable**: dragging a handle by touch now resizes the panels instead of scrolling the page,
  and a drag the browser interrupts ends cleanly. It used to leave the resize cursor on the page
  and keep resizing on later pointer moves.
- **Panel**: dragging the resize handle by touch now resizes the panel instead of scrolling the
  page, and an interrupted drag ends cleanly and commits the size it reached.
- **Carousel**: a mouse drag the browser interrupts now settles on the nearest slide, and the
  next click on a slide is no longer swallowed.
- **Tree**: a drag the browser interrupts no longer moves the node to the row it last hovered.
- **Sortable**: a drag the browser interrupts no longer reorders or transfers the item. The list
  goes back to the order it had.
- **Drawer**: a drag the browser interrupts no longer closes the drawer or changes its snap
  point.
- **Toast**: pressing a toast button and letting go outside the stack no longer leaves the stack
  expanded with its toasts never closing on their own.
- **Toast**: a swipe the browser interrupts puts the toast back instead of leaving it shifted.
- **Calendar, Carousel, Collapsible, Accordion, Color Area, Combobox, Command, Dropdown Menu,
  Menubar, Select, Input OTP, Input Tags, Message Scroller, Navigation Menu, Scroll Area, Slider,
  Sortable, Table virtualizer, Table of Contents, Toast, Tree, Resizable, Focus Scope, Hotkey,
  Roving Focus**: a component removed while it is still wiring up (leaving a page quickly, an
  item removed right after it rendered) no longer leaves its listeners and observers running
  on the page.
- **Copy Button**: leaving a page before its copy buttons finished wiring up no longer raises an
  unhandled `ObjectDisposedException`.
- **Drawer, Panel, Select, Sidebar, Toolbar, Data Table**: a component removed while it is
  still wiring up no longer leaves its listeners and observers running on the page.
- **Input Number**: leaving a read-only or disabled field no longer clamps or snaps its value or
  raises `ValueChanged`. Focusing a value with more than six decimals no longer truncates it. With
  a fractional `Min` the step grid keeps that fraction (`Min="0.5" Step="1"` gives 0.5, 1.5, 2.5).
- **Slider**: with a fractional `Min` the thumb lands on the real grid (`Min="0.5" Step="1"` gives
  0.5, 1.5, 2.5) and Home no longer goes below `Min`.
- **Input Tags**: with `Tab` (or another named key) as a delimiter, typing or pasting text that
  contains the word, such as "Tablet", no longer splits it.
- **Input OTP**: inside a field, the label, description and error now reach the input. `id`,
  `name`, `required` and the naming `aria-*` attributes go to the `<input>`; the rest stay on the
  container. `input-otp` now requires Blaizio.Base 0.3.6.
- **Calendar**: after paging away from the day that had focus, Tab still reaches the grid. The
  Next button now disables on the month `Max` falls in; it used to allow one more month.
- **Color Picker**: dragging the hue to its far end (or pressing End) keeps the thumb there. A
  gradient angle written in `grad` is read. A swatch saved in HSB or CMYK shows its color.
- **Hotkey, Kbd**: the `up`, `down`, `left`, `right` and `return` key names now trigger. They
  rendered their glyph but never fired.
- **Toast**: a toast whose `OnDismiss` or `OnAutoClose` handler throws is still removed.
- **Tooltip**: removing an element while its tooltip is open no longer makes every later tooltip
  in the group open instantly.
- **Popover, Hover Card, Tooltip**: a surface with a non-default `Side` or `Align` no longer plays
  its first frame, entry slide and arrow for the default side.
- **Select**: a multiple select's listbox now has `aria-multiselectable="true"`.
- **Menubar**: two menubars using the same menu `Value` no longer share ids or open under the
  wrong trigger, and a `Value` with a quote no longer throws. Element ids are now generated.
- **Sidebar**: Ctrl/Cmd+B inside a rich-text editor is left to the editor. `Class` applies to
  the mobile sheet too. An `Active` menu button or sub-button has `aria-current="page"`.
- **Toolbar**: changing `Overflow` after the toolbar rendered now takes effect.
- **Scroll Area**: the viewport takes keyboard focus, so a region with no focusable content can
  be scrolled with the keyboard.
- **Table of Contents**: Ctrl, Cmd, Shift and Alt click on a link behave as the browser means them.
- **Pagination, Breadcrumb**: the ellipsis label ("More pages", "More") is announced.
- **Breadcrumb**: a link with `OnClick` and no `Href` renders a `<button>`, so the keyboard
  reaches it.
- **Chart**: a series with one point, or with every point at the same x, draws. Charts whose
  values are all negative include the zero line.
- **Tree**: double-clicking a word in the rename input selects it. It used to restart the rename
  and discard what was typed.
- **Data Table**: buttons and links in `FooterContent` or `Caption` of an editable table can be
  reached with Tab again.
- **Virtualizer**: leaving the page cancels an `ItemsProvider` request still in flight.
- **Progress**: a null `Value` (without `Indeterminate`) is announced as 0, matching the empty
  track it draws.
- **Theme**: after a failed first load of the theme script, the next call tries again. A stored
  style, preset or overlay value containing whitespace is ignored instead of breaking startup.
- **Item, Attachment**: the group no longer claims `role="list"` (its children are not list
  items), which assistive tech read as a list with nothing in it.
- **Tree**: searching a tree that loads children on demand no longer shows the "no results" text
  beside a match found in a loaded branch.
- **Theme**: setting or reading the font no longer removes or misreads Tailwind `font-*` classes
  (`font-sans`, `font-medium`, ...) that an app put on `<html>`.
- **Combobox**: with server-side search (`ShouldFilter="false"` and `Items` replaced per query), a
  multiple selection keeps the items picked under earlier queries. For a value-type item, a value
  that matches nothing is dropped instead of arriving as `default`.
- **Virtualizer, Data Table**: appending rows (infinite scroll) no longer blanks the list for a
  frame or shifts the content under the reader. A smaller `Count` still restarts from the top.
- **Avatar**: on a statically rendered page the image now shows once it loads. It used to stay
  hidden behind the fallback.
- **Resizable**: a panel rendered conditionally gives its space back when it is removed and gets
  a real size when it returns. It used to leave a ghost entry and come back zero-sized. A panel
  added after the first render joins at the end of the order.

### Changed
- **Controlled or uncontrolled: one rule for every component.** A value is controlled when it is
  set, or when its change callback is bound and no default was given. Components followed one of
  two older rules ("a callback is bound" or "the value is not null") and each got a case wrong.
  What changes:
  - A value passed one-way (`Value="x"` with no `ValueChanged`) is shown and held. It was ignored,
    so the component showed nothing and kept its own state. This covers Tabs, Select, Combobox,
    Accordion, Toggle Group, Radio Group, Menubar, the menu radio groups, Calendar, Color Area,
    Slider, Input OTP, Input Tags, Input Number, Input Color, Input Date, Input Time, the search
    text of Command, Combobox and Input Tags, and the Data Table's sort, search, page, column
    widths and column order. For a starting value the user can change, pass `DefaultValue`.
  - A default next to a callback (`DefaultValue="a" ValueChanged="Log"`) only listens: the
    component starts from the default and moves on its own. It was controlled with no value, so
    the selection never moved. Same components, plus Navigation Menu and the Tree's single
    selection.
  - Tree: `@bind-ExpandedValues`, `@bind-SelectedValues` and `@bind-CheckedValues` on a list that
    is still null are controlled from the start, and passing null again empties the tree. The
    binding started uncontrolled. The other side of it: a callback alone on those three
    (`CheckedValuesChanged="Save"`, no value and no default) is now controlled, as it already was
    on every component above, so nothing moves until the value comes back. To only listen, add
    the default: `DefaultCheckedValues="@([])"`.
  - Unchanged: `Open`, `Checked`, `Pressed`, `Expanded`, `SelectedIndex` and the Color Picker's
    color. Only a value controls them, and a callback alone still only listens.
  - A binding next to a default (`@bind-Value` with `DefaultValue`) starts from the default while
    the bound field is empty. An Accordion or Toggle Group bound that way can still close its
    last item. Limits: a parent cannot clear such a value by setting the field to null, and on a
    number type that cannot be null a one-way `Value="0"` reads as not set.

  The rule is `ControllableState.IsControlled` in Blaizio.Base, with `ControllableState.IsSet`
  for values that cannot be null; both are public for your own components. Combobox, Color
  Picker, Input Color, Input Date and Input Time call it and now need Blaizio.Base 0.3.6.
- **Overlay surfaces** (Blaizio.Base): the window and backdrop of a dialog, and the content of
  Popover, Hover Card, Tooltip, Select, Combobox and the three menus share one lifecycle
  (`PresenceSurfaceBase`, and `FloatingSurfaceBase` for the anchored ones) in place of eight
  copies of it, and hold every script instance through `JsInstance`. Parameters, events and
  rendered attributes are the same. `BaseTooltipContent` inherits an `OnDismissRequested` that
  does nothing: a tooltip has no dismissable layer to call it.
- **Docs**: the home page is one live demo. Drag its divider to compare a dashboard of styled
  components with the same dashboard in unstyled Blaizio.Base, and open "Change look" to try every
  style with every theme on the whole page. It gives the `blaizio add ... --style <style> --preset
  <theme>` command for the pair you pick.
- **"Skin" is now "style"** everywhere it is user-facing: docs, READMEs, component comments, CLI
  prompts, help text and messages ("Component style?", "Unknown style", "Style X not applied",
  "(style ember)"). One vocabulary throughout: a **style** is the shape and structure baked into
  the component source (ash, aura, ember, ...), a **theme** is the colors and fonts in one CSS
  variables file (a palette is part of a theme), and light/dark is the **mode**. Machine surfaces
  already said style (`--style`, `"style"` in `blaizio.json`, `style-*.css`, the `.style-*` class,
  the registry's `styles` list and `{base}/{style}/` URLs), so nothing changes there.
- **Dependencies**: the packages and `blaizio new` move to ASP.NET Core 10.0.14 (was 10.0.12), a
  security patch. If restore fails with `NU1605`, move your `Microsoft.AspNetCore.Components.*`
  references to 10.0.14.
- **CLI**: `init`, `new` and `update` pin TailwindMerge.NET 1.5.0, up from 1.4.0. Your own
  `text-[color-mix(...)]` in a `Class` now reads as a color, and a `Class` wins in more cases: `px-*`
  replaces a component's `ps-*` and `pe-*`, `mx-*` its `ms-*` and `me-*`, `inset-x-*` its `start-*`
  and `end-*`, and `min-h-lh` replaces another `min-h-*`. Check a `Class` that relied on both
  surviving. The components' own class merges come out the same on both versions.
- **Registry**: `panel`, `drawer`, `select`, `sidebar`, `toolbar` and `dialog` now require
  Blaizio.Base 0.3.6 or newer. `blaizio add` and `blaizio update` stop with a message on an
  older Base instead of installing source that does not compile.
- **Performance**: the enum-to-class-name lookup components use while rendering is cached. It used
  to reflect over the enum on every render of every component.
- **Base**: every options object a component hands to its script is a named type derived from
  the internal `JsOptions<T>`, which keeps its properties in a trimmed app. They were anonymous
  objects, which a trimmer empties without a warning. The script receives the same properties.
- **Dialog service**: `ShowAsync<TComponent>` renders the component itself. It went through
  `DynamicComponent`, whose type parameter a trimmer cannot follow. `TComponent` (and
  `ShowSheetAsync<TComponent>`) now carries `[DynamicallyAccessedMembers(All)]`, which only matters
  to code that passes its own type parameter on while trim analysis is on.
- **Collapsible, Accordion, Tree**: the script callbacks `OnCloseFinished` and `OnBeforeMatch` are
  declared on `CollapsiblePanelBase`, the type the script's reference is made with. The copies on
  `BaseCollapsibleContent`, `BaseAccordionContent` and `BaseTreePanel` are gone (the members are
  inherited, so calls still compile); a panel that reacts to find-in-page overrides
  `OnBeforeMatchCoreAsync`. Declared on the derived types, a trimmer removed them.
- **`EnumExtensions.GetDescription`** looks the member up with `GetField`, with the trim warning a
  project analysing its copied components used to get answered in place. Same results.

### Added
- **Dialog, Alert Dialog, Sheet, Drawer**: `PreventDismiss` on the root locks the surface. Escape
  and the backdrop stop closing it and the corner close button is hidden; a close button of your
  own, or your code, still closes it. It is the name `DialogOptions` already uses for the same
  lock. `DismissOnOutsideClick` on the content and overlay parts is unchanged and still means the
  backdrop only. Alert Dialog and Sheet pass the new parameter and now need Blaizio.Base 0.3.6.
- **Focus scope**: `BaseFocusScope.OnOpenAutoFocus` and `OnCloseAutoFocus`, the names Dialog and
  Popover content already use for the same two moments.
- **Docs**: a Changelog page (`/docs/changelog`) with a short summary of each release, a dot in
  the nav on pages added in the last couple of months, and previous and next arrows next to each
  page's title.
- **Data Table editing** (`blaizio update table`): mark a `BzPropertyColumn` `Editable` and the
  table becomes a keyboard grid, one tab stop that Tab reaches as soon as the table renders.
  `EditMode` picks how it edits. `Cell` (the default) works like a spreadsheet, one cell at a time:
  the arrows, Tab and Enter move the cursor (Enter goes down, back to the column where a run of
  Tabs began, and Shift+Enter up); Escape cancels. Space flips a bool, Delete clears a cell, and a
  block copied from a spreadsheet pastes across cells. `Row` opens an editor in every editable
  cell of the row and commits the row as one change. `Dialog` edits the row as a form in a dialog,
  hidden columns included, whose fields scroll between a fixed title and buttons. `Drawer` puts
  the same form in a sheet at the inline-end edge (right in a left-to-right page, left in a
  right-to-left one, full width on a phone), with Previous and Next buttons in its header that save
  the row (a refused value keeps it open) and open the one before or after it in the table's
  current order. `EditDialogContent` and `EditDrawerContent` swap in a surface of your own; both
  get a `DataTableEditForm<TItem>` context with `context.Field(key)` for any built-in field,
  `GetValue` / `SetValue` for your own controls, `PreviousAsync` / `NextAsync`, and `Actions` for
  the built-in footer buttons. Closing a changed form (Escape, a press outside, the close button or
  Cancel) asks first in its footer, Discard or Keep editing, with no second dialog
  (`ConfirmingDiscard`, `DiscardAsync`, `KeepEditing` for your own footer); a form with no changes
  closes at once. While a form is open its row carries `data-editing`, which each style tints, and
  when it closes the focus returns to the row it showed last. The form's labels are `EditFormTitle`,
  `EditFormDescription`, `EditFormSaveLabel`, `EditFormCancelLabel`, `EditFormDiscardQuestion`,
  `EditFormDiscardLabel`, `EditFormKeepEditingLabel`, `EditFormPreviousLabel` and
  `EditFormNextLabel`. The pointer and the keyboard start an edit differently. A click selects a
  cell, and a click on a bool cell's checkbox or switch flips it at once. A double-click (or, with
  touch or a pen, a tap on the selected cell) edits in place with the control ready: the caret at
  the clicked character, a pick list's list or a date's calendar open, the time part under the
  pointer. `EditOnClick` makes a single click or tap do that, and a click on another cell then
  commits and opens that one. The cursor's ring shows while the keyboard drives the table, after
  Tab brings the focus in or a key is pressed; a click alone draws none, and in Cell mode the cell
  under the pointer takes the row's hover tint. A pick list's or date's cell shows a chevron or
  calendar mark on hover and on the cursor's cell, and a press on it opens the list or calendar.
  `BzEditActionsColumn` gives each row an Edit button, and Save and Cancel while the row edits in
  place (in Dialog and Drawer modes Edit opens the form): icon buttons named by `EditLabel`,
  `SaveLabel` and `CancelLabel`, each with a tooltip, in a column that pins, sizes and aligns like
  any other, is as wide as Save and Cancel from the start (the columns keep their widths while a
  row edits) and stays hidden in Cell mode. Its `ChildContent` replaces the buttons and gets a
  `DataTableEditActions<TItem>` context (`Item`, `Mode`, `IsEditing`, `Busy`, `BeginAsync`,
  `SaveAsync`, `CancelAsync`). Those buttons belong to the row's edit: a press on Cancel no longer
  commits the row before it cancels, Tab from the row's last field reaches Save and Cancel, and
  Enter or Space presses them. `EditActions` on a `BzTemplateColumn` or a `DataTableColumn` gives
  your own buttons the same treatment, and `BeginEditAsync(item, key)` in Row mode puts the focus
  in that column's field. On the keyboard, typing replaces a
  cell (a number starts on a digit, a sign or the decimal separator, a date or time on a digit, a
  pick list opens and types the key into its type-ahead) and an arrow then commits and moves; F2
  edits in place (a pick list opens its list); Alt+ArrowDown opens a list or calendar, and so does
  Space on a pick list; Backspace empties a text, number, date or time cell and starts typing.
  With a list or calendar open, one Escape closes it and cancels the edit, a pick commits and
  closes the editor, and Tab picks the highlighted option and moves on. Row mode follows the same
  rules for the cell you were on, and the forms open on that column's field. An `EditorContent`
  hears how its edit started through `context.StartedBy` (Pointer, Key, Typing or Code),
  `context.TypedText` and `context.OpenPopup`. Cells, rows and forms edit with the library's own
  controls, picked from the property's type: a text box, a number field, a pick list for an enum
  (or any type given `Options`), a checkbox, a date field or a time field. `Editor` picks another
  where it fits (a `Switch` for a bool, say), and `EditorContent` swaps in any control. An editor
  lays over its cell, so the columns keep their widths while it is open. A column's own cell
  template keeps its editor: `ChildContent` gets the row, and the new `CellContent` on
  `BzPropertyColumn` and `BzTemplateColumn` gets the cell, a `DataTableCell<TItem, TValue>` (or
  `DataTableCell<TItem>`) with the row, the value and its formatted text, whether an edit is open on
  the row, the cell's refusal message and a save still running (`IsEditing`, `Error`, `Pending`).
  An editable table's cells carry `group/table-cell`, and `data-active` while they hold the
  keyboard cursor, so a template follows the cursor in CSS. `EdgeToEdge` on any column drops the
  cells' padding and stretches the content to the cell's full height, so a background tints the
  whole cell. Values parse in the
  current culture and pass the property's DataAnnotations and the column's `Validate` before they
  are written through the property's setter. `OnEdit` hears every change once per action, and
  `Reject` puts the old values back. Moving the cursor never calls .NET, so it stays instant on
  Blazor Server, and it works with `Virtualize`. `BeginEditAsync`, `CommitEditAsync`,
  `CancelEditAsync` and `IsEditing` drive it from code. In an editable table a click on a row no
  longer toggles its selection; use the checkbox or Shift+Space. The table item installs Sheet and
  Tooltip for the drawer. Needs Blaizio.Base 0.3.6 (`CellBinding`, `TabSeparatedText` and the grid
  script).
- **Input Date**: `CalendarOpen`, `DefaultCalendarOpen` and `CalendarOpenChanged` open the calendar
  popup from code and report when it opens or closes (`@bind-CalendarOpen`). A picked day raises
  `ValueChanged` before the popup reports closing.
- **Data Table column sizing, pinning and moving** (`blaizio update table`). On any column (a
  `BzPropertyColumn`, a `BzTemplateColumn` or a `DataTableColumn`): `Width`, `MinWidth` and
  `MaxWidth` in pixels, and `Fill` to take the room the other columns leave in the row (without
  one, the content-sized columns stretch to fill it, as before). `Pinned` keeps a column in place
  while the rows scroll sideways, `DataTablePin.Start` or `End`, which flip in a right-to-left
  page; the selection column joins the start group, and pinning works with `StickyHeader`,
  `MaxHeight`, `Virtualize` and editing. `ResizableColumns` lets users drag the end edge of a
  header, double-click it to fit the content, or press Alt+Shift+Left / Right on a focused header
  (Alt+Shift+Enter fits). `ReorderableColumns` lets users drag a header to a new place, or press
  Ctrl+Shift+Left / Right (Cmd on a Mac), within its pinned group; a column's `Resizable` or
  `Reorderable` opts it out, and one that cannot move keeps the others from crossing it. Bind
  `ColumnWidths` (the widths users chose, by column key) and `ColumnOrder` (every key, in order) to
  save them; `DefaultColumnWidths` and `DefaultColumnOrder` seed an uncontrolled table. The script
  lays out the widths and runs every drag and key without calling .NET, and reports a finished
  resize or move once. A laid-out column clips what does not fit with an ellipsis, a narrowed
  sortable header truncates its title and keeps the sort icon, and an editor never changes a
  column's width. A resizable header lights its resize line while it is hovered or has the
  keyboard focus (the handle itself stays the strongest), and a header that can move shows the
  grab cursor (grabbing while dragged). While a header is dragged, a label follows the pointer and
  a line marks the gap the column will drop into; held over its own place, no line shows, the same
  for every column. `LiveReorder` moves the column itself instead: it follows the pointer, the
  columns it passes slide out of its way (instantly when the user asks for reduced motion), rows
  the virtualizer adds mid-drag slide too, and on release it slides into place. Either way the
  order is reported once, on release, and Escape puts everything back. Needs Blaizio.Base 0.3.6
  (the column script).
- **Data Table in any language** (`blaizio update table`): everything `BzDataTable` says on its
  own now has a parameter, so a right-to-left or non-English app words all of it. The selection
  line takes `SelectionSummaryContent`, a template of your own given `context.Selected` and
  `context.Total`, in place of "N of M row(s) selected." (which also read scrambled in a
  right-to-left page); `SelectAllAriaLabel` and `SelectRowAriaLabel` name the selection
  checkboxes, and `ContainerAriaLabel` the scroll box that `MaxHeight` or `Virtualize` makes a
  keyboard stop. The pager takes `PaginationAriaLabel`, `PaginationPreviousLabel`,
  `PaginationPreviousAriaLabel`, `PaginationNextLabel`, `PaginationNextAriaLabel`,
  `PaginationEllipsisAriaLabel` and `PaginationPageAriaLabel`, a function of the page number. For
  editing, `EditFormCloseLabel` names the form's close button in the dialog and the drawer,
  `EditRejectedMessage` words a save refused without a message of its own, `EditDiscardedMessage`
  what follows a refused value when the focus leaves the table, and `EditParseErrorMessage` words a
  value the table refuses before saving it ("The Hours field must be a number.", "... is
  required.", "... must be one of: ...") from a `CellParseError` that gives the problem (`Kind`),
  the column's title (`Field`), a pick list's options (`Options`) and the English `Message`.
  `EditorLabels` words what the built-in date and time editors say to screen readers, and the time
  editor's AM and PM text, from a `DataTableEditorLabels` (`Day`, `Month`, `Year`, `CalendarButton`,
  `Calendar`, `PreviousMonth`, `NextMonth`, `Hour`, `Minute`, `Meridiem`, `Am`, `Pm`).
  `ColumnAnnouncement` words what screen readers hear after a key resizes or moves a column, from
  a `DataTableColumnAnnouncement` (`Kind`, `Column`, `Width`, `Position`, `Count`, `Message`);
  while it is set, the column script asks .NET for each announcement's words, one call at a time.
  On the columns, `HeaderLabel` on `BzEditActionsColumn` names its header for screen readers
  ("Actions"), and `NoneLabel` on `BzPropertyColumn` labels a nullable enum's blank option
  ("None"). Left unset, every one of them keeps the English the table said before. Needs
  Blaizio.Base 0.3.6 (`CellParseError` and the column script).
- **Date, time and overlay controls in any language** (`blaizio update input-date input-time
  calendar sheet drawer panel dialog carousel sortable tree`): what these controls say on their own
  now has a parameter, English by default. `BzInputDate` takes `DayAriaLabel`, `MonthAriaLabel`,
  `YearAriaLabel`, `CalendarButtonAriaLabel` ("Open calendar"), `CalendarAriaLabel` and, for its
  calendar popup, `PreviousMonthAriaLabel` and `NextMonthAriaLabel`; `BzCalendar` takes the same two
  month-button names and `WeekNumberLabel`. `BzInputTime` takes `HourAriaLabel`, `MinuteAriaLabel`,
  `SecondAriaLabel`, `MeridiemAriaLabel` for the AM/PM toggle ("AM or PM"), and `AmLabel` and
  `PmLabel` for the toggle's text. `CloseLabel` names the corner close button of `BzDialogContent`,
  `BzSheetContent` and `BzDrawerContent` ("Close"), and of a dialog or sheet shown from
  `IDialogService` (`BzDialogOptions.CloseLabel`, which the `BzDialogOptions` you give
  `BlaizioOptions.DialogDefaults` can carry for the whole app); `Label` names `BzPanelClose`
  ("Close panel"). On `BzCarousel`, `RoleDescription` and `SlideRoleDescription` replace what screen
  readers call the region and each slide ("carousel", "slide"), and `SlideAriaLabel` words each
  slide's "N of M" from a `CarouselSlidePosition` (`Position`, `Count`); `DotAriaLabel` on
  `BzCarouselDots` words "Go to slide N". `BzSortable` and `BzSortableList` take
  `ItemRoleDescription` ("sortable item") and `Announcement`, which words what screen readers hear
  as the keyboard picks an item up, moves it, puts it down or puts it back, from a
  `SortableAnnouncement` (`Kind`, `Position`, `Count` and the English `Message`). `BzTree` takes
  `RenameAriaLabel` for the rename box and `Announcement`, worded from a `TreeAnnouncement` (`Kind`,
  `Node` and `Message`): a node picked up, moved by keyboard or a pointer drop, put down, let go,
  renamed, or whose children failed to load. Needs Blaizio.Base 0.3.6.
- **`Blaizio.Base`**: `CellBinding.TryParse` takes a `messages` builder that words a refused text
  in the reader's language from a `CellParseError`, the English `Message` without one. A
  DataAnnotations rule that returns no message of its own is worded by the rule itself.
  The headless parts take the same words as the styled controls: `BaseCalendar` takes
  `PreviousMonthAriaLabel` and `NextMonthAriaLabel`; `BaseCarousel` takes `RoleDescription`,
  `SlideRoleDescription` and `SlideAriaLabel`; `BaseSortable` takes `ItemRoleDescription` and
  `Announcement`; `BaseTree` takes `RenameAriaLabel` and `Announcement`.
- **Trace** (`blaizio add trace`): the steps an agent takes, one line each - an icon, a title, an
  optional description and a status (done, running with a shimmering title, error, pending). A
  line expands (`BzTraceCollapsible`), links out, or opens a dialog or sheet through the trigger's
  `RenderAs`. `BzTraceStatusIcon` draws the line's status by itself, and `AutoOpen` keeps a row
  open while its step runs until the reader opens or closes it. The chevron mirrors in RTL.
  Needs Blaizio.Base 0.3.6.
- **Code Block** (`blaizio add code-block`): code, terminal output, logs and diffs, with a header,
  floating actions, line numbers, a start line, wrapping, highlighted and diff lines (`Diff` reads
  `+` / `-` prefixes from a plain string), a footer, a small size and a destructive variant.
  `BzCodeBlockCopyButton` copies the block's own code by default (never the line numbers, diff
  markers or removed lines) and shows when the clipboard refused the write;
  `BzCodeBlockWrapButton` toggles wrapping. Both buttons name themselves in a tooltip with an arrow
  (`Tooltip`, on by default; the wrap button's says what a click does next, `UnwrapLabel` while the
  block wraps), and take an `Icon` (plus `CopiedIcon` and `FailedIcon`) or a `ChildContent` that
  receives the state, so a text button can read "Copied"; the item installs Tooltip with them.
  `Language` highlights the code in the theme's own
  colors: C#, Razor, JSON, bash, console sessions, PowerShell, TypeScript, JavaScript, HTML, XML,
  CSS, SQL, YAML, diff, HTTP request files, Dockerfile, INI, TOML and Markdown, with their common
  aliases, one language inside another where they nest (C# and HTML in Razor, CSS and JavaScript in
  HTML, scripts in YAML, any of them in a Markdown fence), and with `Diff` for a highlighted diff,
  plus any language the app adds in `AddBlaizio` (from Blaizio.Syntax.Languages, or a
  `SyntaxGrammar` of its own). Every token kind mixes a theme status color with the text color,
  which keeps it readable in every theme, light and dark. Highlighting runs in C# as the block
  renders (no script, the same in static rendering, on the server and in WebAssembly), and streamed
  code re-tokenizes and re-renders only its new and changed lines. `Language="ansi"` renders
  terminal colors in the theme's own colors. Needs Blaizio.Base 0.3.6; the item installs
  Blaizio.Syntax, the highlighter.
- **`Blaizio.Syntax`**, a new package: the highlighter, kept out of Blaizio.Base so an app without
  a Code Block never downloads it. `SyntaxText` splits code into runs of one `SyntaxKind` per line
  for 19 built-in languages (hand-written C# scanners, no script, never throws on content), and
  `SyntaxDocument` keeps each line's tokens and end state so code that grows or changes tokenizes
  only its new and changed lines. Add languages in `AddBlaizio(options => options.Syntax.Add(...))`,
  per app: `Alias` gives one another name, and a language with a built-in's name replaces it,
  including inside the languages that host others (a Markdown fence, an HTML script or style
  element, a console command). Write one with `SyntaxGrammar`, a table of comments, strings (with
  escapes, prefixes, interpolation holes and lines they span), heredocs, word lists, declarations,
  keys, sigils, numbers, function and type names by their shape, and regular expressions for the
  rest; or derive from `SyntaxLanguage`, now public with `SyntaxWriter`: immutable line states that
  compare by value, a scanner that always advances, ranges in order, and a scanner that throws costs
  only its own line. Trimmable.
- **`Blaizio.Syntax.Languages`**, a new package: Python, Java, Go, Rust, C, C++, Kotlin, Swift,
  PHP, Ruby, Dart, Lua, Scala, F#, VB.NET, Haskell, Elixir, Clojure, GraphQL, Protocol Buffers,
  HCL (Terraform), Bicep, Makefile, nginx, CMake, batch files, R, Perl, Julia, Objective-C and
  LaTeX, each a `SyntaxGrammar` exposed as its own property (`SyntaxLanguages.Python`), so a
  trimmed WebAssembly publish keeps only the languages the app adds. `options.Syntax.AddAll()`
  adds every one.
- **`Blaizio.Base`**: `BaseCopyButton`, a headless copy button that writes to the clipboard during
  the click itself (a C# handler can run after the click's activation expires, which Firefox and
  Safari refuse on Blazor Server) and exposes the outcome as `data-copy-state` plus a status
  announcement; `AnsiText`, which splits ANSI terminal output into styled runs per line;
  `KeepRendered` on `BaseCollapsibleContent` (and the styled `BzCollapsibleContent`), which keeps
  closed content in the render tree, hidden with `hidden="until-found"`, so the components inside
  keep their state and find-in-page opens the collapsible on a match.
- **Glyphs**: `BzGlyphs.CircleDashed`, `Copy`, `TextWrap` and `Pencil` (the data table's Edit
  button), mapped in all five icon sets.
- **Typeset**: a code block inside prose takes the same space above as a paragraph.
- **CLI packages**: `add` installs a Blaizio package an item needs (Blaizio.Syntax for
  `code-block`, an icon set) at the project's Blaizio.Base version rather than the newest, which
  would ask for a newer Base than the project has. `update` moves Blaizio.Syntax and
  Blaizio.Syntax.Languages with the base packages when the project references them, as it does the
  icon sets.
- **CLI `--json`**: `apply` reports `style` and `styleNotApplied`, `init`/`new` report them under
  `css`, and `add` reports `styleNotApplied`.
- **Registry templates**: the `{{Style}}` token substitutes the project's style.
- **`Blaizio.Cli.Core`**: `StyleInliner`, `TemplateTokens.Style`, and
  `ICssAssetProvider.GetSharedStyleCss()` / `GetStyleCss(string)` / `AvailableStyles`. The
  interface's style and skin members forward to each other by default, so an implementer
  overrides one set and existing implementations keep compiling.
- **`JsInstance`** (Blaizio.Base): owns one script-side instance created from a module factory.
  It releases an instance that lands after the component was disposed, and ignores calls made
  before the instance exists or after it is gone. For components that wire their own script.
- **Chart**: `AriaLabel` on `BzChart`, `BzPieChart`, `BzRadarChart` and `BzRadialBarChart` exposes
  the drawing to assistive tech as one labelled image. Without it the drawing stays hidden.
- **Static rendering**: `BzComponentBase` has `IsInteractive` and `IsStaticRender`, one guarded
  reading of the renderer's interactivity in place of the copies Avatar and Image each carried, and
  `RequiresInteractivity`. A component that does nothing without events (a trigger, a switch, a
  slider: 34 Base components) writes one warning per component type to the server log, category
  `Blaizio.StaticRender`, when it is rendered on a static page with no render mode. Only in the
  Development environment; set that category's log level to `None` to silence it.
- **Trimming**: `Blaizio.Base` is marked trimmable, so a published WebAssembly app carries only
  the components it uses. Measured on the assembly: 858 KB to 89 KB (222 KB to 29 KB with
  Brotli) for an app with a switch, a dialog and tabs. The docs site uses every component and
  goes from 861 KB to 801 KB. The package builds with no trim or AOT analyzer warning; it is not
  declared AOT-compatible, since no AOT publish is part of the checks. A project that trims its
  own assembly too (`TrimMode` `full`, off by default in Blazor) should keep the copied
  components whole, for example with `<TrimmerRootAssembly Include="YourApp" />`: Data Table and
  Drawer still hand script objects whose properties only the script reads. A row type edited
  through an `Editable` column needs its property setters kept in that case as well.
- **Field**: `For` on `BzField` names the model property as an expression
  (`For="@(() => model.Email)"`, or `() => model.Address.City` for a nested object). Inside an
  `EditForm` the field reads its messages for that property and tells the form's `EditContext`
  when the property's value changes, so validation follows the edit. With `Name` the messages
  refresh on submit only, as before. No control needs a parameter for it: the field reads the
  property itself each time the form renders, so any control that writes the value is followed,
  and an input that notifies the `EditContext` by itself is not announced twice. `For` wins over
  `Name`. A value written from code counts as a change; loading another model does not.
- **Direction**: a direction set in `AddBlaizio(o => o.Direction = ...)` is cascaded from the root
  of every component tree, so each separately rendered interactive component on a page receives
  it. A `BzDirectionProvider` in a statically rendered layout cannot give them that: a cascading
  parameter stops at a render-mode boundary. A provider still wins for its own subtree. With the
  option set, surfaces that move to the document body (popovers, menus, dialogs, tooltips) carry
  the direction in their own `dir` attribute and charts follow it; they used to follow a provider
  only. Nothing is cascaded when the option is left unset, and `dir` is never put on the
  document: the page's direction stays the app's to set.
- **Base**: `BaseHiddenInput` renders a control's value as hidden inputs under a form field name
  (one per value; nothing without a name, for a disabled control, or for no value), and the Base
  controls that own a value take `Name` and render it: Switch, Checkbox, Radio Group, Slider,
  Toggle Group, Input Tags, Input Number, Select, Combobox. `BaseInputOtp` puts `Name` on its own
  input.
- **Form posts**: `Name` on the controls that keep their value in C#, so they take part in a
  plain `<form method="post">` on a statically rendered page, where they used to post nothing:
  `BzSwitch` and `BzCheckbox` (`true` while on, nothing while off, like a native checkbox),
  `BzRadioGroup`, `BzSelect`, `BzCombobox` and `BzToggleGroup` (the value, or one input per value),
  `BzSlider` (one per thumb), `BzInputTags` (one per tag), `BzInputNumber`, `BzInputDate`,
  `BzInputTime`, `BzInputOtp` and `BzInputColor`. Numbers, dates and times are written as the
  matching native input writes them (`1234.5`, `2026-11-19`, `14:30`), whatever the culture or
  display format. Input OTP, Input Color and the `Auto` face of Input Time are native inputs
  already and simply take the name; the others render a hidden input. Without `Name` the markup
  is what it was. A `name` attribute written on one of these controls is now this parameter: it
  used to land on the control's own element, where it posted nothing.

### Deprecated
- **CLI `--json`**: the `skin` and `skinNotApplied` keys still appear next to their new names with
  the same values, and will be removed in a later release.
- **Registry templates**: `{{Skin}}` still substitutes (same value as `{{Style}}`).
- **`Blaizio.Cli.Core`**: `SkinInliner` (forwards to a wrapped `StyleInliner`),
  `TemplateTokens.Skin`, and `ICssAssetProvider.GetSharedSkinCss()` / `GetSkinCss(string)` /
  `AvailableSkins` are `[Obsolete]` and forward to the style-named members. The positional
  `TemplateTokens` parameter is now named `Style`, so a call passing it by name (`Skin: ...`)
  needs the new name.
- **Combobox**: `BaseComboboxItemIndicator.KeepMounted` is `[Obsolete]` and forwards to the new
  `KeepRendered`.
- **Dialog, Alert Dialog, Sheet, Drawer**: `DismissOnOutsideClick` on the roots (`BaseDialog`,
  `BzDialog`, `BzAlertDialog`, `BzSheet`, `BzDrawer`) is `[Obsolete]`. Use `PreventDismiss`, which
  says the opposite: `DismissOnOutsideClick="false"` becomes `PreventDismiss="true"`. The old
  parameter still locks, and with both set the surface is locked if either one asks for it.
- **Focus scope**: `BaseFocusScope.OnMountAutoFocus` and `OnUnmountAutoFocus` are `[Obsolete]` and
  forward to `OnOpenAutoFocus` and `OnCloseAutoFocus`.

### Fixed
- **Base**: a table of contents with more sections than its box can show keeps the active section
  in view. The spy marked the link but never scrolled the box it sits in (a sticky aside with
  `overflow-y-auto`, or the floating panel's own max height), so far down a long page the
  highlight sat below the fold. The first and last sections pin the box to its ends.
- **Input Time** (`blaizio update input-time`): a click on the 12-hour face's AM/PM toggle flips
  it. The toggle handed its click handler to the button under a name the button does not take, so
  only the arrow keys and the A and P keys switched it.
- **Base**: a tree node moved by a pointer drop is announced by its text ("Moved Reports."), as a
  keyboard move is, not by its value.
- **Base**: a number field removed right after it rendered (a data table cell editor committed by
  a fast typist) no longer throws "Cannot read properties of null" from its input guard, which
  surfaced as an unhandled rendering error.
- **Data Table** (`blaizio update table`): a change to a child column's parameters shows in the
  render that made it. The table laid out its columns before they received their new values, so a
  column given another alignment or class, or switched `Sortable`, kept its old cells and row order
  until something else rendered the table. A column now asks the table for one more render when
  what the table reads from it changes, and the rows are filtered and sorted again when its key,
  `Sortable` or `Format` changed. A `DefaultSearch` that only the last `BzPropertyColumn` matches,
  or a `DefaultSort` on that column, now applies from the first render; it showed "No results." or
  the rows in their original order.
- **Input Date** and **Input Time** (`blaizio update input-date input-time`): a day, month, year,
  hour or minute takes all its digits when typed. The first digit showed padded ("1" as "01" or
  "0001"), which filled the part and refused the next one. The calendar button and the AM/PM
  button no longer act as a form's submit button either: Enter in another field of the form opened
  the calendar (or flipped AM and PM) before the form submitted.
- **Base**: a calendar day's accessible name uses the digits the cell shows, so `NativeDigits`
  cultures no longer pair a visible `٣١` with a spoken `31`. A day with `DayContent` drops its
  `aria-label` and names itself from the template's text plus a visually hidden date, so a price
  or note in the cell now reaches assistive tech.
- **Ui**: the color picker's saved-colors add button now has an accessible name that contains its
  visible text. `BzColorSaved.SaveAriaLabel` defaults to `AddLabel` plus "current color" ("Add
  current color", was "Save current color"), so a custom `AddLabel` carries through.
- **Base**: dragging a sortable item into another list can drop it after that list's last item
  every time. The opened gap pushes the last item past the list's edge, and the pointer down there
  used to count as back over the source list, so the item sprang home. Lists with spare
  `min-height` hid this until their first drop filled the space. While hovered, the receiving list
  now grows by the incoming item's size, so the gap it opens shows inside the list at either end
  instead of the end slot sitting invisibly past its border.
- **Ui**: the destructive, success, warning and info `BzButton` variants, the destructive and info
  `BzBadge` and the destructive `BzBubble` clear WCAG AA in dark mode in every theme, on
  `--background` and `--card`, at rest and on hover. Their labels used the plain token over a tint
  of that same color. The destructive Button and Bubble measured as low as 3.86:1 at rest and 3.25:1
  on hover, and missed 4.5:1 on hover in all 16 themes; success, warning and info Buttons missed it
  on hover in 12 or 13 themes, down to 3.58:1; the destructive Badge measured 3.86:1 on a card, and
  the info Badge 4.33:1 on umbra's card. The dark label now has an `oklch` lightness floor that
  keeps the token's hue and chroma, set only as far as each state needs: 0.8 on hover (0.82 on
  warning) and 0.74 at rest on destructive and info. Success and warning keep the plain token at
  rest. The fills, the theme tokens and light mode are unchanged. The ash Badge has no fill and
  keeps the plain token.
- **Ui**: in the ash style, a status `BzBadge` rendered as a link no longer fades on hover. Fading
  the label to 70% measured as low as 2.80:1 in light and 3.22:1 in dark. All four status labels
  now move away from the surface, darker in light and lighter in dark, so hover raises their
  contrast and keeps their hue. The accent Badge still fades, to 85% instead of 70%, which
  measured 3.65:1 in light. The default and outline Badges keep their fade.
- **Ui**: primary-filled controls keep their label readable on hover: the default `BzButton`, a
  default `BzBadge` rendered as a link, an interactive default `BzBubble`, and the toast action.
  The hover fill faded toward the surface, which measured as low as 3.58:1 in light on 11 themes
  and 3.50:1 in dark on 7. It now moves away from the label, toward black or white by the label's
  lightness, so hover raises the contrast in every theme. On a white label the fill now darkens on
  hover instead of lightening.
- **Ui**: the Link variants of `BzButton` and `BzBadge` clear WCAG AA in every theme. In light the
  text lightness is capped at `oklch` L 0.55, which moves only Solstice, from 2.35:1 to 4.80:1. In
  dark it has a floor at 0.65, which moves the seven themes under it and lifts the floor on
  `--card` from 4.23:1 to 4.95:1. The ash Link Badge uses `--foreground` and is unchanged.
- **Ui**: installed status components keep their text size and color. TailwindMerge.NET 1.4.0
  reads the percentage in `text-[color-mix(...)]` as a font size, so on that version the
  destructive, success, warning and info variants of `BzButton`, `BzBadge` and `BzAlert` lost their
  text size (a default Button rendered at 16px instead of 14px, a Badge at 16px instead of 12px). A
  Button size that sets its own text size lost the light mode color instead: Xs in every style but
  ash, Sm in spark, and all text sizes in flint. On 1.4.0, a `Class` color now replaces the
  variant's color, a `[&_[data-token=...]]` color on `BzCodeBlockContent` replaces that token's
  color, and an added `BzCodeBlockLine` shows its green marker instead of the muted one. The sheets
  now write `text-[color:color-mix(...)]`, which compiles to the same CSS and reads as a color on
  1.4.0 and 1.5.0 alike, so a project keeps the fix before it moves to 1.5.0.

## 0.3.5 - 2026-10-21

Sortable items leave the keys typed into their inputs and other controls alone, the theme toggle and
the sidebar trigger and rail take translatable labels, and fixes land for dark-mode contrast, RTL,
the navigation menu, the mobile sidebar and the CLI's scoped apply.

### Added
- **Label parameters**: `BzThemeToggle.Label`, `BzSidebarTrigger.Label` and `BzSidebarRail.Label`
  replace their hardcoded English text (the rail uses it for both `aria-label` and the hover
  `title`), so translated apps no longer show or announce "Toggle theme" / "Toggle Sidebar".
  Defaults are unchanged.

### Changed
- **Dependencies**: `Spectre.Console` 0.57.3-alpha.0.19 (was alpha.0.18) in the CLI; the test
  stack moves to bunit 2.11.3, AngleSharp 1.8.2, Test.Sdk 18.10.1 and ImageSharp 4.1.2.
- **Docs**: the landing page says only what the CLI does. The steps name the real NuGet packages,
  the real first-add prompts (namespace, output folder, skin, palette, RTL) and `blaizio update`
  for upstream fixes; step 3 is `apply comet --only theme`, and a skin switch is described as the
  full apply it is. The theme picker splits "New project" (`new --preset <code>`) from "Existing
  project" (`apply <code>`, which re-installs the components and asks first), and Reset restores
  the whole default look. The hero terminal replays a real first add once (prompts, paths, 51
  files), rests on its final frame, shows that frame at once under reduced motion and wraps on
  phones; the two-layers demo opens a real `BzDialog`; the component marquee is gone; Calendar and
  Input OTP fit their tiles. Every copy control on the site (code blocks, demo code, command chips,
  chart sheets, the icon browser, the Themes code chip, typeset.css and community theme CSS) now
  reports a refused clipboard write instead of a false "Copied", and announces the outcome to
  screen readers. `index.html` gains a
  static title, a noscript summary and JSON-LD, known routes answer 200 on GitHub Pages with a
  `sitemap.xml` and `robots.txt` beside them, the header shows the star count on phones, Examples
  leaves the nav and search until it has content, and the link-preview card is redrawn.
- **Docs**: phones. The menu sheet's site links sit on the same guide rail, with the same sliding
  indicator, as the docs groups below them, and leave out Docs and Components, which Getting
  Started already lists (one page lit two rows). The menu button has an accessible name and
  reports whether the menu is open. Local builds show the GitHub star count too (the public API,
  asked at most twice a day; CI and the E2E suite skip it). On the landing page the three steps
  stack their number over the command, so commands no longer break mid-word, and a flag stays on
  the line with its value; the hero badge, the footer links and the palette names fit a 320px
  screen; the hero terminal sets 12px type (it stood taller than the screen); the headline's
  shimmer sweeps once; touch screens get a touch hint instead of the keyboard ones; 11px labels
  move up to 12px.

### Fixed
- **Base**: keys typed into a control inside a sortable item stay that control's. The item's key
  handler also caught keys bubbling up from its content, so in an input or combobox inside a
  `BaseSortableItem` Space grabbed the item (leaving it highlighted as grabbed) instead of typing a
  space, Enter grabbed it too, the arrows, Home and End moved focus off the control (closing its
  menu), and `sortable.js` cancelled Space, the arrows, Home and End before the control could use
  them. Only the script sees a key's target, so it now decides: a key pressed on the item itself or
  on its `BaseSortableHandle` is forwarded to the new `[JSInvokable] BaseSortable.NotifyItemKeyDown`,
  and any other key is left alone. Items no longer carry a Blazor key handler; each renders a
  `data-sortable-item-id` the script forwards by. A nested list's item keys no longer reach the outer
  list either. Grabbing, moving, dropping and cancelling from the item work as before, so app-side
  workarounds that stop key propagation inside sortable items can go.
- **Base**: hovering a submenu-less top-level `BzNavigationMenuLink` now closes the panel another
  item had open (it scheduled no close before, so the open submenu lingered). Links inside a content
  panel still keep it open on hover: the content cascades itself so a link can tell the two apart.
- **Ui**: `BzResizableHandle`'s hover/drag hit line is centred on the hairline under RTL again. The
  line was positioned with a logical `start-1/2` but a physical `-translate-x-1/2`, so in RTL (where
  `start` is the right edge) it landed a few px to the left of the handle; the centring translate now
  flips for the vertical handle in RTL.
- **Ui**: `BzSkeleton`'s shimmer sweep is softer in dark mode. The highlight (`currentColor` at 15%)
  was tuned for light surfaces and read too hot across a large dark block; dark mode now mixes it at
  8%. Light mode, the pulse animation and small blocks are unchanged.
- **Ui**: dark-mode contrast on two primary fills that still used plain `bg-primary`:
  `BzCalendar`'s selected day and range ends, and `BzSlider`'s value tooltip (with its arrow).
  Both now take the 85%-primary-over-black fill the default button already uses. Under the old fill
  the day number measured as low as 1.2:1 on some palettes (aurora, corona, eclipse, equinox) and
  under 4.5:1 on most others; every skin and palette now clears WCAG AA in light and dark.
- **Ui**: `BzSidebar`'s mobile sheet is as wide as `BzSidebarProvider.WidthMobile` says (18rem by
  default) again. The open sheet portals to the document body, out from under the provider
  element that declares the width variables, so it shrank to its content (222px on a 375px phone).
  `SidebarContext` now carries `WidthMobile`, and the sheet declares `--sidebar-width-mobile` itself;
  inside the sheet `--sidebar-width` takes the mobile width too.
- **Ui**: a pressed `BzToggle` / `BzToggleGroupItem` now takes an accent fill with
  `accent-foreground` text instead of `bg-muted`. On a dark palette the muted fill sat too close to
  the surface, so an on item read as not pressed, and it also matched the hover state; the accent
  pair is distinct in light and dark and from hover. All eight skins.
- **Ui**: `BzAlertDescription` only spaces its own paragraphs now. Its paragraph-spacing rule was a
  descendant selector, so it also pushed apart `<p>`s rendered by nested components: two
  `BzItemDescription`s in a `BzItem` inside an alert showed a 16px gap instead of the item's 4px.
  It is now a child combinator, matching the accordion and alert-link rules.
- **CLI**: `apply <code> --only theme` no longer records the code's skin in `blaizio.json`. A
  scoped apply re-installs no components, so the record named a skin the files did not wear: the
  next `add` brought components in that skin next to ones in the old skin, and `preset resolve`
  printed a code for a look the project did not have. Every scoped apply now keeps the recorded
  skin, and when the code names another one it says so and points at the full
  `blaizio apply <code>`, which switches skin by re-installing the components. `--json` reports
  the skipped skin as `skinNotApplied`.
- **CLI**: `add --style <skin>` on a project with installed components no longer re-records the
  skin. The components wear the recorded one and only a full apply re-installs them, so the record
  used to name a skin nothing on disk had: the next `add` pulled components in the new skin next to
  the old ones. The run now keeps the recorded skin and warns with the full `blaizio apply <code>`
  (a code for the asked-for look) that switches by re-installing; `--json` reports it as
  `skinNotApplied`. A project whose installed items copied no files (fonts) still takes the new
  skin, and an unknown skin name keeps the recorded one instead of falling back to ember.
- **CLI**: `add --preset <x> --style <skin>` on an initialized project switches to that skin in the
  preset's own apply (the style overrides a code's skin, and turns a palette name into a code).
  It used to re-install everything in the code's skin and drop the style. Wiring flags next to
  `--preset` (`--rtl`, `--pointer`, `--scrollbar`, `--tailwind`, `-f`) are no longer dropped when
  the run names no components.
- **CLI**: a full `apply` records the new skin as soon as the components are re-installed in it.
  It used to record the skin only with the tokens patch, so a project without a tokens file kept
  the old skin on record over components wearing the new one.
- **CLI**: the showcase template that `blaizio new showcase` scaffolds no longer uses em dashes in
  its page copy (dashboard, forms demo, overlays).
- **Docs**: the CLI README, the repository README and the docs Introduction no longer mention
  `add --update`, which was removed in 0.1.0-alpha.10. Upstream changes come through
  `blaizio update`, which the CLI README's command table now lists: it pins the Blaizio packages
  (and TailwindMerge.NET) to the tool's versions and re-pulls the installed components, keeping
  the files you changed on unattended runs unless you pass `--force`.

## 0.3.4 - 2026-09-16

The command palette's mouse highlight no longer outlives the pointer, and the packages move to
ASP.NET Core 10.0.12.

### Fixed
- **Command**: the mouse no longer keeps the highlight after it leaves the list. Hovering a row
  still lights it, but the highlight returns to the row it came from (the arrow-key row, the first
  match, or the last clicked row) once the pointer leaves, so a selection the host draws on top
  no longer reads as a second highlighted row. Enter keeps working on that row.

### Changed
- **Dependencies**: the packages now build against `Microsoft.AspNetCore.Components.Web`
  10.0.12 (was 10.0.11), so a consumer pinned lower fails restore with `NU1605` until its
  `Microsoft.AspNetCore.Components.*` references move to 10.0.12; `blaizio new` scaffolds
  WebAssembly 10.0.12. Also `Net.Codecrete.QrCodeGenerator` 3.2.1, `Spectre.Console`
  0.57.3-alpha.0.18, and the test stack (bunit 2.10.3, AngleSharp 1.8.1, Test.Sdk 18.10.0).

## 0.3.3 - 2026-09-10

Dry-run previews now count only the files a real update would write, and revisioned local packs
no longer share build output with release packs.

### Fixed
- **Build**: a revisioned pack (the docs' local feed) now builds under its own `bin/rev` and
  `obj/rev`. It used to share `obj/Release` with the plain pack, so a docs-server build between
  two release packs left a four-part `Blaizio.Icons.dll` there and the next `Blaizio.Icons.Tabler`
  pack compiled against it: a package calling itself 0.3.2 that referenced `Blaizio.Icons
  0.3.2.80`, failing at runtime with "Could not load file or assembly". Only local packs were
  affected; the 0.3.2 packages on nuget.org are clean.
- **CLI**: `--dry-run` reports each file with the outcome the real run would give it. A file
  kept for your edits (or left alone by a non-overwriting `add`) previews as skipped and a file
  already at upstream as unchanged; only a file that would actually be written counts as
  planned. The "would re-pull N file(s) changed" count previously included the kept and
  unchanged files, so a preview overstated what `update -y` then did.

## 0.3.2 - 2026-09-10

Dialog first-open stutter fixed at the source, `update` decides keep-or-take per file, the
showcase template compiles again, and the table's horizontal scroll box gets the thin scrollbar.

### Changed
- **CLI**: `update` (and `add --overwrite`) decide keep-or-take per FILE, not per component.
  The interactive picker lists every changed file under its component; unticked files keep
  your version, ticked ones take upstream, so a component can keep the file you extended and
  still drop the one you only patched. `-y` still keeps everything and `--force` still takes
  everything. The summary lists the kept files under their component and says which files of
  the same component took upstream; `--json` carries `updated.decisions`, one entry per file
  with `item`, `path`, `kind` and `kept`. `AddRequest.ResolveConflicts` now returns file paths
  instead of item names.

### Fixed
- **CLI**: the showcase template's three submit buttons (forms demo, login, register) passed
  `Type="submit"` to `BzButton`, whose `Type` parameter is the `ButtonType` enum, so a fresh
  showcase did not compile. They pass `ButtonType.Submit` now (#27).
- **Dialog, Sheet, Drawer, AlertDialog**: the first open no longer stutters. The surface rendered
  at its declaration site, started its entry animation there, and moved to `<body>` one interop
  roundtrip later, which restarts CSS animations - so the entry played twice, visibly on a cold
  Blazor Server circuit. The window and overlay now carry `data-bz-portal` and `blaizio.css`
  keeps them invisible until they have arrived at body, so the one entry animation that shows is
  the one that starts there. Inline surfaces are untouched. (Blaizio.Base + the `blaizio.css`
  contract sheet.)
- **Table**: the horizontal scroll container now ships `scrollbar-thin` like every other Blaizio
  scroll box, so a project that opted into the thin scrollbar gets it on a wide table too. Cells
  still keep `whitespace-nowrap` by design; pass `whitespace-normal` through `CellClass` (or a
  cell's `Class`) on the column that should wrap.

## 0.3.1 - 2026-09-08

One CLI fix for `--icons` with items that draw Tabler directly; no other change.

### Fixed
- **CLI**: on a project that picked another set with `--icons`, an item whose files draw Tabler
  by name (a third-party item, say) kept its `Blaizio.Icons.Tabler` dependency in the summary but
  had it swapped for the chosen set's package at install, so the copied files did not compile.
  The swap now leaves Tabler's package in place whenever something being installed names
  `Tabler.*` in code (comments do not count); the glyph file, which is retargeted, never does.

## 0.3.0 - 2026-09-08

The icon set the components draw from is now a choice: `--icons <set>` on `new`, `add` and
`apply`, backed by a glyph map (`BzGlyphs.cs`) that replaces the Tabler names inside the components.

### Changed
- **Ui**: the styled components no longer name an icon set. The 38 glyphs they draw with (the
  check in a select item, the chevron on a trigger, the grip on a sortable row, the spinner...)
  resolve through `BzGlyphs`, a new file in the `utils` item whose members default to their
  Tabler icons and keep the Tabler names. Point a member at another set's icon and every
  component follows; the file is yours after install, like the tokens file. `utils` now declares
  `Blaizio.Icons` and `Blaizio.Icons.Tabler` (it always shipped inside a project that had them).
  Groundwork for choosing the components' set at install time; Tabler stays the default.

### Added
- **CLI**: `--icons <set>` on `new`, `add` and `apply` picks the icon set the styled components
  draw from (`tabler`, `lucide`, `phosphor`, `remix`, `hugeicons`). On a new or first-wired
  project that set's package replaces `Blaizio.Icons.Tabler` and `BzGlyphs.cs` lands retargeted;
  on a wired project `apply --icons <set>` (alone, no preset) rewrites the file's members and
  installs the package, `--icons tabler` goes back. `blaizio.json` records it as `icons`;
  `update` re-pulls the glyph file against the record (no false "local edit") and bumps the
  recorded set's package instead of reintroducing Tabler's. A Themes code's `.N` icons segment
  now means the same thing. The per-set glyph table lives in `Blaizio.Cli.Contracts`
  (`GlyphCatalog`, 38 rows, held to the generated icon classes by a test). The CLI never removes
  a set's package: after a switch the summary lists what stays referenced.

### Fixed
- **CLI**: `blaizio apply <code> --only icons --dry-run` now reports the icon set it would install
  ("Would apply icon set lucide (Blaizio.Icons.Lucide)", `icons: true` in `--json`) instead of
  staying silent; the csproj is still left alone.

## 0.2.1 - 2026-09-06

Licence fix for the Remix icon set; no other change.

### Changed
- **Icons**: `Blaizio.Icons.Remix` is generated from the Remix Icon v4.8.0 release, the last one
  published under Apache-2.0; from v4.9.0 the set carries the Remix Icon License v1.0, which does
  not allow shipping it as a standalone package. 41 icons added in 4.9.x are gone (mostly vendor
  logos: GrokAi, MicrosoftCopilot, QwenAi, ZhipuAi, Gitbook, Reddit2, Subreddit, plus
  Certificate, Cookie, Draw, Mosaic, Connector, QrScanAi and a few more), and six shapes revert
  to their 4.8.0 drawing. The generator pins that tag; `THIRD-PARTY-LICENSE.txt` records the
  pin and the SVG-to-C# transform. `Blaizio.Icons.Remix` 0.2.0, generated from a 4.9.x checkout,
  is unlisted on nuget.org for the same reason.

## 0.2.0 - 2026-09-05

Five icon sets as packages, a Themes dock that knows about them, and the build moved to Central
Package Management. Breaking for every `Icons.Outline.*` reference: Tabler now lives in
`Blaizio.Icons.Tabler` as `Tabler.Outline.*`, and `Blaizio.Icons` is the component only.

### Added
- **Icons** (breaking): `Blaizio.Icons` is now the component only - `BzIcon` and the `Icon`
  value. Every set is a package on top of it, versioned with it. Tabler moves to
  `Blaizio.Icons.Tabler` as `Tabler.Outline.*` / `Tabler.Filled.*` (was `Icons.Outline.*` /
  `Icons.Filled.*` inside `Blaizio.Icons`); the CLI installs it with every project since the
  styled components draw from it, and `blaizio update` adds it to a project that predates the
  split. The other four: `Blaizio.Icons.Lucide` (`Lucide.Outline.*`, ISC), `Blaizio.Icons.Phosphor`
  (`Phosphor.Thin/Light/Regular/Bold/Fill/Duotone.*`, MIT), `Blaizio.Icons.Remix`
  (`Remix.Line.*`, `Remix.Fill.*`, Apache-2.0) and `Blaizio.Icons.HugeIcons`
  (`HugeIcons.StrokeRounded.*`, the free set, MIT). Every member is the same trim-friendly typed
  `Icon` value, so the sets mix in one app and a WebAssembly publish keeps only the icons it
  references. Each package ships the set's licence as `THIRD-PARTY-LICENSE.txt`.
  `scripts/Update-BlaizioIcons.ps1` generates all five sets (`-Set` picks one).
- **Icons**: `Icon` carries its grid and stroke width (`ViewBox`, `StrokeWidth`), which is what
  lets one `BzIcon` render Phosphor's 256 grid and Hugeicons' 1.5 stroke at their true weight.
  `BzIcon.ViewBox` and `BzIcon.StrokeWidth` are now nullable overrides: unset, the icon's own
  values apply. The Tabler set is refreshed (5,130 outline, 1,054 filled).
- **CLI**: `blaizio update` moves any referenced `Blaizio.Icons.*` set in lockstep with the base
  packages; `init` installs `Blaizio.Icons` and `Blaizio.Icons.Tabler`, never the other sets.
- **Docs**: the Icons page browses every icon of every set (set, family and name filter; click
  to copy the member), fed by build-generated JSON so the site's own trimming is untouched. The
  themes canvas gains a third page - an icons-only card, pricing, inbox, activity and settings
  previews - and the dock an **Icons** knob (lockable, hover-previewed, shuffled) that the card
  follows.
- **Preset code** (v4): the icon set rides as a `.N` segment (`32r.2` = Phosphor), only when it
  is not Tabler, so every existing code still means what it meant. `blaizio init --preset`,
  `blaizio new --preset` and `blaizio apply` install the named set's package on top of Tabler
  (`apply --only icons` for that leg alone) and record it as `icons` in blaizio.json;
  `preset current` and `preset decode` report it. `IconSetCatalog` in Blaizio.Cli.Contracts is
  the append-only list behind the knob, the digit and the docs browser.
- **Docs**: the Demo shell's Inspect probe covers floating surfaces - dialogs, menus, selects
  and tooltips a demo opens are traced back to it and their parts outlined - and leaves
  interaction live so those surfaces can be opened while inspecting; Escape leaves Inspect. Its
  slot map is generated at build from the component sources, so every `data-slot` resolves to
  the component that emits it. The accessibility x-ray probe is removed.
- **Docs**: Cloudflare Web Analytics (cookieless) on the published site, injected by the Pages
  workflow from a repository variable; local runs never report.

## 0.1.0 - 2026-09-04

The first stable release: every package drops its `-alpha.N` suffix and publishes to nuget.org,
the docs site and its component registry go live at https://blaiz.io, and the repository moves
to https://github.com/blaizio/blaizio. The `0.1.0-alpha.*` sections below are the pre-release
history that led here; everything in them is part of this release.

### Changed
- **Packages**: `Blaizio.Base`, `Blaizio.Icons`, `Blaizio.Cli`, `Blaizio.Cli.Core` and
  `Blaizio.Cli.Contracts` are all `0.1.0`. The CLI installs the base packages pinned to its own
  version, and the unpinned path (`dotnet add package` without a version) now resolves the latest
  stable release rather than the latest prerelease.
- **CLI**: the "the public blaiz.io registry is not live yet" hint after a registry error is
  gone; the registry is live, so an unreachable default registry is reported like any other.
- **Base**: the package no longer ships source maps for its interop modules. The maps embedded
  the TypeScript sources and outweighed the bundles three to one; every consumer site served
  them to anyone opening DevTools. Maps are a watch-mode build feature now.
- **Docs**: the site boots on a plain static host (GitHub Pages). `index.html` carries an
  import map, filled at publish with the fingerprinted `_framework` names, so the runtime's
  `./dotnet.js` import resolves without ASP.NET's static-asset middleware.

### Changed
- **Repository**: prepared for the move to GitHub (`github.com/blaizio/blaizio`). README,
  CONTRIBUTING and the CLI README no longer reference the retired `/create` route, the stale
  `alpha.5` tool install or a "three suites" test layout; CONTRIBUTING gains the Node/pnpm
  prerequisites, the docs toolchain bootstrap, a licensing note and the release procedure;
  CODE_OF_CONDUCT and SECURITY name a real contact. New: `CODEOWNERS`, `dependabot.yml`, a
  tag-triggered `publish.yml` (NuGet trusted publishing), `docs/README.md` indexing the
  engineering notes, and a README for `Blaizio.Cli.Contracts`. Every package now carries
  `RepositoryUrl`, `PackageProjectUrl`, an icon and SourceLink. The stray `blaizio.json` at the
  repository root (a machine-specific path) is gone and ignored.
- **Docs**: index.html no longer carries inline CSS or JS. The boot splash styles moved to
  `Styles/boot.css` (imported by app.css, which is render-blocking so they are in place before
  the splash paints), and the pre-paint re-inject of persisted theme overrides is now
  `lib/ts/prepaint.ts`, bundled by esbuild to `js/prepaint.js` as a classic script and loaded
  from a fingerprinted tag next to boot.js. The storage keys it shares with docs.ts live in one
  `lib/ts/storageKeys.ts` module so the two cannot drift.
- **Docs**: the whole Node toolchain now lives in `docs/Blaizio.Docs/lib` - package.json, the
  pnpm lockfile and workspace file, node_modules, and the two scripts (`tools/make-pin-base.mjs`,
  `tools/validate-community.mjs`) - the same layout Blaizio.Base uses. `pnpm install` runs in
  `lib/`; the build targets, CI and the community workflow point there. `Styles/app.css` can no
  longer walk up to a `node_modules`, so its two package imports are spelled as paths into
  `lib/` (a real app keeps `@import "tailwindcss"`; the comment says so).

### Fixed
- **Docs**: two axe findings the CI gate caught on Linux. The sidebar's active row in dark used
  the raw `--primary` as 13px text on the sidebar surface; every palette tunes that color to
  clear AA on the page background, and the sidebar sits a step lighter, so Nova landed at 4.34:1.
  The active label is now lifted 15% toward white in dark (the mirror of the 15% deepen the
  button skins apply to the fill), 5.5:1 on Nova. The landing showcase's progress bar had no
  accessible name; it is labelled "Volume" like the slider it mirrors. The axe harness now
  records each offending node's markup and check data (colors, ratio, or the reason a background
  could not be resolved) in the CI log and the AxeResults artifact, so a contrast finding is
  actionable without a local re-run.
- **CLI**: a reference to an unrecorded `@namespace` no longer turns into "Cancelled." (exit 130)
  when the community directory host accepts the connection and never answers. The courtesy
  lookup only let non-cancellation errors through, but HttpClient reports its own timeout as a
  cancellation, so on such a network the lookup blocked for the client's full 30 s and then
  aborted the command. The fetch now has a 5 s leash of its own and only the caller's Ctrl+C
  propagates; the unknown-registry error (exit 2) is the answer as documented. The CLI test suite
  keeps `BLAIZIO_DIRECTORY` pointed at a missing file for the whole run, so no test reaches the
  network.
- **Build**: `dotnet build Blaizio.slnx` works on a fresh clone, and the docs project builds on
  Linux. A solution build restores the whole graph up front and never runs a project's
  `Restore` hook, so the docs project's pack / registry / component-copy steps ran too late:
  restore failed with NU1101 on an empty local feed, and even with the feed filled Razor had
  already collected its sources before `Components/Ui` existed. The three steps now also hook
  `CollectPackageReferences`, which the solution restore does call. Separately, every path the
  docs and Base projects hand to `Exec` was built with backslashes, which Linux passes to the
  shell verbatim (`src\Blaizio.Ui`: "Source not found"); they are forward slashes now. The docs
  scripts are bundled by `lib/build.mjs` through esbuild's JS API (the same shape as Base):
  `node node_modules/esbuild/bin/esbuild` only works on Windows, because esbuild's install step
  replaces that file with the native executable everywhere else.
- **Build**: the solution builds with zero compiler warnings again. A `paramref` naming a
  parameter that did not exist (CalendarSystem), an ambiguous `cref` to the `FocusAsync`
  overloads (ICore), nullable flow on registry item names in `build`, an
  `EventCallback<string>` handed to a `ControllableState<string?>` (BzColorPicker), an inferred
  `Person?` item type in the Combobox custom-items example, and three docs `cref`s that could not
  resolve.
- **Ui**: the Input Date calendar popup and the Input Color picker popup now carry an
  `aria-label` ("Calendar" / "Color picker"), so screen readers announce the dialog and the
  Popover's dev-mode "add a PopoverTitle or an aria-label" warning no longer fires for them.
- **Docs**: the boot splash no longer sits on a half-inked wordmark reading "100%". The fill
  and scanline followed `--blazor-load-percentage` through a 0.25s transition; Blazor sets 100%
  and then blocks the main thread for the runtime start, so the transition froze part-way until
  the app swapped in. The fill now tracks the variable exactly - with 200+ resources the steps
  are sub-1%, so nothing visible was lost.
- **Base**: first-open stutter on floating surfaces after a cold load. Dialogs, menus and
  popovers lazy-import their interop modules on first open; on a fresh page the import paid
  fetch + parse mid-open, so the entry animation ran while a popover was still hidden, and a
  dialog's late `portal.js` reparented it to `<body>` mid-animation (CSS animations restart on
  reinsertion). Two layers of warmup close the gap: `boot.js` now prefetches the
  presence / positioning / portal / dismissable-layer / focus-scope / scroll-lock / menu modules
  at idle time after page load (skipped on data-saver connections), so the browser's module map
  is warm before the first interaction - including for service-shown dialogs that mount whole on
  `ShowAsync`; and `BaseDialog` primes the shared `JsModules` cache on first render, the same
  warmup `BasePopover` / `BaseDropdownMenu` / `BaseSelect` already do - the dialog root was the
  one floating surface that warmed nothing.

### Added
- **Ui**: `BzInputColor`, an inline color field - the color sibling of `BzInputDate`. A swatch
  dot leads, a monospace text field shows the color in `Format` (any of the picker's ten) and
  accepts any parseable color on blur or Enter, and `ShowPicker` (on by default) adds a trailing
  button that opens `BzColorPicker` in a popup; dragging there writes straight into the field.
  `ShowAlpha`, `Swatches`, `ShowEyeDropper` and `ShowFormatPicker` flow through to the popup;
  `@bind-Format` follows its dropdown. Text that does not parse stays in the field flagged
  invalid; clearing it clears the value. `AutoFocus` and `FocusAsync` as on the other inputs.
  Registry item `input-color`, docs page at /docs/components/input-color.
- **Docs**: a landing page at "/". Hero with a replaying init + add terminal, a strip of live
  components, the two-layer story with styled-vs-Base code tabs, three pillars, a skin and
  palette picker that restyles the page and prints the preset code, the three-command
  walkthrough and a closing call to action, all on the design tokens so it re-skins with the
  site. Sections reveal on scroll. The
  Introduction now lives only at /docs; the header no longer lights "Docs" on the home route.
- **Cli**: `update` reaches NuGet-only projects. A project that references the Blaizio packages
  without a `blaizio.json` (a class library using only `Blaizio.Base`, say) was invisible to the
  solution-root fan-out and refused in-place with "run blaizio add first" - so a solution update
  left it on an older `Blaizio.Base` than its siblings. `update` now discovers such projects too
  and runs only the package leg there: the ids the csproj already references are pinned to the
  tool's versions, nothing is introduced, nothing ledgered. `--json` marks the run
  `packagesOnly`. Other commands are unchanged - a csproj alone is still not a Blaizio project.
- **DropdownMenu**: `RestoreFocusOnPointerDismiss` on the content (default `true`). The
  outside-click focus restore is stranded-only and invisible to pointer users, and it keeps the
  keyboard's Tab position at the menu - so it stays the default - but a host that reacts to any
  focus landing inside it (an editor lighting up on focus-in) can now turn it off: a click-away
  then leaves focus where the click put it, while Escape and item selection still restore the
  trigger. Lives on the shared menu surface base, so every root surface accepts it; submenus and
  menubar menus, already stranded-only on pointer dismissal, ignore it. A docs demo shows the
  two side by side.
- **Toolbar**: contextual controls escape the clip. `Reveal` on `BzToolbarButton` / `Group` /
  `Link` / `Input` says what the control does when it would sit on a clipped row of an `Expand`
  bar: `Pin` orders it first (pure CSS), so it lands on the visible row and pushes an older
  control down; `Expand` holds the bar open while the control renders below the first row - the
  scroller publishes `data-reveal-open` and the clip lifts in CSS, a hold rather than a state
  change, so `@bind-Expanded` stays honest and the bar settles back when the control leaves.
  The observer watches the subtree now, so a control appearing inside a group counts too, and
  the first-row measurement keys off the topmost control rather than DOM order, so pinning
  cannot confuse it. Both new utilities verified against the pinned standalone Tailwind 4.1.11.
- **Toolbar**: the `Expand` state is a parameter. `Expanded` / `DefaultExpanded` /
  `ExpandedChanged` follow the Toggle's controlled-or-not pattern, so `@bind-Expanded` opens the
  bar from code and the bar's own toggle keeps the bound field in step. Other overflow modes
  ignore it. The overflow demo drives it from a button outside the bar.
- **Cli**: every project command runs from a solution root. In a folder with no `blaizio.json`
  and no `.csproj`, the CLI looks for the Blaizio projects underneath (build output, caches and
  VCS folders skipped). One found: the command runs there with a `project` line. Several: a list
  with every project checked, `space` / `a` / `enter`, and the command runs once per selection
  under its own header with a tick-or-cross summary and the worst exit code; one project failing
  does not stop the rest. `-y` (or no terminal, as in CI) takes every project; `--json` refuses
  several with the `-c` hint, since two JSON documents on one stdout is not JSON. `search`,
  `view` and `docs` pick one project rather than repeat themselves. No solution-level
  configuration: each project keeps its own `blaizio.json`, registry, skin and ledger, and the
  fan-out is exactly the command run in each folder in turn. The case that prompted it: a repo
  with two projects where `update` ran in one and the other silently stayed eleven releases behind.
- **Docs**: a registry page, Author a component (`docs/registry/author-a-component`). One
  worked example, a star rating, from the first line to `blaizio add @acme/rating`: where to write
  it (a wired project, a folder per item), the namespace rule and why it is the one prefix that
  gets rewritten, the anatomy with what each convention buys the consumer (tokens not colors,
  `Class` merged last, unmatched attributes on the root, `data-slot`, icons from the package, XML
  docs), the generate / validate / build commands with the real output, installing from the built
  folder into a second project, and a pre-publish checklist. Getting Started links to it from the
  section that used to be the only guidance. Every claim on the page was run before it was written.
- **Cli**: `generate` works for a third-party tree, not only the official one. Fonts - the fifty
  two items that mirror the CLI's font catalog - now come only with `--fonts` (the official
  callers pass it); a tree without root helpers gets `@default/utils` as its dependency instead of
  a `utils` item that does not exist, so the manifest validates; and `Bz*` types referenced from
  code but defined by no folder in the tree are named at the end of the run with the shape of the
  fix (`@default/<item>`), instead of being dropped silently. The source argument defaults to the
  current directory, and the help no longer calls the command a maintainer tool.
- **Cli**: `init` registers the services. `builder.Services.AddBlaizio()` lands in `Program.cs`
  just above the line that builds the host - the one registration every Blaizio app needs and the
  one nothing on the install path ever wrote: a project without it compiles and runs until the
  first component that injects `ICore` or the dialog and toast services renders, and then fails
  with "No registered service of type 'Blaizio.ICore'" and no hint why. Idempotent; a call the app
  wrote itself, with or without the options lambda, counts and is never touched. `update` warns
  when the call is missing rather than adding it (once wired, `Program.cs` is the app's, the same
  rule as the host page), and `uninstall` strips exactly the line the CLI wrote, by its marker
  comment. `--json` reports it as `services` on `init` and `servicesRegistered` on `update`.
- **Cli**: the install record carries its source. An item added from a file, a URL or an
  `owner/repo/item` address is recorded with that reference (`source` in `blaizio.json`), and
  `update` re-pulls it from there. Every plain key used to be taken for a name on the default
  registry, so a direct install could never be updated: the re-pull asked a registry that had
  never heard of it and failed with a bare "file not found" under its skin folder. Records written
  before this keep working as they did; `update` names the ones it cannot re-pull and says how to
  give them a source. `add --all --prune` leaves sourced records alone, as it already did
  namespaced ones - they were never the default registry's to sweep.
- **Cli**: `update` re-pulls what it can. One ledger entry the registry could not serve used to
  abort the run before any component was refreshed; a whole-ledger run now skips it, re-pulls the
  rest, and reports each skipped entry at the end with what to do (`--json` carries them under
  `skipped`). `update <name>` with an explicit argument still fails on a miss - that is a typo to
  fix, not a ledger to route around.
- **Base, Ui**: `FollowMargin` on the message scroller - the block of pixels above the end of
  content that still counts as "at the end" (48 by default). Inside it the transcript follows the
  stream and the end button is hidden; scroll up out of it and follow pauses, with the button
  appearing at the same moment; come back into it, or press the button, and follow resumes. It
  used to be a 2px tolerance for follow and a separate 48px one for the button, so the reader
  could be paused with no button offering the way back.
- **Base, Ui**: `ReleaseReservedSpaceAsync()` on the message scroller, for a reply that ended
  short of the bottom. The reservation exists to push the sent turn up; it is used up line by line
  as the reply grows and is gone by the time the reply reaches the bottom. A reply that stops
  early leaves some, and only the app knows the reply stopped (a quiet gap in a stream is not an
  ending), so it says so with this call. The engine also hands a reservation back on its own
  wherever that is safe or asked for: when the reader scrolls up out of the follow margin (the
  blank is below the fold, nobody sees it go) and when they press the end button (the scroll
  lands on the last line with the thumb at the bottom). A handed-back reservation collapses in a
  short settle rather than a jump.
- **Cli**: `minBase` on registry items - the lowest `Blaizio.Base` an item's sources work against,
  set per family in the generator when a component calls into a Base capability (typically a JS
  module) that shipped in a specific release. `add` now fails BEFORE installing anything when the
  project pins an older `Blaizio.Base`, naming the item, both versions and the upgrade path
  (`dotnet tool update --global Blaizio.Cli`, then `blaizio update`) - previously the unpinned
  package was skipped without a version look and the component's interop 404'd at runtime. A
  missing, floating (`0.1.0-alpha.*`) or unpinned reference skips the check; `panel` is the first
  item to carry one (its drag-resize module shipped in Base alpha.24). Item schema and the
  registry-item reference document the field.
- **Ui**: the Panel family (`BzPanel` + Header/Title/Close/Content/Footer) - an in-flow side panel
  that PUSHES its siblings instead of overlaying them: no portal, no backdrop, no focus trap, so
  the rest of the page stays interactive. Because it sits in normal flow it is bounded by whatever
  parent it lives in - the full page or any div - and docks to any edge (`Side`: Start/End push
  along the inline axis and mirror under RTL; Top/Bottom push along the block axis). Open/close
  animates the panel's size between 0 and `Size` (any CSS length, exposed as `--panel-size`), with
  the inner surface pinned to the content-facing edge so it slides in from the docked edge like a
  sheet while the siblings are pushed; while closed the panel is `inert`, so nothing inside it can
  be tabbed to or read. `Variant` picks the look: `Attached` (flat, one shared border - reads as
  part of the content) or `Floating` (an inset card with a full border, rounding and a shadow -
  its own surface). `Resizable` adds a
  window-splitter handle on the content-facing edge: pointer drag resizes live with the gesture
  applied browser-side (no per-move interop), arrow keys nudge, Home/End jump to `MinSize`/`MaxSize`
  (any CSS length, a bare number meaning px, clamped), and the settled px size lands in
  `SizeChanged` for persistence. The
  handle sits centered on the panel's edge, window-splitter style, and takes the Resizable
  component's options: `Handle` picks the affordance - None (bare strip), Line (a thin centered
  line revealed on hover and while dragging), or a grip on top of that feedback
  (Grip/Dots/Knob/Pill) - `RevealOnHover` hides the grip until the handle is hovered, focused or
  dragging, `HandleContent` replaces the grip with custom content, `HandleClass` restyles the
  strip, and `Cursor`/`DragCursor` swap the OS resize arrows.
- **Ui**: `ResizableHandleVariant.Line` and `RevealOnHover` on the Resizable handle - the same
  affordances as the panel's: every variant except None thickens the hairline into a thin centered
  line on hover and while dragging, and `RevealOnHover` hides the grip until the handle is
  hovered, focused or dragging. None stays exactly as before.
- **Ui**: sheets through the dialog service - `BzDialogOptions.SheetSide` dresses an imperatively
  shown instance in the Sheet skin sliding from that edge, and `ShowSheetAsync` (component and
  template overloads) is the sugar that sets it. Same contract as `ShowAsync`: the content closes
  itself through the cascaded `DialogInstance`, stacking and z-layering included.
- **Base**: `ts/panel.js`, the push panel's resize module - resolves `MinSize`/`MaxSize` CSS
  lengths to px by probing, owns the pointer/keyboard gesture, and reports only the committed size
  to C#.
- **Ui**: `AutoFocus` and `AutoSelect` across the input family - `InputText`, `InputGroupInput` and
  `InputNumber` take both; `InputTags`, `InputOtp`, `InputDate` and `InputTime` take `AutoFocus`
  (their segments and slots already select themselves). `AutoFocus` is the one the platform cannot
  give you: browsers honor the native attribute only while the document loads, so a field inside a
  dialog, a wizard step or anything Blazor renders later never focuses. This focuses on MOUNT and
  retries across animation frames, which is what carries a field that renders while its dialog is
  still opening. On a container - an OTP's slots, a date field's segment row - it descends to the
  first focusable control. `AutoSelect` selects the value on every focus, the replace-on-type
  pattern for a field the user overwrites rather than edits. Each of the seven also exposes a
  `FocusAsync(FocusOptions?)` method - hold the component with `@ref` and call it, no id to
  invent; it rides `ICore`'s retrying focus and reports whether focus landed.
- **Base**: `ICore` / `Core`, the browser-side services Blazor cannot provide from C#, registered by
  `AddBlaizio()`. `FocusAsync` retries across animation frames, so focusing a target that is still
  `display:none` mid-open-animation lands instead of silently doing nothing, and it reports whether
  it did. It takes an `ElementReference` or an element id - the id form is how you reach the styled
  inputs, which never expose a reference: give the field an `id` through its forwarded attributes
  and focus it with that. The id is re-resolved on every retry (a call racing the render that
  creates the element still lands), and either form descends from a wrapper to the first focusable
  control inside, so a container id - an OTP's, a date field's - focuses the right thing. `EnsureGuardsAsync` installs the key guard below. There is deliberately no imperative
  `PreventDefaultAsync`: the browser applies a default action synchronously during dispatch and a
  C# handler runs after it, so such an API would be timing-dependent by construction. The module
  behind it, `ts/core.ts`, is the renamed `ts/interop.ts` and now also holds the combo parsing the
  hotkeys module used to own.
- **Ui**: `PreventKeys` on `InputText` and `InputGroupInput` - the combos whose browser default is
  suppressed, comma-separated and matched exactly on modifiers (`"enter, mod+s"`). A send-on-Enter
  composer can now handle Enter in `OnKeyDown` without the newline landing first, while Shift+Enter
  still breaks the line: the granularity Blazor's own all-or-nothing
  `@onkeydown:preventDefault` cannot express. The suppression is one delegated capture listener
  reading a `data-bz-prevent-keys` attribute, so it costs no interop per keystroke and the event
  still reaches the C# handler.
- **Ui**: `Cursor` / `GrabbingCursor` on `ColorPicker` and `Cursor` / `DragCursor` on
  `ResizableHandle`, matching the pair `Sortable` already had. `BzCursor.From` gained `halo`, a
  contrasting backing drawn behind the glyph the way the OS hands pair a white body with a dark
  line - without one an outline icon is bare 2px strokes with a see-through interior, legible over
  a plain page and invisible over busy content.

- **Ui**: infinite scroll on `Virtualizer` - `OnLoadMore`, `LoadMoreOffset` and `LoadMoreContent`.
  A batch-fetching source becomes one continuous list: when the rendered window comes within
  `LoadMoreOffset` items of the end (10 by default) the callback fires once, item-sized skeleton
  rows hold the tail while the batch is on the way, and appending to `Items` re-arms it for the
  next batch. There is deliberately no `HasMore` flag to maintain: the callback only re-arms when
  `Items` grows, so a drained source stops itself - the fetch that returns without appending is
  the last one ever made.
- **Ui**: `ItemsProvider` on `Virtualizer` - the constant-memory source for large server-backed
  lists. The provider receives a start index + count (plus a cancellation token that fires when a
  faster scroll supersedes the window) and returns that slice with the set's total count; the
  virtualizer holds only the current window, discards what scrolls away, sizes the scrollbar from
  the total up front so the user can jump anywhere, and renders item-sized skeleton placeholders
  (`Placeholder` overrides) while a window is on the way. `RefreshDataAsync` re-queries after the
  underlying data changes. The two sources never combine and the split is enforced loudly:
  `Items` (+`OnLoadMore`) is the feed model where batches accumulate in a list you own;
  `ItemsProvider` is the seeking model where the virtualizer fetches windows you don't keep -
  setting both, or `OnLoadMore` with a provider, throws with a message saying which to drop.
- **Base, Ui**: `ScrollToIndexAsync` and `InitialItemIndex` on the window virtualizer and
  `Virtualizer`. `InitialItemIndex` opens a long list at an item on the first interactive render
  (one-shot, clamped); `ScrollToIndexAsync` jumps after that - instant, top-aligned, index math in
  fixed mode, measured offsets in dynamic mode (a jump into unmeasured territory lands on the
  estimate and settles as rows measure). Called before the JS attaches, the target is remembered
  and applied on attach.
- **Ui**: `Loading` / `LoadingContent` on `Virtualizer`. A virtualized list whose rows are still on
  their way used to render its empty state - or nothing - until the fetch landed, so a slow source
  read as "no results". Set `Loading` while the request is in flight and an empty list paints
  item-sized skeleton rows instead; `LoadingContent` swaps in your own wait. `DataTable` picked up
  the same default: while an `ItemsProvider` loads and no rows are on screen it shows skeleton rows
  shaped like the table - one bar per visible column, a page's worth when `PageSize` is set -
  instead of the old "Loading..." sentence, and a custom `LoadingContent` still renders in a single
  cell spanning every column.
- **Cli**: `@default/item`, the reserved namespace a third-party registry uses to depend on the
  components its consumers already install. Inside a namespaced item a plain dependency name means
  that item's own registry, which is right for a registry shipping a complete set and wrong for one
  building on the base components: an editor that needs `toolbar` sent the CLI looking for
  `@editor/toolbar`. A `@default/` dependency resolves against the consumer's default registry and
  installs exactly as their own copy would - ordinary folder, ordinary namespace, ordinary install
  record - so nothing is duplicated and an already-installed component is reused. It works on the
  command line too, and `registry add` refuses to record it.
- **Cli**: a missing dependency that the resolver rewrote now says so. A plain name inside a
  namespaced item is claimed by that item's registry, so the failure named an address the author
  never wrote (`https://acme.dev/r/toolbar.json`) with nothing joining it to the `toolbar` they
  did write. It now adds which dependency, of which item, why it resolved there, and that
  `@default/toolbar` reaches the consumer's own registry instead.
- **Cli**: `registry add` refuses `@blaizio`. Its installs would land in a `Blaizio` folder, and
  every `Blaizio.Base` reference inside those files would then bind to that nested namespace
  segment instead of the package, so the components would install and fail to compile.
- **Docs**: the /themes composer redesigned around direct token editing. The left rail is now a
  bottom dock on every viewport (dropdowns open upward; the meta actions live in a three-dot
  menu at the end; Shuffle/Undo/Redo/Get Code are stacked beside it), the canvas takes the full
  width with the page switcher at the top, and page 01 leads with a theme identity card (theme +
  body face, live token swatches). Every semantic token (primary, surfaces, statuses, border,
  chart series) is a swatch in the dock: its popover holds a live color picker with an AA/AAA
  contrast readout, edits apply per mode (light/dark edited separately), persist like every other
  knob, and text-bearing surfaces auto-derive an AA-guaranteed foreground. Preset codes gained a
  v3 form carrying the edits (`<code>-<chunks>`), so customized themes share by URL and decode in
  the CLI - `blaizio apply` patches them over the preset. Shuffle no longer draws from the theme
  gallery: themes are curated complete looks, Shuffle explores raw combinations (a random
  primary pair plus charts, faces and radius); picking a theme clears the edits and applies its
  pairings.

### Changed
- **Cli**: adapted to the dependency bump's Spectre.Console.Cli, whose command entry points
  became `protected` and grew a `CancellationToken`. Command-to-command forwarding (`add` runs
  `apply`, `update` runs `add`, `new`/`add` run `init`) goes through a public `RunAsync` now,
  and the test suite references the split-out `Spectre.Console.Cli.Testing` package.
- **Cli**: `build` writes to `./wwwroot/r` by default, not `./public/r`. `public/` is a JS
  toolchain word; the audience here has one word for "the folder served verbatim", and a Blazor
  app that hosts its own registry now serves it on `dotnet watch` with nothing configured. Every
  snippet and the Pages workflow follow (`upload-pages-artifact` takes `./wwwroot`). `-o` still
  puts it anywhere.
- **Toolbar**: the overflow buttons exist only while there is overflow. A `Scroll` bar used to
  render both chevrons whatever its width - a bar that fit carried two permanently dimmed buttons -
  and an `Expand` bar always rendered its toggle, expanding nothing when the controls already sat
  on one row. The scroller now publishes `data-overflowing` on the root (width for Scroll, a second
  row for Expand, measured against the first control's bottom edge so `items-center` stagger never
  counts as a row) and the chevrons and toggle stay `display:none` until it reads `true`. Absent
  attribute counts as hidden, so a prerendered bar never paints dead buttons. Hiding widens the
  viewport and showing narrows it, but the check runs against the current layout, so the band where
  either answer would hold keeps whatever it has: no flicker. Hidden buttons fall out of the arrow
  order on their own (`End` lands on the last real control), and the dim-when-exhausted rule still
  applies once the bar does overflow. Expand gains the same `toolbar.js` import Scroll already had.
- **Cli**: the message over a ledger entry `update` cannot re-pull covers how it got there. It
  used to imagine only "another registry" and send the reader to `registry add`; an item installed
  from a file or URL got advice that could not apply. It now says to re-add from the file or URL
  (after which the record follows it), or from its registry namespaced, or to remove it - and that
  remove deletes the files the item installed, since it is not a way to forget a record. A record
  that carries a source gets the short version: the source no longer serves it.
- **Base**: a `ScrollAnchor` row anchors wherever the reader was. It used to anchor only when
  they were already at the live edge, so sending a turn after scrolling up to re-read something
  left the new turn out of view and the reader hunting for the button. The reader's own turn is
  the thing to look at: it anchors, the reservation opens under it, and follow comes on with it.
- **Base**: an anchored turn no longer switches follow off. The reader has the latest line on
  screen throughout - the reply streams into the space reserved under their turn - so the engine
  now measures "at the live edge" against the end of real content, not the end of the
  reservation, and keeps following. Follow only ever scrolls forward to the end of real content,
  never back up into a reservation, so at the anchor the pinned turn holds until the reply passes
  the fold and from there the view follows the reply like any other stream - and a reader who
  scrolled up and came back by the button or the scrollbar is followed from wherever they landed,
  where before the reservation froze follow for everyone until it was used up. The end button and
  "scroll to latest" measure the same way: no button over blank, and the jump lands on the last
  line rather than at the bottom of the reservation.
- **Base**: the message scroller's own smooth scrolls no longer read as the reader scrolling.
  The browser animates `scrollTo` over several frames and fires a scroll event per frame, and at
  every one of those positions the live metrics said "not at the end" - so an anchored turn
  switched follow off mid-flight (end button up) and on again on arrival, every click. An engine-initiated scroll now settles: follow is whatever the caller meant, the buttons
  are judged at the destination, and its scroll events only count for arrival; `scrollend`, a
  wheel, pointer or key from the reader, or a one-second timeout end it. Same treatment for the
  ScrollTo commands and the buttons.
- **Base**: a reservation is measured against rendered rows. Rows carry `content-visibility:
  auto` with a 10rem placeholder, and Chromium sizes a freshly inserted row at that placeholder
  until its first visibility pass a frame later - so the anchor measured two stand-ins, found no
  room to reserve, and the turn degraded to a plain scroll-to-bottom. The anchored turn and the
  rows under it are now rendered for real while the reservation is live (they are on screen by
  construction) and handed back to `auto` when it is used up. The reservation is also measured
  from the rows rather than the column's scroll height: the column has a min-height of the
  viewport, so with a short transcript the scroll height sat on that floor and did not move with
  the pad - the pad converged one step per resize and the anchor scroll issued in between was
  clamped to zero, which is why a fresh transcript never anchored at all.
- **Base**: the echo of the engine's own stick-to-end is recognised, so text landing between
  that write and its scroll event no longer reads as the reader leaving the bottom.
- **Ui**: `BzBubble` gained `TailShape` - `Triangle` (the flat pointer it always had, still the
  default) or `Curved` (a taller wedge with a rounded tip that sweeps away from the corner). Both
  shapes are the same `::after` pseudo-element, so the tail keeps taking the variant's fill from
  the sheet and the ghost rule still hides it; each shape is drawn once for the top-start corner
  and `Align="End"`, `Tail="Bottom"` and RTL flip it instead of carrying geometry per corner.
  `BubbleReactionsSide` is now `BubbleReactionAnchor` (the `Side` parameter is unchanged).
- **Cli**: `update` sets its closing line apart - a yellow `Tool itself:` label and a blank line
  above it. It was the only line in the summary with no label at all, so the one step the command
  cannot perform for itself read as a footnote under the results and was missed.

### Removed
- **Base, Ui**: the message scroller's `data-autoscrolling` flag and the scrollbar it hid. The
  viewport used to drop its scrollbar while a reply was being followed and bring it back on a
  quiet timer, and that timer blinked the scrollbar through every anchored reply (the
  reservation keeps the height flat, so "growth" never re-armed it). A scrollbar that stays put
  is the honest state: `AutoScroll` decides whether the transcript follows the stream, the
  scrollbar just shows where you are.
- **Ui**: the color picker's built-in hand cursors, and `ts/cursors.ts` with them. The picker
  forced its own artwork on every draggable surface and rasterised it through a canvas on first
  render (Chromium refuses SVG cursors without intrinsic dimensions). The OS grab and grabbing
  hands are the default now, and custom artwork is opt-in through the parameters above - the same
  contract as the scrollbar utility: the library ships behaviour, not taste.

### Fixed
- **Accordion, Alert**: the prose conveniences stop at the content's own level. The accordion
  panel's paragraph spacing (`mb-4` between paragraphs) and the link styling on the panel, the
  alert title and the alert description used descendant selectors, so anything nested inside -
  a card, a form, another accordion - had its own `<p>` and `<a>` elements restyled by a
  component that does not own them. Child combinators now: the rules apply to the content's
  top-level paragraphs and links, and nested markup keeps its own styling.
- **Cli**: one Ctrl+C quits, and Escape cancels any prompt. Spectre's synchronous prompts own
  the keyboard and never look at a token, so the first Ctrl+C during a question was swallowed
  by the prompt's key loop and only the second (hard-exit) press got out. Every interactive
  question now goes through one front door that observes the cancellation token: confirms
  answer to a single key (y / n, Enter for the default), text inputs and selection lists ride
  the token-aware prompt APIs, and the checkbox pickers read keys the same way. Escape anywhere
  cancels the whole command - the quiet "Cancelled." exit (130), same as Ctrl+C - and the key
  legends say so. The second Ctrl+C stays as the hard-exit escape hatch for a stuck run.
- **Toolbar**: an Expand bar containing a select no longer shows its toggle with nothing
  clipped. A closed select keeps its content in the bar as a `display:none` `position:fixed`
  child at offsetTop 0, and the row measurement took it as the topmost control - putting the
  "first row" at the top of the page, so every real control read as wrapped and
  `data-overflowing` stuck true. Out-of-flow children (hidden, fixed, absolute) are now ignored
  by the first-row, wraps and reveal measurements, and the overflow demo carries a select so
  the case stays covered.
- **Cli**: `add` warns when one item ends up recorded twice. Installing `editor` from a file
  while `@editor/editor` is also on the ledger leaves two records that each maintain their own
  copy at their own paths - the namespaced one nests under the namespace folder - so every
  update faithfully rewrites both layouts and the duplicates read as CLI corruption (they were
  taken for exactly that in the field). Each record is deterministic; the pair is the problem.
  The add now names the rival and the `blaizio remove` that resolves it, in the report and in
  the JSON result (`rivalRecords`).
- **Cli**: a component installed from a relative file path updates from anywhere. The record
  keeps the source as written (`../registry/r/editor.json` stays portable with the project), but
  the read resolved it against the process working directory - correct only when the command
  happened to run inside the project, and broken the moment `update` ran from the solution root.
  The registry client now roots relative local references at the project directory before
  opening them.
- **Tooltip**: no more tooltip popping on returning to the tab. The browser restores focus to
  the last-focused element and re-evaluates `:focus-visible` as if the focus were
  keyboard-driven, so a button merely clicked before switching away suddenly matched and its
  tooltip opened with the pointer nowhere near it. A focusin landing in the same breath as the
  window regaining focus is the restore, not the user: the module stamps that on the event
  itself (the interop question arrives a round-trip later) and the trigger stays closed. A
  genuine keyboard focus afterwards still opens immediately.
- **Toolbar**: the overflow buttons hid nowhere but the docs site. The hide rule used the
  `not-group-data-[...]` variant, which Tailwind 4.3 (the docs' node build) compiles and the
  CLI's pinned standalone 4.1.11 (every consumer) silently drops - so a consumer's bar kept
  showing its chevrons and toggle with nothing to overflow. Now `group-not-data-[...]`, which
  both compile to the same selector. Verified against the 4.1.11 binary this time.
- **Base**: the message scroller's reserved space no longer outlives its anchored row. When the
  row left the DOM with space still reserved (a cleared transcript, a deleted or retried turn, a
  re-keyed render) the engine forgot the anchor but kept the padding, leaving a viewport of blank
  at the end of the transcript until the next anchored turn recomputed it.
- **Ui**: a `Virtualizer` with `MaxHeight` is keyboard-scrollable. Chromium refuses to scroll a
  scroll box that cannot take focus, so the viewport now carries `tabindex="0"` (with the
  library's inset focus ring), the same treatment the data table's scroll container already had -
  and the axe `scrollable-region-focusable` rule stops flagging it.
- **Ui**: the sidebar trigger's default glyph mirrors under RTL. The icon draws its panel on the
  left, which is where the sidebar sits in LTR; right-to-left flips the sidebar to the right, and
  now the glyph follows.
- **Ui**: keyboard focus on a tab trigger (and a focusable badge) wore the browser's own white
  outline instead of the library's focus ring. Both components paint the two-layer ring but had
  slipped past the `outline-none` every other focusable component carries, so the UA outline drew
  on top - the "white border" on the active tab. They were the only two; the whole styled layer
  was swept.
- **Ui**: an Expand toolbar no longer unfolds when a click lands inside it. The clipped row lifted
  on any focus within the bar, so opening a dropdown menu sitting in the visible row yanked the
  whole bar open under the pointer. The reveal now keys on `:focus-visible` inside the viewport:
  keyboard travel into the hidden rows still holds the bar open on its own, pointer focus leaves
  it clipped, and the trailing toggle remains the pointer's way in.
- **Docs**: a collapsed example-code panel no longer shows a vertical scrollbar. The preview clips
  to four lines, but the code area inside kept its own expanded-height scroll box, so long
  examples scrolled inside the clip; the code area now scrolls only while expanded.
- **Docs**: a wide code block's horizontal scrollbar is reachable from wherever you are reading.
  Blocks now cap at 70vh and scroll on themselves, so the bar stays on screen instead of parking
  at the foot of a 2000px block - the sidebar page had nine of those. Each block also gained a
  wrap toggle beside Copy, which soft-wraps long lines (and breaks a long unbroken token rather
  than restoring the scroll); wrapped lines hang under the code, not under their own line number,
  since the numbers now sit on block line boxes with a hanging indent. Both controls carry
  tooltips.
- **Docs**: the RTL demos' language tabs read right to left, like the demo under them - Arabic
  first from the right, arrow keys following suit.
- **Docs**: the API pages read as reference again. Their table of contents lists the family
  (`BzInputGroup`, `BzInputGroupAddon`, ...) instead of a single "API Reference" entry that
  restated the page title; long parameter descriptions wrap instead of stretching the table into a
  3000px horizontal scroll (the skin's table cells are `nowrap`); and a summary that documents a
  grammar renders its examples as a list, since the build's XML flattener now keeps `<para>` and
  `<item>` breaks. The summaries themselves are boiled down to a small JSON file at build time -
  the first API page used to parse the raw 600KB compiler XML with `XDocument` on the WebAssembly
  interpreter, a visible stall with empty tables underneath it.
- **Docs**: the sidebar variants demo and every RTL demo switch with tabs rather than a stack and
  a dropdown - three sidebar shells stacked was a lot of scrolling for a difference that only
  reads when you can flip between them, and the language switch is a two-way choice, which is what
  a tab pair is for.
- **Base, Icons**: a packed version is never packed twice. Both packages take their version from
  one property (`BlaizioVersionBase`), and any pack that would overwrite an existing `.nupkg` now
  fails with a message naming the fix. NuGet caches by id+version and never re-extracts a version
  it already holds, so republishing different bytes under one version was invisible: a consumer
  compiled against the new package, loaded the old assembly, and hit a `TypeLoadException` at
  runtime that only a cache eviction plus a clean `obj`/`bin` cleared. The docs dogfood loop, which
  repacks on every edit to either package, now mints a private revision per pack and floats its own
  references onto it, so consumer-facing versions stay immutable.
- **Cli**: the version of `Blaizio.Base`/`Blaizio.Icons` that `init` and `upgrade` write into a
  project is generated from the build, so a release cannot ship a tool naming a version the
  packages never packed.
- **Docs**: the site header no longer pushes the page into a horizontal scrollbar between
  roughly 800 and 1100px: the alpha badge and the GitHub link hide below lg, the search control
  falls back to its icon button there, and `html` clips horizontal overflow outright (wide
  content scrolls inside its own container, never the page).
- **Ui**: `BzColorPicker` now decides controlled vs uncontrolled by whether `Value` is supplied
  (the null sentinel every other component uses) instead of by `ValueChanged` having a delegate -
  an uncontrolled picker with a change listener rendered black because it was treated as
  controlled with a null value.
- **Ui**: the Zenith preset's dark block now inverts its primary pairing like the newer palettes -
  bright spring-green primary (`oklch(0.74 0.14 155)`) under a near-black label - instead of white
  on mid emerald. The old pairing was the worst in the audit (3.29:1 raw, 4.86:1 after the button
  deepen); the new one measures 8.58:1 raw, 5.61:1 deepened, and 8.67:1 for primary used as link
  text on the dark background. Light mode is unchanged.
- **Ui**: the remaining primary-filled text surfaces - default badge (seven skins; ash's
  text-only badge was already fine), primary tooltip, toast action and avatar badge - now deepen
  their dark-mode fill 15% toward black in oklab, exactly like the default button. A contrast
  audit of all 15 palettes found seven (comet, meteor, nebula, pulsar, quasar, zenith and the
  default Nova) whose dark `--primary` sits at 3.3-4.0:1 under `--primary-foreground`, below
  WCAG AA for text; the deepened fill clears 4.5:1 in every palette. Non-text primary fills
  (checkbox, radio, switch, slider, progress) stay raw - the worst pairing is 3.29:1, above the
  3:1 graphical minimum.

### Changed
- **Base, Ui** (breaking): tabs default to manual keyboard activation. Arrow keys move focus along
  the tablist and Enter or Space activates the focused tab; a tab no longer loads its panel just
  because focus passed over it on the way somewhere else. `ActivationMode="TabsActivationMode.Automatic"`
  restores selection-follows-focus per instance.
- **Ui**: an Expand toolbar opens and closes with a 150ms height tween instead of snapping. The
  clip now interpolates to the content's own height (`interpolate-size` - Chromium animates,
  other engines keep the instant swap), reduced-motion users keep the instant swap too, and the
  viewport carries 2px of inner breathing room so a control's focus ring is no longer shaved by
  the clip while the bar is collapsed.
- **Ui, Cli** (breaking, 0.1.0-alpha.20): `scrollbar-thin` is a project-level opt-in. The
  components still mark their scroll areas with it (select and command lists, menus, the
  virtualizer, scrollable tables, the sidebar), but `blaizio.css` now ships the utility as an
  inert stub, so an app that never asked keeps the browser's own scrollbars everywhere instead of
  inheriting a look it did not choose. Opting in redefines the utility in the app's own Tailwind
  input - `--scrollbar` on `new`, `add` or `apply` appends the block, or paste it from the
  Scrollbar page - and every mark lights up at once. The block lands in your file: restyling the
  bar is an edit, opting back out is a delete. Projects that want the previous behavior need the
  flag once. `scrollbar-hover`, `scrollbar-activity`, `scrollbar-none` and `scrollbar-auto` are
  unchanged - they are only ever applied by hand.
- **Cli**: `apply` gained `--pointer` and `--scrollbar`, the same wiring toggles `new` and `add`
  take, so a Themes selection reaches an existing app in one command. A full `apply` of a code
  carrying RTL now also records the flag and installs `direction-provider` once, matching what
  `add --rtl` has always done - the skins mirror through logical properties on their own, but a
  layout flips direction through the provider.
- **Ui** (breaking): `TooltipVariant.Default` is now the inverted foreground surface
  (`bg-foreground` / `text-background`), the conventional tooltip look, and the solid primary
  surface moves to its own `TooltipVariant.Primary`. `BzTooltipContent.Variant` previously
  defaulted to `Accent` while the enum's `Default` member painted primary; both now agree on
  `Default`. A tooltip that relied on `TooltipVariant.Default` for the primary fill needs
  `TooltipVariant.Primary`; one that relied on the accent default needs an explicit
  `TooltipVariant.Accent`.
- **Base, Ui** (0.1.0-alpha.18): event callbacks keep their `On` prefix - `OnClick`, `OnSelect`,
  `OnFocus`, `OnBlur`, `OnKeyDown`, `OnDragStart`, `OnDragEnd`. This reverts the alpha.17 rename:
  the ecosystem-standard names win over attribute pass-through. The documented consequence stands -
  a raw DOM attribute of the same name on a component (e.g. `onfocus="..."` as a JS string) binds
  to the parameter instead of reaching the element; use the C# callback, or put the raw attribute
  on the element via `RenderAs`.

### Added
- **Cli**: `registry:theme` items install. A theme item carries a `cssVars` payload split into
  `light` and `dark` maps (token names with or without the `--` prefix); `add` patches the values
  into the tokens file's `:root` / `.dark` blocks declaration by declaration, leaving everything
  else in the file alone. `registry validate` accepts file-less theme/font items but requires
  their payloads.
- **Cli**: namespaced installs nest. `add @acme/button` now writes under its own registry folder
  (`Components/Ui/Acme/`), one namespace segment down (`MyApp.Components.Ui.Acme`), and is
  recorded as `@acme/button` - so two registries can both ship a `button` without colliding on
  disk, in C#, or in `blaizio.json`. `diff`, `remove` and whole-registry `--prune` are all
  namespace-aware (a prune never touches other registries' files or records).
- **Docs**: the /community page - a searchable, paginated directory of community registries (with
  per-entry add-command dialogs) and a community theme gallery whose entries can be applied to the
  whole docs site live or copied as CSS. Driven by two committed JSON files under
  `wwwroot/community/`; listing is a pull request. A new "Registry" guide (docs/registry) covers
  the manifest format, authoring components and themes, hosting, consumption, trust, and the
  get-listed flow.

- **Cli**: a trust gate in front of third-party sources. `registry add` states what recording a
  registry means (installed items are source code compiled into your app, plus their NuGet
  packages) and asks for confirmation when a terminal is attached (`-y` skips; scripts proceed
  with the note printed). `add` with a direct URL from a host that is neither the configured
  registry nor recorded under `registry add` warns and confirms the same way.

### Changed
- **Base, Ui** (breaking, audit batch 3 waves 1+2): one enum per axis and one name per concept
  across the component surface. Shared SelectionMode replaces AccordionType/ToggleGroupType/
  TreeSelectionMode and Select/Combobox bool Multiple; CarouselOrientation/ResizeDirection fold
  into Orientation; TreeCheckState into CheckedState; size enums say Default (not Md) and
  TooltipVariant leads with Default. Renames: Active (was IsActive/Current/Selected), the
  Search/DefaultSearch/SearchChanged triad (was Query/SearchTerm/Filter), FilterPredicate,
  DismissOnOutsideClick (positive, was PreventDismiss/DismissOnClick), Virtualize/ItemSize/
  Overscan, Toast provider Duration/CloseButton/RichColors. Carousel gains bindable
  SelectedIndex; InputOtp gains DefaultValue; BzInputTags/BzSlider surface their base bindings.
  Every emitted class, data-* attribute and JS string is unchanged - only C# names moved.
- **Base, Ui** (breaking, audit batch 3 wave 3): the last rename pass. Table's public types
  drop the phantom "DataGrid" name (DataTableRequest/Result/ItemsProvider/Sort/Column, was
  DataGrid*/GridSort/ColumnDef). Sheet and Drawer share one PanelSide enum (both params `Side`;
  SheetSide/DrawerDirection deleted). The element-tag parameter is `Element` everywhere (was
  `As`, one letter from the unrelated RenderAs). Placeholder splits into string `Placeholder` +
  fragment `PlaceholderContent` on Select and Combobox values. Disabled predicates are
  `DisabledSelector` (Calendar, Sortable). ScrollArea `Type`->`Behavior`, PieChart
  `Color`->`ColorSelector`, Slider `Tooltip`->`TooltipMode`, Sidebar `Collapsible`->`CollapseMode`,
  DirectionProvider `Direction`->`Dir`, InputNumber step `Direction`->`StepDirection`.
  AvatarStatus folds into ImageStatus; UiDialogOptions is BzDialogOptions; OtpSlotState says
  Active/ShowCaret; `*Template` fragments are `*Content`; CarouselDots/SelectValue/CommandDialog/
  ToastProvider gain the attribute splat; ColumnBase `Hidden` inverts to `Visible`; ellipsis
  `MoreLabel` is `AriaLabel`. Wire strings all unchanged.
- **Cli** (breaking, audit batch 1 - blaizio.json + registry schema): the config field that
  stored the skin is now `style` (it shipped as `theme`, colliding with registry theme items),
  and the font pair is `headingFont`/`bodyFont` (was `heading`/`font`). Old files keep working:
  the legacy names are accepted on load and rewritten on the next save. The dead `$schema`
  default (a 404 URL) is no longer written. Registry file entries type as `registry:ui`/
  `registry:lib` only (a new `FileType`, same wire strings - theme/font/template are item kinds,
  not file kinds), and inlined `content` is immutable after parse.
- **Cli** (breaking, audit batch 2): `--json` is output-format only - a machine-driven add/init
  now installs packages and wires the Tailwind pipeline exactly like a human run, and the JSON
  document reports what actually happened. `add -o|--output` is gone (files written outside the
  configured output directory were recorded on paths remove/uninstall/diff resolve against the
  wrong root). The add trust gate records accepted origins in blaizio.json `trustedHosts`, so a
  host confirms once, ever. `update --dry-run` reports the full plan without touching anything.
  New `registry list` and `registry remove` (alias `rm`); `search`'s `list` alias now shows in
  help; `build`/`registry validate` positionals read `[manifest]`. Internal-only Core helpers
  (CssBlocks, TokenOverlays, ProcessRunner, GlobalUsingsWriter, ImportsUpdater) are internal now,
  and the never-read `aliases.ui` config entry is no longer written.
- **Cli** (breaking): registry item JSON no longer carries the dead `tailwind` field (it was read
  by nothing), and `search -o` is gone - `--offset` is long-only, since `-o` means `--output` on
  every other command. Declining the Tailwind download confirm now exits 0 like every other
  declined confirm. The full pre-beta consistency sweep lives in `docs/api-freeze-audit.md`.
- **Cli** (breaking, audit batch 4 - the shared flags say what they mean): every command now
  carries exactly the shared flags it honors. The common surface is tiered - all commands take
  `-c`/`-s`/`--json`; only commands that confirm take `-y`; only commands that read a registry
  take `--registry` - so `info`, `contrast`, `eject`, `generate`, `build` and the `tailwind`
  subcommands no longer accept-and-ignore `--registry`, and `search`/`view`/`info` no longer
  accept a `-y` they never prompt for. `docs` gains `-s` and its `--registry` is documented
  (it was hidden and a hard parse error). The `preset` subcommands share one surface (`url`/
  `open` took no flags at all; every leaf now takes `-c`/`-s`/`--json`). `registry add`/`list`/
  `remove`/`validate` all emit `--json` (`validate` reports a findings array on every outcome -
  missing, unparseable, invalid, valid - for CI). `apply --dry-run` reports the full plan
  (re-installs, theme, fonts, tokens) without touching anything, same as `update`. `--json`
  stdout is uniformly compact (one line per document; on-disk files stay indented), the last
  hardcoded JSON string literals are serialized for real, and `add --json`'s file entries carry
  one `path` (output-relative, POSIX) instead of `relativePath`+`absolutePath`. `init`'s
  never-registered CLI surface is gone - the wiring pipeline is programmatic-only behind
  `new`/`add`, whose flags forward to it.

### Fixed
- **Base**: the incremental-build stamp moved from `wwwroot/dist/.stamp` into `obj/`. As a static
  web asset it was listed in the packaged manifest but stripped from the NuGet payload (default
  dotfile excludes), so every consumer build failed with MSB3030 trying to copy a file the
  package never shipped.
- **Base**: the packaged contract sheet (`css/blaizio.css`) now includes the shimmer utilities
  (`shimmer`, `shimmer-block`, and their knobs) - registry components that emit them
  (`BzSkeleton`) rendered without a sweep against the alpha.16 sheet.
- **Cli**: `CssBlocks.FindBlock` treated top-level statements (`@import ...;`,
  `@custom-variant ...;`) as part of the next block's selector, so a tokens file whose first
  braced block followed such statements was invisible to every scoped patch (fonts, presets,
  chart/radius overlays). Statements now terminate the selector prelude.

## 0.1.0-alpha.16 - 2026-07-26

### Fixed
- **Base**: the select listbox keeps its focus inside a dialog - opening one from a dialog left the
  keyboard dead (arrows and typeahead did nothing) and made Escape close the dialog instead of the
  listbox. Both were the same defect: the listbox portals to the document body, so focus landing on
  an option is outside the dialog's subtree, and the dialog's focus trap - which tests containment
  by DOM alone - yanked it straight back to the trigger; with focus stranded there, Escape bubbled
  into the dialog's own handler. Menus and popovers were never affected because each wraps a
  `BaseFocusScope`, and scopes stack, which is what pauses the trap beneath. The select's surface
  stays mounted while closed (its options register the trigger's display value), so it now creates
  a scope from its JS attach path instead - `passive`, a new option that claims the scope stack and
  nothing else, since `ts/menu.js` already owns focus placement and restoration for a listbox.
  Stacked dialogs compose for free: the stack is LIFO, so a passive scope pauses whatever is
  beneath it at any depth.

## 0.1.0-alpha.15 - 2026-07-26

### Fixed
- **Ui**: a popup opened inside a dialog no longer paints behind it. Popups portal to the document
  body, so one opened from a dialog is a stacking *sibling* of that dialog rather than a
  descendant - the z-indices compete instead of composing. Every floating surface sat at the base
  layer (`z-50`) while `BzDialogProvider` stacks each imperatively shown dialog above it (overlay
  60, window 61, then 70/71...), so a select, dropdown menu, combobox, context menu, menubar,
  popover, hover card or tooltip opened inside a service dialog was simply invisible. Raising the
  constant would not fix it - against dialogs at 60/61, 70/71, 80/81 any fixed value is either
  under a later dialog or above one it should sit beneath - so the modal surfaces (dialog, alert
  dialog, sheet, drawer) now cascade the layer they occupy and each popup lifts itself to
  `layer + 5`: clear of that window, short of the next stacked surface. A popup in the first of two
  stacked dialogs still sits correctly under the second, and outside a modal nothing is emitted, so
  the stylesheet keeps governing. The new `OverlayLayer` ships with the `utils` item.

## 0.1.0-alpha.14 - 2026-07-26

### Changed
- **Base**: ⚠️ `BaseInputNumberIncrement` and `BaseInputNumberDecrement` are replaced by one
  `BaseInputNumberStep` with a `Direction` (`InputNumberStepDirection.Increment` / `Decrement`).
  The parts were 46 lines each and differed in six - an aria-label, a data attribute, the `Can*`
  flag gating them and the sign they press with. The markup changes with them: one
  `data-bz-input-number-step` part carrying `data-direction="increment|decrement"`, so a skin can
  target the pair or either half. An audit found this was the only such split in the library.
- **Ui/Base**: ⚠️ the Image surface hands the edited picture back as a parameter instead of
  offering a download button. `BzColorImage`'s `Download` / `DownloadAriaLabel` /
  `DownloadFileName` and the picker's `ImageDownload` are gone; bind `Export` (or the picker's
  `ImageExport`) for the picture as a `data:` URL - rotation and the tone grade baked into the
  pixels - and save, upload or paint it yourself (an `<a download>` is the whole download). It is
  produced only when something is bound, since baking re-encodes the bitmap, and settles after
  edits stop (`ExportDelayMs`, default `300`) rather than firing on every slider tick. An
  untouched image keeps its original encoding; a graded one comes back as PNG. `imageFill.ts`
  exports `exportImage` in place of `downloadImage`.

### Fixed
- **Base**: `BaseInputNumber` refuses characters that could never be part of a number - a typed
  letter used to sit in the field until blur reverted it. The field stays `type="text"` on purpose
  (`role="spinbutton"` owns the semantics, and a native number input brings a spinner that
  duplicates the stepper buttons, a scroll wheel that changes the value by accident, and a `value`
  that reads back empty for anything the browser dislikes), so `ts/inputNumber.ts` does the one
  thing `type="number"` gave for free: a `beforeinput` guard refuses the character before it is
  inserted, keeping the caret and undo stack intact and covering paste, drops and IME commits.
  What passes is loose (anything that could still grow into a number) and context-aware: no
  decimal separator for an integral `TValue`, no minus when `Min` rules out negatives, and the
  culture's own separator alongside the keypad's dot.

## 0.1.0-alpha.13 - 2026-07-25

### Added
- **Cli**: `add` and `update` check the registry before they touch the project. Both wire first and
  fetch after, so an unreachable registry used to install packages, write the tokens file and edit
  the host page and only then fail - leaving a half-applied project behind a late error. One
  request up front turns that into a clean refusal (exit 2, "nothing was changed"). A wiring-only
  run (`add --rtl`, `--tailwind`, `--css`) skips the check, and a registry that answers without an
  `index.json` still passes: items resolve at the base path.

### Fixed
- **Cli**: an item resolves from a registry that ships no `index.json` even when the project has a
  skin recorded. The per-skin variant gate read the index and let a missing one fail the whole
  lookup, so v1 (raw sources) and third-party registries were unusable on any initialized project;
  no index now means no skin variants, as documented, rather than an error. Registry failures also
  carry a reason (unreachable / not found / malformed) instead of only a message.

## 0.1.0-alpha.12 - 2026-07-25

### Added
- **Cli**: `blaizio remove <components...>` (alias `rm`) takes individual components back out -
  previously only `uninstall` could undo an add, and it removed everything. Removal is by record
  like uninstall: exactly the files `add` wrote for each named item plus its `blaizio.json` entry,
  so files you authored under the output directory are never swept up and a file two items share
  survives while either is installed. Names resolve however they are typed. It refuses to break
  the project - an item another installed component depends on is reported and skipped (exit 1)
  unless `--force` - and never uninstalls NuGet packages or touches the wiring; components and
  packages nothing needs anymore are listed instead. `--dry-run` previews, `-y` skips the prompt.

## 0.1.0-alpha.11 - 2026-07-25

### Added
- **Ui**: `BzColorPicker` edits gradients. `ShowGradient` adds a Solid / Gradient switch, a stop bar
  and the shape controls (Linear, Radial, Angular, Diamond); the area, sliders and text input then
  edit whichever stop is selected. In gradient mode `Value` carries the CSS paint instead of a color
  string, and the mode follows the shape of whatever `Value` is handed - so a gradient string round
  trips through `@bind-Value`. `@bind-Gradient` exposes the same thing as a model (`GradientValue`:
  type, angle, stops), and `Mode` is bindable too. New parts: `BzColorModeTabs`,
  `BzColorGradientBar`, `BzColorGradientType`.

  Diamond has no CSS gradient function, so it serializes as four quadrant ramps carrying their own
  position and size: assign gradient values to the `background` shorthand, not `background-image`,
  which drops the whole declaration.
- **Ui**: `BzColorPalette` and `BzColorPaletteGroup` - a labelled swatch grid of named ramps, the way
  a design system lists its colors. Rows of equal length line up column by column, and every shade is
  an ordinary `BzColorSwatch`, so clicking applies it and the matching one marks itself selected.
- **Base/Ui**: an Image surface - `BzColorImage` - fills with a picture and grades it. Everything
  happens in the browser (`imageFill.ts`, nothing is uploaded): choose a file, pick how it lays into
  its box (Fill / Fit / Crop / Tile), rotate it a quarter turn at a time, and grade it with
  exposure, contrast, saturation, temperature, tint, highlights and shadows. `Value` becomes the
  CSS paint; the grade rides on `@bind-Image` as an `ImageFillValue`, whose `ToFilterCss()` gives
  the matching `filter` - a separate CSS property, so it cannot travel in the paint. Rotation is
  baked into a new bitmap rather than left as a transform, so the paint always matches what you
  see. Like Gradient it is one of the picker's surfaces, so Solid, Gradient and Image share a
  single switch; each is optional (`ShowSolid`, `ShowGradient`, `ShowImage`), the switch appears
  only once more than one is on, and a picker with all three off still picks a solid color.
- **Ui**: `BzColorPicker` has a `Bordered` card frame (on by default) - turn it off inside a surface
  that already has one, such as a popover - and `ShowOpacityInput` to drop the percentage field
  beside the alpha slider.
- **Ui**: the picker's draggable surfaces use the Tabler hand cursors (`HandStop` at rest,
  `HandGrab` while dragging) in place of the browser's `grab`/`grabbing`, and the gradient stop
  add/remove buttons carry tooltips. Chromium ignores SVG cursors, so `ts/cursors.ts` rasterises
  the pair to PNG on first render and writes them back over the custom properties the sheets read;
  `grab`/`grabbing` stay behind them as the fallback.
- **Ui**: `BzSelectTrigger` takes a `Disabled` of its own, for disabling one trigger inside a
  composite without disabling the whole select.
- **Ui/Base**: the Image surface can save its picture back out. `BzColorImage`'s `Download` (and
  the picker's `ImageDownload`) adds a download button beside rotate that writes the picture as it
  looks in the preview - rotation and the tone grade baked in, by redrawing through the same
  filter string the preview renders with. An untouched image keeps its original file bytes (and
  encoding); a graded one re-encodes as PNG. `DownloadFileName` names the file.

### Fixed
- **Base**: marquee labels (`BzTree`'s `MarqueeLabels`) reveal on hover as intended. Two timing
  faults made them look dead: the one measurement pass ran before webfonts swapped in, so labels
  that only overflow in the real face were never marked truncated (nothing re-measured on font
  load, and no observer fires for a font swap) - now re-measured on `document.fonts.ready` and
  `loadingdone`, and each label is watched by the ResizeObserver, not just the root. The slide was
  also far too slow at 15ms per overflowing pixel: a 190px tail took 2.85s of linear travel, so a
  normal hover moved the text a few imperceptible pixels and slid back. Now 5ms/px capped at
  1.6s, which puts a typical reveal just under a second.
- **Base/Ui**: the marquee slide is driven by the module, not a CSS transition. Chromium does not
  repaint glyphs for interpolated `text-indent` transition frames on an ellipsised line box - the
  computed value and layout advance while the pixels stay frozen, so the reveal looked dead even
  with everything wired correctly. `ts/marquee.ts` now steps the inline `text-indent` in a rAF
  loop (every frame a discrete style write, which does repaint), reverses interrupted slides at
  the same speed, honors reduced motion with an instant reveal, and starts the slide for a cursor
  already parked on a label when it becomes armed. The stylesheet keeps only the hover
  ellipsis-to-clip swap.
- **Cli**: component names resolve regardless of case and separators - `blaizio add inputnumber`,
  `INPUTNUMBER` and `Input_Number` all land on `input-number` (previously only `InputNumber` or
  `input-number` matched). Plain names resolve through the registry index; registries without one
  keep the literal kebab-case path.
- **Cli**: registry dependency inference reads code only. Mentioning another component in a
  comment ("like `BzInputText`") counted as a dependency, so `add input-number` also installed
  `input-text` - 19 phantom dependencies across 17 components. Comments (razor, C# line and
  block, HTML) are stripped before the scan; the shipped registry is regenerated without them.
- **Base**: `BaseSelectContent` no longer throws an unhandled `ObjectDisposedException` when the
  component is torn down mid close-animation (its surrounding surface swapped out, say) - the
  `onClosing` callback is guarded like the dispose paths already were.

## 0.1.0-alpha.10 - 2026-07-25

### Changed
- **Cli**: stack updates are a top-level command: `blaizio update [components...]`. Same lockstep
  operation as before - bump the Blaizio NuGet packages to the tool's pinned versions, then
  re-pull installed components (all, or just the ones named), with the v1-to-v3 migration gate
  intact - but under the verb every package manager uses, instead of hiding a refresh-everything
  mode behind `add`. It does not update the tool itself; that stays
  `dotnet tool update -g Blaizio.Cli`.

### Removed
- **Cli**: `add --update` and its hidden `--upgrade` alias - use `blaizio update`. Strict parsing
  rejects the old spellings loudly, so a stale script fails with a usage error rather than doing
  something else.
- **Cli**: the legacy `deinit` spelling of `uninstall` (the `un` alias stays).

## 0.1.0-alpha.9 - 2026-07-25

### Changed
- **Base/Ui**: `BaseInputNumber` and `BzInputNumber` are generic - `BzInputNumber<TValue>` - over the
  numeric types Blazor's own `InputNumber<TValue>` supports: `int`, `long`, `short`, `float`,
  `double`, `decimal`, bare or nullable. `TValue` is inferred from the binding, so
  `@bind-Value="_days"` on an `int` now just works - no `(double?)` cast, no transforming setter.
  Semantics follow the type:
  - Parsing is per-type, like Blazor's converter: an integral `TValue` rejects `"2.7"` outright
    instead of truncating it.
  - Only a nullable `TValue` can rest empty (empty commits `null`); a non-nullable one keeps its
    last good value and reverts on blur, matching Blazor's failed-parse behavior.
  - Values clamp to the type's own range on top of `Min`/`Max`, so stepping can never overflow the
    conversion back.

  Internally every computation now runs in `decimal`, so integer and money math is exact - three
  `0.1` steps make `0.3`, not `0.30000000000000004` - and the cascaded `InputNumberContext` carries
  `decimal?`, keeping the Group / Input / Increment / Decrement parts non-generic. Migration:
  existing `double?` bindings are unaffected (`TValue` infers as `double?`); code that referenced
  the component type by name needs the type argument (`BzInputNumber<double?>`). `Min` / `Max` /
  `Step` / `LargeStep` stay `double`-typed parameters. A float/double magnitude beyond decimal's
  ±7.9e28 saturates.

## 0.1.0-alpha.8 - 2026-07-25

### Fixed
- **Base**: `BaseInputNumber` reconciles the displayed text correctly in controlled mode. alpha.7 kept
  the text in step with the value by comparing against the last value the component emitted, which
  holds only when the parent stores exactly what it is handed. Two common cases break that, and both
  showed up as the field displaying one number while the binding held another:
  - A `@bind-Value:set` that **transforms** the value (clamp, round, `?? fallback`) sends something
    else back. Clearing a field bound with `v => _days = (int)Math.Clamp(v ?? 1, 1, 365)` emitted
    `null`, the parent turned it into `1`, and that `1` was read as an external push - refilling the
    box the user had just emptied.
  - On a Blazor Server circuit a render batch can be built before a later keystroke and delivered
    after it, so the value arriving is sometimes an **earlier** one of the component's own. That was
    also read as external, rewinding the display to a number the user had already typed past.

  Reconciliation is now driven by whether the component committed since the last parameter sync,
  and an unflagged change only rewrites the text when the text does not already spell that value -
  so a partial decimal (`"40."` while `40` lands) survives too. A genuine parent push while the
  field has focus still reaches the display, which is what alpha.7 set out to fix.
- **Base**: a stepper hold now continues from where its own first press landed. The repeat loop
  seeded its running total by re-reading the controlled value, which in controlled mode still holds
  the pre-press number until the parent's render arrives, so the first repeat tick recomputed the
  step the press had already made and the hold lost a step.

## 0.1.0-alpha.7 - 2026-07-24

### Added
- **Ui**: `BzColorPicker` - a composable color picker family: saturation/value area
  (`BzColorArea` over the new headless `BaseColorArea`), hue and alpha sliders, eye dropper,
  format select (HEX/RGB/HSL/HSB), per-channel inputs riding `BzInputNumber`, opacity input,
  preview, swatches and saved colors.
- **Ui**: `BzQrCode` - SVG QR codes with module/eye styling, colors and gradients, quiet zone,
  error-correction levels, and center content (logo) support.
- **Ui**: `BzTree` marquee (drag-to-select) support.
- **Base**: select-level `Disabled` on `BaseSelect`.

### Changed
- **Cli**: `add --update` and `add --upgrade` merged into `add --update`, which now does both:
  bump the Blaizio NuGet packages to the tool's pinned versions, then re-pull the installed
  components (all of them, or just the ones you name). The split shipped a foot-gun: components
  lean on the package's JS/CSS assets, so a source-only sync could pair new components with an
  older package (a component importing a `dist/` module its package doesn't carry fails at
  runtime). `--upgrade` remains as a hidden alias so existing scripts keep working. The v1 → v3
  migration now bumps the packages too.

### Fixed
- **Base**: `BaseInputNumber` survives controlled lag on Blazor Server. Two defects: (1) while
  the input had focus, a parent-pushed `Value` never reached the displayed text - the field
  showed one number while the bound value held another. The text now resyncs whenever the
  incoming value is not the echo of the component's own last emit, focused or not. (2) the
  press-and-hold repeat loop re-read the controlled value every 60ms tick, but that value only
  updates with a render - which on a Server circuit can lag well behind the cadence - so every
  tick redid the same stale math and a whole hold netted a single step. The loop now accumulates
  a local pending value and stops at the bounds itself.
- **Base**: JS-to-.NET callbacks no longer surface "There is no tracked object with id ..." as an
  uncaught promise rejection when they race component teardown - e.g. a dialog button that
  navigates: the focus scope's unmount round-trip (and any late animationend / dismiss / scroll
  report) can arrive after the component disposed its `DotNetObjectReference`. Every interop
  module now routes callbacks through a guard (`ts/interop.ts`) that swallows exactly the
  disposed-reference rejection - the component is gone, there is nobody left to notify - and
  keeps every other failure as loud as before.
- **Base**: closing a floating surface no longer throws `ObjectDisposedException` when the close
  races the component's own disposal - e.g. a dropdown-menu item that navigates: the menu's exit
  animation reports back while navigation is already tearing the component down, and both paths
  disposed the same JS positioning reference. Every floating surface's dispose helpers (dropdown
  menu, popover, tooltip, combobox, select, dialog content + overlay, hover card, collapsible,
  table of contents) now claim their reference synchronously before awaiting - the second caller
  no-ops - and swallow `ObjectDisposedException` alongside the existing circuit-gone guard.
- **Ui**: `--input` gained contrast against its surface in every palette - the light value dropped
  from lightness `0.91` to `0.88` and the dark value rose from `0.325` to `0.36`. Input borders,
  and everything else deriving from the token, now read as an edge rather than a suggestion.
- **Ui**: interactive states that leaned on `--muted` over `--background` (barely two percent
  apart in light mode) moved to `--accent`, so they are actually visible: pagination link hover,
  outline-toggle hover, and the outline/ghost bubble action hovers. Data-table column-header
  hover moved to the existing `color-mix(..., var(--foreground) 5%)` hover step in the skins that
  had no visible treatment (ember, spark, forge, wisp, aura, flint).
- **Ui**: the pagination link's active page shows its border again. The shared
  `[data-active="true"]` rule and each skin's base `.bz-pagination-link` rule carry the same
  specificity, so the skin's `border-transparent` - imported later - silently won and the current
  page lost its outline in every skin. The active treatment now lives in the skin rules.
- **Ui**: `BzDataTable` row clicks are delegated in JS, so a click can be attributed to its
  target. Clicking a row now toggles that row's selection when `Selectable`, while clicks that
  originate inside an interactive cell control (the selection checkbox, a row-actions trigger, a
  link) stay that control's own. `OnRowClick` gains the same treatment, finally honouring its
  documented "anywhere outside an interactive cell control" contract - as a Blazor `@onclick` on
  the `tr` it had fired for every click that bubbled out of the row.
- **Base**: a tooltip trigger opens on focus only when the focus is *keyboard* focus
  (`:focus-visible`). Focus also lands on a trigger programmatically - a dropdown or dialog
  sharing that trigger restores focus there when it closes - which popped the tooltip open under
  a pointer nowhere near it. Keyboard users still get the immediate, no-delay open, including
  when Escape closes an overlay back onto the trigger.

## 0.1.0-alpha.6 - 2026-07-18

### Fixed
- **Base**: the `.blaizio/` contract now materializes **before** the CLI-wired Tailwind compile.
  Both hook `BeforeBuild`, but the Tailwind target lives in the project file and package targets
  import after it, so on a fresh clone or worktree the Tailwind compile ran first and failed on
  the missing `.blaizio/` imports before the contract could materialize. The materialize target
  now also declares itself before `BlaizioTailwindFetch`/`BlaizioTailwindBuild` - a fresh checkout
  heals on its first `dotnet build`, no manual copying.
- **Ui**: `BzPieChart`, `BzRadarChart`, and `BzRadialBarChart` tooltips gained the edge-aware
  placement `BzChart` already had - near the chart's start/end the panel hugs that edge instead
  of centring, and with no room above it flips below the anchor - so cards that clip overflow no
  longer cut the tooltip. The single-row pie/radial panels also dropped their forced minimum
  width so they fit beside small charts.

### Added
- **Base**: opt-in theme-scope portal frames. An ancestor with
  `data-bz-portal-frame="some classes"` makes its portaled surfaces re-home into a shared
  `<body>`-level `display: contents` container carrying those classes instead of bare `<body>`,
  so ancestry-scoped CSS (theme pins, scoped skins) survives the portal move. The frame is
  created on demand and removed when its last surface leaves; nested portals resolve to the same
  frame.

### Changed - floating surfaces portal to the body
- **Base**: every floating surface - tooltip, popover, hover card, dropdown menu, context menu,
  menubar menu, select, combobox, and the declarative dialog/alert-dialog/sheet/drawer content and
  overlay - now **moves itself to `document.body` while open** and returns to its place in the DOM
  before unmounting. A surface declared inside an ancestor that creates a stacking context
  (`position: fixed`/`sticky` with a z-index, `transform`, `filter`, `backdrop-blur`,
  `container-type`, `opacity` below 1) or an overflow clip can no longer be painted over or cut at
  that ancestor's edge. Positioning, focus, dismissal, and animations are unchanged; submenus stay
  inside their (portaled) parent surface. No setup: upgrade `Blaizio.Base` and re-pull components.
- **Breaking (styling)**: because the open surface is a `<body>`-level node, **ancestor CSS
  selectors no longer reach it** (e.g. `.my-panel .bz-tooltip-content { ... }` or a parent's
  `[data-theme]` scope). Style floating content through its own classes/attributes, target
  `:root`, or set `Inline` (below) to restore in-place rendering. Theme, skin, and dark classes on
  `<html>` are unaffected.

### Added
- **Base/Ui**: `Inline` parameter (default `false`) on every floating content component -
  `BzTooltipContent`, `BzPopoverContent`, `BzHoverCardContent`, `BzDropdownMenuContent`,
  `BzContextMenuContent`, `BzMenubarContent`, `BzSelectContent`, `BzComboboxContent`,
  `BzDialogContent`, `BzDialogOverlay`, `BzAlertDialogContent`, `BzSheetContent`,
  `BzDrawerContent` (and their `Base*` counterparts). `Inline="true"` renders in place (today's
  pre-alpha.5 behavior) - for CSS-containment parents, print, or tests that assert on local markup.
- **Base**: a portaled surface stamps its own `dir` attribute from the cascaded
  `BzDirectionProvider` direction, so a subtree-scoped RTL keeps applying to body-level surfaces.

## 0.1.0-alpha.4 - 2026-07-16

### Changed - CSS layout v3
- **Registry/Ui**: component classes are now **inlined per skin** at registry build - `blaizio
  build` merges the shared + skin `@apply` lists (TailwindMerge semantics) and substitutes every
  `bz-*` token in the shipped `.razor`/`.cs` source, emitting per-skin variants under
  `r/{style}/`. No skin stylesheet ships to consumers; `bz-*` classes are gone from output.
- **Base**: ships the static contract (`blaizio.css` - `data-*` variants, keyframes,
  chart/toast machinery - plus the vendored `animate.css`) and `buildTransitive` MSBuild targets
  that **materialize them into the consumer's gitignored `.blaizio/`** before every build.
  Opt out with `<BlaizioMaterializeContract>false</...>`, redirect with `<BlaizioContractDir>`.
- **CLI**: v3 layout throughout - `init` scaffolds ONE user-owned tokens file
  (`Styles/app.css`: Tailwind input, `:root`/`.dark` values with preset/chart/radius/fonts baked
  as plain editable values, `@theme inline` map) and only ever patches it surgically afterwards.
  `add` pulls the recorded skin's inlined variants; a full `apply` (skin swap) re-installs the
  ledgered components (confirm-gated); `update` has no styling leg left; `uninstall` strips by
  record. New config keys: `css`, `cssCreated`, `ejected`.
- **CLI**: `update` runs the confirm-gated **v1 → v3 migration** when it sees the old
  `Styles/blaizio/` layout: components re-install from the skin's inlined variants first, the
  tokens file is composed from the project's v1 sheets (user values survive; preset/fonts/pointer
  folded in), then `Styles/blaizio/` is deleted.
- **Ui**: `--primary-button` retired - the default button's dark fill derives from `--primary`
  via a `color-mix` formula (WCAG AA-verified across presets).
- **Docs**: v3 story (Installation/Theming/CLI rewritten), per-skin Source view on component
  pages, Get Code dialog emits the merged comment-free token block.

### Added
- **CLI**: `blaizio eject` - copies the materialized contract into the tokens file, drops the
  `.blaizio/` imports and sets `"ejected": true`; the styling plumbing is frozen and yours.
  Confirm-gated, irreversible, clean no-op on a second run.
- **CLI**: `blaizio contrast` - WCAG AA audit of the tokens file's color pairs (light + dark,
  focus ring at 3:1, the derived dark button fill); exits `1` on failures, `--json` for CI.

### Changed
- **CLI**: a `/create` code's chart palette and radius now bake directly into the tokens file's
  `:root` (exact-declaration rewrite). The selection is recorded in `blaizio.json` (`chart`,
  `radius`) so `update`/`apply` re-runs keep it. `apply --only tokens` patches values in place.
- **CLI**: `preset resolve` round-trips the full `/create` code, including fonts, chart and
  radius.

### Fixed
- **Ui**: accordion/collapsible close blink - the `@theme` animation shorthands in `blaizio.css`
  dropped the `--tw-animation-fill-mode` hook, so the exit animation ran with `fill-mode: none`
  and the closed panel snapped back to full height until the unmount landed. The shorthands now
  carry `var(--tw-animation-fill-mode, none)`, letting the `[data-state='closed']` forwards pin
  hold the final frame.

## 0.1.0-alpha.3 - 2026-07-14

### Fixed
- **Base**: collapsible/accordion panels re-measure their content height while open
  (`MutationObserver` + window-resize, rAF-throttled) - content that grew after opening
  (async loads, expanding composers) no longer causes the close animation to start from a stale
  height. The measurement also lifts the inner wrapper's height pin first so fresh content can't
  report the old value back.

### Changed
- **CLI**: tool bumped in lockstep; installs pin `Blaizio.Base`/`Blaizio.Icons` `0.1.0-alpha.3`.

## 0.1.0-alpha.2 - 2026-07-11

### Fixed
- **Ui/Base**: floating-surface close blink (dialogs, dropdowns, popovers) - exit animations now
  hold their final frame via `[data-state='closed'] { --tw-animation-fill-mode: forwards; }` in
  `shared.css`, so a slow unmount round-trip can no longer flash the surface back to visible.

### Added
- **CLI**: `apply [preset]` (preset name or `/create` code, `--only theme,fonts,tokens`);
  `preset decode/resolve/url/open`; `registry add/validate` with `@namespace/component`
  resolution; `docs <components...>`; commander-style help (`BlaizioHelpProvider`); `add`
  absorbed `--update/--upgrade/--diff/--view`; `search` absorbed `list`.
- **CLI**: `uninstall` (formerly `deinit`) is undo-by-record - components, NuGet packages,
  `@using`s and config are removed exactly as tracked in `blaizio.json`; user files survive.

## 0.1.0-alpha.1 - 2026-07

Initial pre-release.

- **Blaizio.Base**: headless primitives (behavior, ARIA, keyboard, `data-state` contract) with
  TypeScript interop shipped as `_content/Blaizio.Base/dist/*` - consumers copy no JS.
- **Blaizio.Icons**: Tabler icons as a tree-shakeable SVG component.
- **Blaizio.Ui**: 61 styled Tailwind v4 components, 8 skins, 9 color palettes, RTL via logical
  properties, light/dark token model - distributed as source through the registry.
- **Blaizio.Cli**: `init` (templates incl. Showcase, Tailwind v4 wiring with standalone-binary
  auto-fetch or bundler detection, host-page patching), `add` (transitive registry dependencies,
  namespace rewrite, install ledger), `search/view/info/docs`, `build` (registry compiler),
  sha256-verified Tailwind standalone downloads, per-user binary cache, `--json` contract on
  every command.
- **Docs**: component documentation site hosting the registry (`/r`) and the `/create`
  configurator with shareable preset codes.
