# MAUI theming mechanics: tokens, dark mode, custom fonts

**Date:** 2026-08-08 · **Ticket:** REN-15 · **Scope:** .NET 10 MAUI, Android + iOS, localized en/pl/ru · **Sources:** Microsoft Learn (net-maui-10.0 moniker), dotnet/maui repo (source + issues), google/fonts repo (METADATA.pb), developer.android.com

---

## 1. Design tokens: ResourceDictionary organization, Static vs Dynamic, AppThemeBinding

### 1.1 Canonical ResourceDictionary organization

- The default .NET MAUI template merges two style dictionaries in `App.xaml`, in this order: `Resources/Styles/Colors.xaml` then `Resources/Styles/Styles.xaml` (plus a conditional `AppStyles.xaml` when sample content is included). Verified against the template source: [dotnet/maui `src/Templates/src/templates/maui-mobile/App.xaml`](https://github.com/dotnet/maui/blob/main/src/Templates/src/templates/maui-mobile/App.xaml). Ordering matters because `Styles.xaml` consumes color keys defined in `Colors.xaml` — colors must be merged first.
- Resources can live at view, layout, page, or application level; scope is the element and its children ([Resource dictionaries — Create resources](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/resource-dictionaries?view=net-maui-10.0#create-resources)).
- **Lookup rules** ([Resource lookup behavior](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/resource-dictionaries?view=net-maui-10.0#resource-lookup-behavior)): the element's own dictionary is checked first, then the visual tree is searched **upwards** parent-by-parent, then the application-level dictionary; if still not found a `XamlParseException` is thrown. First match wins, so resources defined lower in the tree override app-level ones with the same key ([Override resources](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/resource-dictionaries?view=net-maui-10.0#override-resources)).
- **MergedDictionaries precedence** ([Merge resource dictionaries](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/resource-dictionaries?view=net-maui-10.0#merge-resource-dictionaries)): when keys collide, precedence is (1) resources local to the dictionary, (2) resources in `MergedDictionaries` **in the reverse order they are listed** (i.e., the last merged dictionary wins among merged ones). Only one `MergedDictionaries` property-element tag is allowed per dictionary. The deprecated `MergedWith` property must not be used.
- Stand-alone dictionaries (no code-behind): remove `x:Class`; from .NET 9/10 they are XAML-compiled by default unless `<?xaml-comp compile="false" ?>` is specified; build action must be **MauiXaml** ([Stand-alone resource dictionaries](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/resource-dictionaries?view=net-maui-10.0#stand-alone-resource-dictionaries)). `ResourceDictionary.Source` can only be set from XAML. Merging a dictionary from another assembly requires build action MauiXaml, a code-behind file, and `x:Class`.
- **Performance rule:** "Resources that are specific to a single page shouldn't be included in an application level resource dictionary, as such resources will then be parsed at app startup instead of when required by a page" ([Resource dictionaries — Consume resources](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/resource-dictionaries?view=net-maui-10.0#consume-resources); expanded in [Improve app performance — Reduce the application resource dictionary size](https://learn.microsoft.com/en-us/dotnet/maui/deployment/performance?view=net-maui-10.0#reduce-the-application-resource-dictionary-size)).
- From code, prefer `Resources.TryGetValue("Key", out var value)` over the indexer — the indexer can throw `KeyNotFoundException` when a merged dictionary mixes file-sourced and inline resources ([doc note referencing dotnet/maui PR #11214](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/resource-dictionaries?view=net-maui-10.0#access-resources-by-key-from-code), [dotnet/maui#11214](https://github.com/dotnet/maui/pull/11214)).

### 1.2 StaticResource vs DynamicResource

- "While the `StaticResource` markup extension performs a **single dictionary lookup**, the `DynamicResource` markup extension **maintains a link to the dictionary key**. Therefore, if the dictionary entry associated with the key is replaced, the change is applied to the visual element." ([Resource dictionaries — Consume resources](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/resource-dictionaries?view=net-maui-10.0#consume-resources))
- Official guidance: "Use the `StaticResource` markup extension if your app doesn't need to change themes dynamically at runtime. If you anticipate switching themes while the app is running, use the `DynamicResource` markup extension" ([Theme an app](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/theming?view=net-maui-10.0)). DynamicResource is *required* only where the value under a key is replaced at runtime (custom runtime theming via swapping MergedDictionaries).
- Consumption pattern from the theming doc: theme values inside `Style` setters use `{DynamicResource …}`; the styles themselves are consumed with `{StaticResource …}` ([Theme an app — Consume theme resources](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/theming?view=net-maui-10.0#consume-theme-resources)).
- Runtime theme swap = `Application.Current.Resources.MergedDictionaries.Clear()` then `.Add(new DarkTheme())` ([Theme an app — Load a theme at runtime](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/theming?view=net-maui-10.0#load-a-theme-at-runtime)).
- Runtime cost note: docs warn that "Searching resource dictionaries can be a computationally intensive task if an app contains multiple, large resource dictionaries" ([Merge resource dictionaries tip](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/resource-dictionaries?view=net-maui-10.0#merge-resource-dictionaries)). A `DynamicResource` additionally keeps a live subscription to key changes (its documented mechanism), so it carries ongoing bookkeeping that `StaticResource` does not. A related framework-side leak in that bookkeeping (`ResourceDictionary.ValuesChanged`) was fixed in [dotnet/maui#36253](https://github.com/dotnet/maui/pull/36253) (June 2026).

### 1.3 AppThemeBinding, RequestedTheme/UserAppTheme, and pitfalls

**Mechanics** (all from [Respond to system theme changes](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/system-theme-changes?view=net-maui-10.0)):

- `{AppThemeBinding Light=…, Dark=…, Default=…}` selects a resource by current theme and "objects that consume these resources are automatically updated if the system theme changes while an app is running." `Default` is the content property.
- Code equivalents: `SetAppThemeColor(BindableProperty, lightColor, darkColor)` and `SetAppTheme<T>(BindableProperty, lightValue, darkValue)` extension methods on `VisualElement`.
- `Application.Current.RequestedTheme` (get: `Unspecified`/`Light`/`Dark`), `Application.Current.UserAppTheme` (set to force a theme app-wide; set `AppTheme.Unspecified` to follow the OS), `Application.Current.RequestedThemeChanged` event.
- Minimum OS: iOS 13+, Android 10 (API 29)+.
- **Android requirement:** "To respond to theme changes on Android your `MainActivity` class must include the `ConfigChanges.UiMode` flag in the `Activity` attribute" (template includes it by default).
- **AppThemeBinding inside Styles/Setters works** — the doc's own examples put `{AppThemeBinding Light={StaticResource …}, Dark={StaticResource …}}` in `Setter.Value`, and even `{AppThemeBinding Light={DynamicResource …}}` is shown as valid.

**Documented/known pitfalls (dotnet/maui issues):**

- *Memory:* the official [Memory Leaks wiki](https://github.com/dotnet/maui/wiki/Memory-Leaks) does **not** list AppThemeBinding as a leak source; its named culprits are C# events with circular references, delegates whose target points back at parents, and iOS/Catalyst native/managed reference cycles. Per maintainer comments on [PR #24465](https://github.com/dotnet/maui/pull/24465), the team deliberately **stopped subscribing AppThemeBinding to `Application.Current.RequestedThemeChanged` "for perf reasons"**; theme changes now propagate to elements through the tree ([dotnet/maui#19931 "Propagate the app theme to all children"](https://github.com/dotnet/maui/pull/19931)). Consequence: widely-circulated claims that AppThemeBinding leaks pages/handlers describe older behavior; this research found no currently-open dotnet/maui issue asserting an AppThemeBinding page/handler leak (searched `AppThemeBinding leak`, `AppThemeBinding memory`, `RequestedThemeChanged leak`). Note this propagation design has a functional cost: bindings on objects *outside* the element tree don't get notified — see next bullet. If you subscribe to `RequestedThemeChanged` yourself, standard event-unsubscription hygiene applies ([Improve app performance — Unsubscribe from events](https://learn.microsoft.com/en-us/dotnet/maui/deployment/performance?view=net-maui-10.0#unsubscribe-from-events)).
- *AppThemeBinding on non-Elements does not update:* `PlatformBehavior`/non-Element bindables never receive theme changes; the attempted fix was rejected pending redesign ([#24444](https://github.com/dotnet/maui/issues/24444), [PR #24465, closed unmerged](https://github.com/dotnet/maui/pull/24465)).
- *VisualStateManager interference:* AppThemeBinding setters fight VSM setters ([#17898, open](https://github.com/dotnet/maui/issues/17898)).
- *Defining an AppThemeBinding value as a keyed resource and consuming via StaticResource fails* ([#21693, open](https://github.com/dotnet/maui/issues/21693)).
- *Style `BasedOn` inheritance with AppThemeBinding was broken, fixed 2025* ([#31280, closed](https://github.com/dotnet/maui/issues/31280)).
- *Android regression: Entry/Editor AppThemeBinding colors reset to defaults on theme change* — fixed ([#31889](https://github.com/dotnet/maui/issues/31889), [#31891](https://github.com/dotnet/maui/pull/31891)).
- *iOS CollectionView virtualization: AppThemeBinding not applied consistently to recycled cells* — fixed ([#31554, closed](https://github.com/dotnet/maui/issues/31554)).
- *Theme-change doesn't restyle some controls:* e.g. SwipeItem background not updating on theme change (fixed in [#36271](https://github.com/dotnet/maui/pull/36271)); Android dynamic style swap leaves stale colors ([#6183, open since 2022](https://github.com/dotnet/maui/issues/6183)); iOS binding to theme not reacting for BindableProperty scenarios ([#15573, open](https://github.com/dotnet/maui/issues/15573)).
- *.NET 10 XAML source generator:* `AppThemeBindingExtension` is internal, which blocked `MauiXamlInflator=SourceGen`; source generation for AppThemeBinding was explicitly **disabled for .NET 10** and the public API deferred to .NET 11 ([#32665](https://github.com/dotnet/maui/issues/32665), [#33101](https://github.com/dotnet/maui/pull/33101), [#32678](https://github.com/dotnet/maui/issues/32678)). If you enable the .NET 10 XAML source generator, AppThemeBinding falls back to the runtime path.

**Practical takeaway:** use `AppThemeBinding` (not DynamicResource swapping) for light/dark since the OS drives it and MAUI re-applies values automatically; reserve DynamicResource for user-selectable custom themes beyond light/dark.

---

## 2. Custom fonts

### 2.1 Registration pipeline

- TTF and OTF are the supported formats. Fonts dropped into `Resources\Fonts` get the **MauiFont** build action automatically; a wildcard item `<MauiFont Include="Resources\Fonts\*" />` registers everything ([Fonts — Register fonts](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/fonts?view=net-maui-10.0#register-fonts)).
- Registration happens in `MauiProgram.CreateMauiApp()` via `.ConfigureFonts(fonts => fonts.AddFont("Nunito-Bold.ttf", "NunitoBold"))` — first arg filename, second an **optional alias**. Consume via `FontFamily` set to the filename-without-extension or the alias ([Fonts — Consume fonts](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/fonts?view=net-maui-10.0#consume-fonts)).
- Fonts embedded in a **class library**: mark the font as `EmbeddedResource` and register with `AddEmbeddedResourceFont(this IFontCollection fontCollection, Assembly assembly, string filename, string? alias = default)` ([API reference](https://learn.microsoft.com/en-us/dotnet/api/microsoft.maui.hosting.fontcollectionextensions.addembeddedresourcefont?view=net-maui-10.0)). Historical pain point when misconfigured: [dotnet/maui#3584](https://github.com/dotnet/maui/issues/3584).
- Default app font is Open Sans. `FontAttributes` supports only `None`, `Bold`, `Italic` — there is **no cross-platform FontWeight API** ([Fonts](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/fonts?view=net-maui-10.0)). `FontAutoScalingEnabled` is on by default (OS text-scaling accessibility).

### 2.2 Variable fonts: NOT supported — ship static instances

- Variable font support is an **open feature request in the Backlog milestone** with no API surface for axis/named-instance selection: [dotnet/maui#16604 "Variable font support"](https://github.com/dotnet/maui/issues/16604) (labels `area-fonts`, `proposal/open`). A related request for FontImageSource axes (Fill/Optical size/Grade for Material Symbols) is also open: [#21772](https://github.com/dotnet/maui/issues/21772).
- Because `AddFont` exposes no variation-settings parameter and `FontAttributes` has no weight axis, a variable-weight TTF can only render at whatever single instance the platform picks by default; you cannot address wght=600 etc. from MAUI. **Conclusion: ship static-instance TTFs per weight** (e.g., `Inter_18pt-Regular.ttf`, `Inter_18pt-SemiBold.ttf`) and register each under its own alias. (The "no API" part is verified from the docs + #16604; the "platform default instance" rendering behavior is the practical consequence, not separately documented by MS.)

### 2.3 Font candidates — coverage verified from google/fonts METADATA.pb

All license/subset facts below come from the authoritative `METADATA.pb` files in [github.com/google/fonts](https://github.com/google/fonts). `latin-ext` covers Polish diacritics (ĄĆĘŁŃÓŚŹŻ ąćęłńóśźż); `cyrillic` covers the Russian alphabet incl. Ёё. (For belt-and-braces, the specimen "Glyphs" tab on fonts.google.com can confirm individual codepoints.)

| Font | License | cyrillic | cyrillic-ext | latin | latin-ext | Variable axes | Source |
|---|---|---|---|---|---|---|---|
| **Nunito** | OFL | ✅ | ✅ | ✅ | ✅ | wght 200–1000 | [METADATA.pb](https://github.com/google/fonts/blob/main/ofl/nunito/METADATA.pb) |
| **Manrope** | OFL | ✅ | ✅ | ✅ | ✅ (+greek) | wght 200–800 | [METADATA.pb](https://github.com/google/fonts/blob/main/ofl/manrope/METADATA.pb) |
| **Inter** | OFL | ✅ | ✅ | ✅ | ✅ (+greek, greek-ext) | opsz 14–32, wght 100–900 | [METADATA.pb](https://github.com/google/fonts/blob/main/ofl/inter/METADATA.pb) |
| **Rubik** | OFL | ✅ | ✅ | ✅ | ✅ (+arabic, hebrew) | wght 300–900 | [METADATA.pb](https://github.com/google/fonts/blob/main/ofl/rubik/METADATA.pb) |
| **Comfortaa** | OFL | ✅ | ✅ | ✅ | ✅ (+greek) | wght 300–700 (Display category) | [METADATA.pb](https://github.com/google/fonts/blob/main/ofl/comfortaa/METADATA.pb) |
| **Mulish** | OFL | ✅ | ✅ | ✅ | ✅ | wght 200–1000 | [METADATA.pb](https://github.com/google/fonts/blob/main/ofl/mulish/METADATA.pb) |
| **PT Sans** | OFL | ✅ | ✅ | ✅ | ✅ | none (static 400/400i/700/700i) | [METADATA.pb](https://github.com/google/fonts/blob/main/ofl/ptsans/METADATA.pb) |

**Every candidate passes the en/pl/ru coverage bar.** Differentiators:

- **Nunito** — rounded terminals, warm/friendly; huge weight range (200–1000) so headings can go heavy. Cyrillic + latin-ext confirmed.
- **Inter** — designed for UI screens; excellent legibility at small sizes; widest script coverage of the group.
- **Manrope** — geometric-friendly, but weight tops out at 800 and no italics in the GF family.
- **Comfortaa** — categorized as *Display* in its METADATA.pb; too soft/round for body text, fine only for large headings.
- **Rubik/Mulish/PT Sans** — solid fallbacks; PT Sans is static-only, which is actually irrelevant here since MAUI needs static instances anyway.

**Recommended pairing: Nunito (headings/display) + Inter (body/UI).**
Proof of coverage: Nunito subsets `cyrillic, cyrillic-ext, latin, latin-ext` ([METADATA.pb](https://github.com/google/fonts/blob/main/ofl/nunito/METADATA.pb)); Inter subsets `cyrillic, cyrillic-ext, greek, greek-ext, latin, latin-ext` ([METADATA.pb](https://github.com/google/fonts/blob/main/ofl/inter/METADATA.pb)). Both OFL. Nunito's rounded forms give the "friendly/reassuring" tone for headings; Inter keeps dense body text (dates, document numbers, form labels) crisp on both platforms. Because of §2.2, download the **static** folder from each family's GitHub release/Google Fonts download and register ~5 files: `Nunito-Bold`, `Nunito-ExtraBold` (headings), `Inter-Regular`, `Inter-Medium`, `Inter-SemiBold` (body/emphasis).

---

## 3. App icon + splash pipeline

### 3.1 MauiIcon / MauiSplashScreen build items

All from [Add an app icon](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/images/app-icons?view=net-maui-10.0) and [Add a splash screen](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/images/splashscreen?view=net-maui-10.0):

- `<MauiIcon Include="…appicon.svg" ForegroundFile="…appiconfg.svg" Color="#…" TintColor="…" ForegroundScale="0.65" BaseSize="W,H" Resize="false" Condition="…per-platform…" />`. Only the **first** `<MauiIcon>` item is processed; conditional (per-platform) items must be declared before the unconditional fallback. `Include` (background) is required; `ForegroundFile` optional.
- **SVG is converted to PNG at build time**: "when adding an SVG file to your .NET MAUI app project, it should be referenced from XAML or C# with a *.png* extension. The only reference to the SVG file should be in your project file."
- Filenames must follow Android resource rules: lowercase, start/end with a letter, alphanumeric/underscore only.
- `BaseSize` defines the 1.0-scale-factor baseline; bitmaps without BaseSize are not resized; SVG dimensions are used as BaseSize when unspecified; `Resize="false"` stops resizing.
- **iOS caution:** "If you don't define a background color for your app icon the background is considered to be transparent on iOS and Mac Catalyst. This will cause an error during App Store Connect verification."
- Splash: `<MauiSplashScreen Include="…" Color="…" TintColor="…" BaseSize="…" />`; Android output is `maui_colors.xml` + `maui_splash_image.xml` under the default `Maui.SplashTheme` (don't override the theme); **on Android 12+ (API 31+) the splash shows only a centered icon** (Android splash-screen API); on iOS it becomes `MauiSplash.storyboard` set as `UILaunchStoryboardName` (don't set your own). Known quirk: iOS 16.4+ simulators don't show the splash for unsigned apps ([xamarin/xamarin-macios#18469](https://github.com/xamarin/xamarin-macios/issues/18469)).

### 3.2 SVG feature constraints in the Resizetizer

- The Resizetizer's SVG rendering stack is **Svg.Skia over SkiaSharp** (packages referenced from Resizetizer: `Svg.Skia`, `Svg.Custom`, `Svg.Model`, `ShimSkiaSharp`, `SkiaSharp`, `SkiaSharp.HarfBuzz`, `HarfBuzzSharp`, `ExCSS`, `Fizzler`) — see [dotnet/maui `src/SingleProject/Resizetizer/src/ResizetizerPackages.projitems`](https://github.com/dotnet/maui/blob/main/src/SingleProject/Resizetizer/src/ResizetizerPackages.projitems).
- MS Learn publishes **no list of unsupported SVG features**; the capability envelope is defined by Svg.Skia. Per the [Svg.Skia README](https://github.com/wieslawsoltes/Svg.Skia/blob/master/README.md): SVG 1.1 baseline plus selected SVG 2 static features; CSS (`class`/`style`/stylesheets/`@import`) supported; static `text` supported ("practical static text rendering"); "many SVG 1.1 filter primitives" supported with edge cases "tracked as partial"; masks/filters partially supported; **no animation, no browser DOM, JavaScript disabled by default**.
- Real-world Resizetizer SVG failures on record: stylesheet-parsing build failures ([#19401](https://github.com/dotnet/maui/issues/19401)), "Unable to allocate pixels for the bitmap" for oversized/degenerate SVGs ([#12109](https://github.com/dotnet/maui/issues/12109)), `ArgumentNullException` from malformed splash SVGs ([#4247](https://github.com/dotnet/maui/issues/4247)).
- **Practical constraint set for REN-15 assets:** export icon/splash SVGs flattened — paths only, no `<text>` (convert to outlines), no filters/masks, no embedded CSS `<style>` blocks, no external references. Everything in that subset is safely inside Svg.Skia's supported envelope and avoids the failure modes above.

### 3.3 Android adaptive icon

- Adaptive launcher icons are generated on Android 8.0+ **whenever `ForegroundFile` is set**; `Color` recolors a transparent background; `ForegroundScale` "is a percentage value so 0.65 will be translated as 65%" ([App icons — Adaptive launcher](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/images/app-icons?view=net-maui-10.0)).
- Safe-zone numbers (Android primary source, linked from the MAUI doc): layers are **108×108 dp**, the masked viewport shows the inner **66×66 dp**, the outer 18 dp per side is reserved for mask/parallax; "Use a logo that's at least 48x48 dp. It must not exceed 66x66 dp" ([Adaptive icons — developer.android.com](https://developer.android.com/develop/ui/views/launch/icon_design_adaptive)). So keep the foreground glyph within ~61% of the canvas — the docs' own `ForegroundScale="0.65"` example matches this.
- Monochrome themed-icon layer (`MonochromeFile` attribute on `<MauiIcon>`, Android 13+ themed icons) is documented under the **net-maui-11.0** moniker only — not available in .NET 10 ([App icons doc, net-maui-11.0 section](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/images/app-icons?view=net-maui-11.0#adaptive-launcher)).

### 3.4 iOS 18 dark/tinted icon variants

- **Not supported by `MauiIcon` as of .NET 9/10.** The request is an open Ideas discussion: [dotnet/maui discussion #25572 "[iOS18] Support for dark themed app icon in iOS 18"](https://github.com/dotnet/maui/discussions/25572) — no framework implementation. A follow-on proposal for the Xcode 26 "Liquid Glass" `.icon` format (which carries light/dark/tinted appearances) is tracked as [#35983](https://github.com/dotnet/maui/issues/35983) (labels `area-tooling`, `platform/ios`, `proposal/open`; no implementation linked).
- **Documented workaround** (from discussion #25572): bypass MauiIcon on iOS — create `Platforms/iOS/Resources/Assets.xcassets/AppIcon.appiconset` manually with light/dark/tinted PNGs and a `Contents.json` using `"appearances": [{"appearance": "luminosity", "value": "dark"|"tinted"}]`, then point `Info.plist`'s `XSAppIconAssets` at it (and use a `Condition`-scoped `<MauiIcon>` for Android only).

---

## 4. Handler-level styling gotchas: DatePicker, Picker, Entry

### 4.1 Handler customization mechanics (MS Learn)

From [Customize controls with handlers](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/customize?view=net-maui-10.0):

- Modify a handler's mapper with `PrependToMapping` / `ModifyMapping` / `AppendToMapping`. Key = `nameof(IEntry.Property)` to re-run on every property change, or an arbitrary key to run once at handler creation. **"Handler customizations are global… Once a handler is customized, it affects all controls of that type, everywhere in your app."** Scope per-instance by subclassing (`class BorderlessEntry : Entry`) and checking `if (view is BorderlessEntry)` inside the mapping.
- Native views for `Entry` in **.NET 10**: iOS/MacCatalyst `UITextField`, Android **`MauiAppCompatEditText`** (changed from `AppCompatEditText` — a .NET 10 breaking detail if you cast `PlatformView`; the change adds the `SelectionChanged` event) ([customize doc, net-maui-10.0 moniker](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/customize?view=net-maui-10.0#customize-a-control); [What's new in .NET 10 — Editor and Entry on Android](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0#editor-and-entry-on-android)).
- Use `HandlerChanged`/`HandlerChanging` events for native event subscribe/unsubscribe lifecycles.

### 4.2 Android underline on Entry/Picker/DatePicker

- There is **no cross-platform API** to remove the Material underline on Android text fields; the feature request has been open since 2022: [#7906 "Entry and Editor: option to disable borders and underline (focus)"](https://github.com/dotnet/maui/issues/7906). Related: [#36041 (can't remove text-to-underline offset)](https://github.com/dotnet/maui/issues/36041); a `BorderStyle` property proposal was closed ([#33210 ref via #33310](https://github.com/dotnet/maui/issues/33310)).
- The accepted approach is a handler mapper that clears the native background tint, e.g. `EntryHandler.Mapper.AppendToMapping("NoUnderline", (h, v) => { #if ANDROID h.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent); #endif });` — the mapper mechanism is the documented API ([customize doc](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/customize?view=net-maui-10.0)); the specific `BackgroundTintList` recipe is community-standard practice, not in MS Learn. The same applies to `PickerHandler`, `DatePickerHandler`, `TimePickerHandler` (all render as underlined `EditText`-family views on Android).
- Material 3 styling for Android DatePicker/TimePicker fields (outlined text fields, floating label, trailing icon) is **.NET 11 work in progress**, not .NET 10: [#36907](https://github.com/dotnet/maui/pull/36907).

### 4.3 iOS DatePicker/Picker presentation

- On iOS, `DatePicker`/`TimePicker`/`Picker` present as the classic **wheel/spinner input view sliding from the bottom** (UIPickerView/UIDatePicker as `inputView` of a text field), not the modern inline/popover calendar. Complaint closed as duplicate: [#22669 "DatePicker and TimePicker use outdated controls on iOS"](https://github.com/dotnet/maui/issues/22669) (dup of [#19879](https://github.com/dotnet/maui/issues/19879)).
- Forcing `UIDatePickerStyle.Compact` via a handler mapper is **broken since MAUI 8.0.3** and still open: [#20285 ".Net Maui DatePicker control's PreferredDatePickerStyle for iOS not working"](https://github.com/dotnet/maui/issues/20285) (regression from 7.0.52; the picker still opens from the bottom and shifts views inside CollectionView/ScrollView). Plan UI assuming the wheel presentation on iOS.

### 4.4 Android Picker/DatePicker dialog theming

- Android `Picker` opens an `AlertDialog`; `DatePicker` opens a `DatePickerDialog`. Known issues: the Android date-picker dialog is created inside `CreatePlatformView` rather than on demand ([#8537, open](https://github.com/dotnet/maui/issues/8537)); dialog button layout direction issues in RTL ([#14367](https://github.com/dotnet/maui/issues/14367), [#10490](https://github.com/dotnet/maui/issues/10490)); Picker doesn't respect app themes for outline/dropdown/indicator ([#33821, open](https://github.com/dotnet/maui/issues/33821)); CANCEL/OK button styling improvements tracked in [#31739](https://github.com/dotnet/maui/issues/31739). Dialog button colors follow the **native Android theme** (`Maui.SplashTheme`/AppCompat colors in `Platforms/Android/Resources/values`), not MAUI XAML resources — style them via Android `styles.xml`/`colors.xml` or dialog-level handler code.

### 4.5 Entry ClearButtonVisibility quirks

- Android: the clear button "is just a drawable set on the underlying TextView" and is **not reachable by accessibility services** ([#3384, open](https://github.com/dotnet/maui/issues/3384)).
- iOS: crash report with `ClearButtonVisibility="WhileEditing"` on device ([#21313, open](https://github.com/dotnet/maui/issues/21313)); clear-button tint appearing dimmed vs `TextColor` and tint not resetting when `TextColor` is set to null — fixed in 2026 servicing ([#35517](https://github.com/dotnet/maui/issues/35517), [#35076](https://github.com/dotnet/maui/issues/35076), fix PRs [#36472](https://github.com/dotnet/maui/pull/36472), [#35177](https://github.com/dotnet/maui/pull/35177)).

### 4.6 .NET 10-specific changes relevant to these controls

From [What's new in .NET MAUI for .NET 10](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0):

- **Picker**: new programmatic **Open/Close API**.
- **DatePicker/TimePicker nullable selection**: `Date` is now `DateTime?` (with nullable `MinimumDate`/`MaximumDate`), `Time` is now `TimeSpan?` — enables clearing values; a **breaking change** for bindings assuming non-nullable types.
- **Entry/Editor on Android**: native view is now `MauiAppCompatEditText` (adds `SelectionChanged`) — update any `PlatformView` casts in handler code.
- There is **no `CalendarDatePicker` control in .NET 10** — it does not appear in the .NET 10 what's-new or control docs; modern Material 3 picker styling is tracked under .NET 11 ([#36907](https://github.com/dotnet/maui/pull/36907)). No handler-architecture rewrite affects these three controls in .NET 10 (the Shell handler migration is .NET 11: [#37034](https://github.com/dotnet/maui/issues/37034)).

---

## Recommendations for RenewTheDoc

**Token structure to adopt**

1. Keep the template layout and extend it: `Resources/Styles/Colors.xaml` (primitive palette + semantic tokens) merged **before** `Resources/Styles/Styles.xaml` (implicit + keyed control styles) in `App.xaml` — matching the template ([template App.xaml](https://github.com/dotnet/maui/blob/main/src/Templates/src/templates/maui-mobile/App.xaml)); remember last-merged-wins among MergedDictionaries.
2. Express every color usage as `{AppThemeBinding Light={StaticResource TokenLight}, Dark={StaticResource TokenDark}}` inside style setters (documented-valid pattern), consume styles via `StaticResource`. Skip `DynamicResource` entirely unless a user-selectable custom theme (beyond light/dark) becomes a requirement — per MS guidance StaticResource is the default choice.
3. Set `Application.Current.UserAppTheme` from a settings screen (`Unspecified` = follow OS); keep `ConfigChanges.UiMode` in `MainActivity` (template default).
4. Don't enable `MauiXamlInflator=SourceGen` for theme-heavy XAML yet — AppThemeBinding source generation is disabled in .NET 10 ([#33101](https://github.com/dotnet/maui/pull/33101)).
5. Avoid AppThemeBinding on non-Element objects (Behaviors, plain bindables) — it silently won't update on theme change ([#24444](https://github.com/dotnet/maui/issues/24444)).
6. Page-specific resources go in the page's dictionary, not App.xaml (startup parse cost, [perf doc](https://learn.microsoft.com/en-us/dotnet/maui/deployment/performance?view=net-maui-10.0#reduce-the-application-resource-dictionary-size)).

**Font pairing (en/pl/ru proven)**

- **Nunito** for headings/display + **Inter** for body/UI. Both OFL; both list `cyrillic`, `cyrillic-ext`, `latin`, `latin-ext` subsets in their google/fonts METADATA.pb ([Nunito](https://github.com/google/fonts/blob/main/ofl/nunito/METADATA.pb), [Inter](https://github.com/google/fonts/blob/main/ofl/inter/METADATA.pb)) → Polish diacritics and Russian Cyrillic covered.
- Ship **static instances** (variable TTFs are not addressable in MAUI — [#16604](https://github.com/dotnet/maui/issues/16604)): `Nunito-Bold.ttf`, `Nunito-ExtraBold.ttf`, `Inter-Regular.ttf`, `Inter-Medium.ttf`, `Inter-SemiBold.ttf`, registered with aliases via `ConfigureFonts().AddFont(...)`; wildcard `<MauiFont Include="Resources\Fonts\*" />`.

**Icon/splash pipeline constraints**

- Author `appicon.svg` (background) + `appiconfg.svg` (foreground) as flattened path-only SVGs: no `<text>`, filters, masks, or CSS `<style>` blocks (Svg.Skia envelope + Resizetizer failure history: [#19401](https://github.com/dotnet/maui/issues/19401), [#12109](https://github.com/dotnet/maui/issues/12109)). Reference generated assets as `.png` in code.
- Always set `Color` on `<MauiIcon>` (transparent background fails App Store Connect validation) and keep the Android foreground glyph inside the 66/108 dp safe zone (`ForegroundScale` ≈ 0.6–0.65).
- Splash: single centered glyph on brand `Color`; on Android 12+ only a centered icon shows regardless of design.
- iOS 18 dark/tinted icons: if wanted, use the manual `Assets.xcassets` + `Contents.json` luminosity workaround from [discussion #25572](https://github.com/dotnet/maui/discussions/25572); MauiIcon can't do it in .NET 10.

**Control-styling gotchas checklist (Entry/Picker/DatePicker)**

- [ ] Android underline removal: one global mapper per handler (Entry/Picker/DatePicker/TimePicker) clearing `BackgroundTintList`; scope with control subclasses if any field should keep the native look ([customize doc](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/customize?view=net-maui-10.0), [#7906](https://github.com/dotnet/maui/issues/7906)).
- [ ] .NET 10: cast Android Entry `PlatformView` to `MauiAppCompatEditText`, not `AppCompatEditText`.
- [ ] Design for iOS wheel presentation of Date/Time/Picker; don't rely on `PreferredDatePickerStyle.Compact` ([#20285](https://github.com/dotnet/maui/issues/20285)).
- [ ] Android dialog buttons (OK/CANCEL) are themed via native Android styles, not MAUI resources ([#31739](https://github.com/dotnet/maui/issues/31739), [#33821](https://github.com/dotnet/maui/issues/33821)).
- [ ] Adopt nullable `DatePicker.Date` (`DateTime?`) — useful for "expiry date not set yet" flows in RenewTheDoc; audit bindings for the .NET 10 nullability change.
- [ ] Skip `ClearButtonVisibility` or accept its a11y gap on Android ([#3384](https://github.com/dotnet/maui/issues/3384)); verify iOS behavior on device ([#21313](https://github.com/dotnet/maui/issues/21313)).
- [ ] Verify theme switching on real pages with recycled lists (CollectionView + AppThemeBinding regressions history: [#31554](https://github.com/dotnet/maui/issues/31554), [#31889](https://github.com/dotnet/maui/issues/31889)).

---

## Sources

**Microsoft Learn (.NET MAUI, net-maui-10.0)**
- Resource dictionaries — https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/resource-dictionaries?view=net-maui-10.0
- Theme an app — https://learn.microsoft.com/en-us/dotnet/maui/user-interface/theming?view=net-maui-10.0
- Respond to system theme changes — https://learn.microsoft.com/en-us/dotnet/maui/user-interface/system-theme-changes?view=net-maui-10.0
- Fonts — https://learn.microsoft.com/en-us/dotnet/maui/user-interface/fonts?view=net-maui-10.0
- AddEmbeddedResourceFont API — https://learn.microsoft.com/en-us/dotnet/api/microsoft.maui.hosting.fontcollectionextensions.addembeddedresourcefont?view=net-maui-10.0
- App icons — https://learn.microsoft.com/en-us/dotnet/maui/user-interface/images/app-icons?view=net-maui-10.0
- Splash screen — https://learn.microsoft.com/en-us/dotnet/maui/user-interface/images/splashscreen?view=net-maui-10.0
- Customize controls with handlers — https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/customize?view=net-maui-10.0
- What's new in .NET MAUI for .NET 10 — https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0
- Improve app performance — https://learn.microsoft.com/en-us/dotnet/maui/deployment/performance?view=net-maui-10.0

**dotnet/maui repo**
- Template App.xaml — https://github.com/dotnet/maui/blob/main/src/Templates/src/templates/maui-mobile/App.xaml
- Resizetizer packages — https://github.com/dotnet/maui/blob/main/src/SingleProject/Resizetizer/src/ResizetizerPackages.projitems
- Memory Leaks wiki — https://github.com/dotnet/maui/wiki/Memory-Leaks
- Issues/PRs: [#16604](https://github.com/dotnet/maui/issues/16604), [#21772](https://github.com/dotnet/maui/issues/21772), [#25572 (discussion)](https://github.com/dotnet/maui/discussions/25572), [#35983](https://github.com/dotnet/maui/issues/35983), [#24444](https://github.com/dotnet/maui/issues/24444), [#24465](https://github.com/dotnet/maui/pull/24465), [#19931](https://github.com/dotnet/maui/pull/19931), [#36253](https://github.com/dotnet/maui/pull/36253), [#17898](https://github.com/dotnet/maui/issues/17898), [#21693](https://github.com/dotnet/maui/issues/21693), [#31280](https://github.com/dotnet/maui/issues/31280), [#31889](https://github.com/dotnet/maui/issues/31889), [#31554](https://github.com/dotnet/maui/issues/31554), [#15573](https://github.com/dotnet/maui/issues/15573), [#6183](https://github.com/dotnet/maui/issues/6183), [#33101](https://github.com/dotnet/maui/pull/33101), [#32665](https://github.com/dotnet/maui/issues/32665), [#7906](https://github.com/dotnet/maui/issues/7906), [#36041](https://github.com/dotnet/maui/issues/36041), [#36907](https://github.com/dotnet/maui/pull/36907), [#22669](https://github.com/dotnet/maui/issues/22669), [#19879](https://github.com/dotnet/maui/issues/19879), [#20285](https://github.com/dotnet/maui/issues/20285), [#8537](https://github.com/dotnet/maui/issues/8537), [#14367](https://github.com/dotnet/maui/issues/14367), [#33821](https://github.com/dotnet/maui/issues/33821), [#31739](https://github.com/dotnet/maui/issues/31739), [#3384](https://github.com/dotnet/maui/issues/3384), [#21313](https://github.com/dotnet/maui/issues/21313), [#35517](https://github.com/dotnet/maui/issues/35517), [#12109](https://github.com/dotnet/maui/issues/12109), [#19401](https://github.com/dotnet/maui/issues/19401), [#4247](https://github.com/dotnet/maui/issues/4247), [#11214](https://github.com/dotnet/maui/pull/11214), [#12039](https://github.com/dotnet/maui/issues/12039), [#21809](https://github.com/dotnet/maui/issues/21809)

**Fonts & platform**
- google/fonts METADATA.pb: [Nunito](https://github.com/google/fonts/blob/main/ofl/nunito/METADATA.pb), [Inter](https://github.com/google/fonts/blob/main/ofl/inter/METADATA.pb), [Manrope](https://github.com/google/fonts/blob/main/ofl/manrope/METADATA.pb), [Rubik](https://github.com/google/fonts/blob/main/ofl/rubik/METADATA.pb), [Comfortaa](https://github.com/google/fonts/blob/main/ofl/comfortaa/METADATA.pb), [Mulish](https://github.com/google/fonts/blob/main/ofl/mulish/METADATA.pb), [PT Sans](https://github.com/google/fonts/blob/main/ofl/ptsans/METADATA.pb)
- Svg.Skia README — https://github.com/wieslawsoltes/Svg.Skia/blob/master/README.md
- Android adaptive icons — https://developer.android.com/develop/ui/views/launch/icon_design_adaptive
- xamarin/xamarin-macios#18469 (iOS simulator splash) — https://github.com/xamarin/xamarin-macios/issues/18469
