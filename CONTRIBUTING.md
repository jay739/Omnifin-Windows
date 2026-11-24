# Contributing to Omnifin-Windows

Thanks for considering a contribution. Issues, discussions and PRs are all
welcome.

## Getting set up

Windows 10 or 11 with the .NET 10 SDK and the Windows App SDK workload. `dotnet restore .\OmnifinNative\OmnifinNative.csproj -r win-x64`, then `dotnet build` with the same project and runtime.

## Running the checks

CI runs portable API contract checks, `dotnet format --verify-no-changes`, and a Release build with `-warnaserror` on every code PR. Run the checks locally before submitting changes.

The API checks compile the production client and models and exercise them against local HTTP fixtures. They use the .NET SDK without extra test-framework packages and run on Windows, Linux, or macOS without launching WinUI:

```sh
dotnet run --project OmnifinNative.ContractTests/OmnifinNative.ContractTests.csproj --configuration Release
```

Formatting and building the Windows app still require the Windows development environment described above.

## Ground rules

- Open an issue or discussion before large changes, so effort isn't wasted.
- Keep PRs focused: one change per PR.
- New behavior needs a test where the repo has a test suite.
- CI must pass before merge.

## Licensing

By contributing you agree your contributions are licensed under the repo's
MIT license.
