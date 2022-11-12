using Microsoft.AspNetCore.Components;

namespace BlazorSvg.Client;

// In the .csproj file:
// <ItemGroup>
//   <AdditionalFile Include="Icons\*.svg" />
// </ItemGroup>
//
// Three matching files:
// * home.svg
// * layout-list.svg
// * plus.svg
//
// See NavMenu.razor for an example of their use.

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