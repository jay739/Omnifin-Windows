# Contributing to Omnifin-Windows

Thanks for considering a contribution. Issues, discussions and PRs are all
welcome.

## Getting set up

Windows 10 or 11 with the .NET 10 SDK and the Windows App SDK workload. `dotnet restore .\OmnifinNative\OmnifinNative.csproj -r win-x64`, then `dotnet build` with the same project and runtime.

## Running the checks

CI runs `dotnet format --verify-no-changes` and a Release build with `-warnaserror` on every PR. Run both locally first.

## Ground rules

- Open an issue or discussion before large changes, so effort isn't wasted.
- Keep PRs focused: one change per PR.
- New behavior needs a test where the repo has a test suite.
- CI must pass before merge.

## Licensing

By contributing you agree your contributions are licensed under the repo's
MIT license.
