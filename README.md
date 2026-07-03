# Omnifin Native Windows App

OmnifinNative is a Windows desktop client for Omnifin. It connects to a server that the user provides at runtime and stores only local session state on the device.

## What the user must provide

On first launch, the app asks for:

- Server URL
- Username
- Password
- Admin login toggle, if the account is an admin account

These values are not hardcoded in the app source. The server URL is saved locally on the machine so the next launch can reuse it, and the login session is stored in Windows Credential Manager on that device only.

## Privacy and safe distribution

This repo should not contain any of the following:

- Server URLs for a private environment
- Usernames or passwords
- Refresh tokens or other auth secrets
- Customer or user records

If you add new settings, keep them user-provided at runtime or store them locally on the machine only. Do not commit secrets to the repository.

## Build an exe for Windows users

The repository includes a GitHub Actions workflow that publishes a Windows x64 build artifact. It uses a Release publish for the app and packages the output as a zip file that contains the executable and its runtime files.

To run it, open the Actions tab in GitHub and start the workflow manually, or push to the default branch if you want it to run automatically.

## Local development

1. Open the OmnifinNative project in Visual Studio or VS Code.
2. Build with `dotnet build`.
3. Run the app and enter your own server URL and credentials on the login screen.

## Notes for maintainers

- The server URL is stored locally through Windows Credential Manager, not in source control.
- Login tokens are also stored locally through Windows Credential Manager.
- If you want to change the startup experience, keep the login page as the place where server URL and credentials are entered.
