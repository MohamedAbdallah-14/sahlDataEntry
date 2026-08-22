# Third-party notices

## Runtime dependencies

**None.** The application has zero third-party NuGet package dependencies at runtime.
Excel (.xlsx) reading/writing and CSV parsing are implemented in-project over the .NET BCL
(`System.IO.Compression`, `System.Xml`).

## Platform

- .NET 10 runtime, shipped self-contained inside the published executable.
  License: MIT — https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
- Windows is a trademark of Microsoft Corporation. WPF/Win32 interop surface used:
  `SendInput`, `RegisterHotKey`, `UnregisterHotKey`, `CreateWindowExW`, `Get/SetWindowLongW`.

## Development-only packages (never shipped to users)

| Package | Version | License | Used for |
|---|---|---|---|
| Microsoft.NET.Test.Sdk | 17.13.0 | MIT (part of .NET tooling) | test host |
| xunit | 2.9.3 | Apache-2.0 | test framework |
| xunit.runner.visualstudio | 3.1.0 | Apache-2.0 | VS Test Explorer integration |
