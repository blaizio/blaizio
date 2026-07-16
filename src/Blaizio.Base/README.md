# Blaizio.Base

Headless, unstyled Blazor UI primitives: behavior, accessibility and state - **not one line of
CSS**. The foundation the styled Blaizio components are built on, and a standalone toolkit for
building your own design system.

## What you get

- Primitives for accordions, dialogs, dropdowns, popovers, comboboxes, calendars, carousels,
  tooltips, trees, and more - each rendering the correct element with the correct ARIA roles and
  keyboard behavior.
- A `data-state` / `data-*` attribute contract on every interactive part
  (`data-state="open"`, `data-[side=top]`, …) - style the states with whatever you like:
  Tailwind, plain CSS, CSS modules.
- `RenderAs` on every interactive part: render your own element and splat the provided
  attributes - ARIA, `data-*` and event handlers ride along.
- JS interop (focus scopes, dismissable layers, height measurement, typeahead…) served from
  `_content/Blaizio.Base/dist/` - ES modules behind typed services, no global `window` state,
  nothing to copy into your app.

## Setup

```csharp
builder.Services.AddBlaizio();          // optionally AddBlaizio(o => ...)
```

```razor
@using Blaizio
```

Prerendering-safe: interop attaches after render and every reference is disposed with the
component.

## Relationship to the rest of Blaizio

The styled layer (`Blaizio.Ui`, copied into your app by the `blaizio` CLI) dresses these
primitives with Tailwind classes. Mix freely: styled components where they fit, raw primitives
where your design diverges - same behavior and accessibility either way.

## Documentation

Visit https://blaiz.io/docs/base to view the documentation.

## License

Licensed under the [MIT license](https://github.com/blaizio/blaizio/blob/main/LICENSE.md).
