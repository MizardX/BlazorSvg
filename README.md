# BlazorSvg
SVGs source generated into Blazor Component

## Usage

1. Add the project to your solution
2. Add the attributes `OutputItemType="Analyzer" ReferenceOutputAssembly="false"` to the `<ProjectReference>` tag.
3. Add the `*.svg` files to use to into the project directory somewhere.
4. Include the `*.svg` files with one or more `<AdditionalFiles Include=".../*.svg" />` tags.
5. Create a file `Icon.cs` somewhere in the project, and add the following content:
```cs
public enum IconType // The enum type to use is specified by the `Type` property in the targeted class.
{
    Home, // => home.svg
    LayoutList, // => layout-list.svg
    Plus, // => plus.svg
    //NonExistant // Warning if matching <AdditionalFile> not found
}

[GenerateSvg(nameof(Type), nameof(AdditionalAttributes))] // Warning if properties not found
public partial class Icon : ComponentBase
{
    [Parameter] public IconType Type { get; set; } // Warning if not an enum type
    [Parameter(CaptureUnmatchedValues = true)] public IDictionary<string, object>? AdditionalAttributes { get; set; }
}
```
6. Use the component like so:
```html
<div>
  <Icon Type="IconType.Home" title="This is my home" style="color: red;"/>
  Home
</div>
```
and it will be rendered as an inline `<svg>` based on the `home.svg` image:
```html
<div>
  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="color: red;">
    <title>This is my home</tile>
    <path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z" />
    <polyline points="9 22 9 12 15 12 15 22" />
  </svg>
  Home
</div>
```