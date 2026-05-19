# CE Accuracy and Firepower Analyzer

CE Accuracy and Firepower Analyzer is a RimWorld 1.6 in-game analysis tool for Combat Extended.

It provides a practical report-style interface for estimating hit chance, burst behavior, sustained firepower, armor-adjusted damage, near misses, and ballistic distribution under Combat Extended-style conditions. The calculations are approximations intended for comparison and planning, not a perfect reimplementation of Combat Extended internals.

## Requirements

- RimWorld 1.6
- Combat Extended

## Source Layout

- `About/` - RimWorld mod metadata and preview image.
- `Defs/` - RimWorld XML definitions, including the main button.
- `Languages/` - Chinese and English translations.
- `Source/` - C# source code for the analyzer.
- `Assemblies/` - compiled DLL output when building locally.

For a code map, see `Source/ARCHITECTURE.md`.

## Building

The project targets `.NET Framework 4.7.2`.

The local project file references RimWorld and Combat Extended assemblies by path. If your install paths differ, update the `HintPath` entries in:

`Source/CE_HitChanceCalculator.csproj`

Then build:

```powershell
dotnet build Source/CE_HitChanceCalculator.csproj -c Release
```

The compiled DLL is written to `Assemblies/`.

## License

This mod is licensed under CC BY-NC-SA 4.0.

See `LICENSE.md` and `NOTICE.md`.

