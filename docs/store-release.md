# Releasing BeNGy

BeNGy ships two ways from the same code:

| Channel | Artifact | Packaging |
|---|---|---|
| Website / GitHub Releases | `BeNGy-Setup-x64.exe`, `BeNGy-x64.msi`, `BeNGy-x64-portable.zip` | unpackaged |
| Microsoft Store | `BeNGy_<version>.0_x64.msix` | MSIX (packaged) |

`powershell -File tools\packaging\build-release.ps1 -Msix` builds all of them into
`artifacts\<version>\`, plus `SHA256SUMS.txt`. The version is
`ApplicationDisplayVersion` in `src\BeNGy\BeNGy.csproj`. Keep `ApplicationVersion`
at `0`: it becomes the fourth part of the MSIX version, and the Store rejects a
non-zero fourth part.

## Website release

1. Bump `ApplicationDisplayVersion`, update `CHANGELOG.md`, merge to `main`, tag `vX.Y.Z`.
2. Build: `powershell -File tools\packaging\build-release.ps1`.
3. `gh release create vX.Y.Z artifacts\X.Y.Z\BeNGy-Setup-x64.exe artifacts\X.Y.Z\BeNGy-x64.msi artifacts\X.Y.Z\BeNGy-x64-portable.zip artifacts\X.Y.Z\SHA256SUMS.txt --notes-file <notes>`

The website links to `releases/latest/download/<file name>`, so keep the file
names exactly as the script produces them.

The installers are not code-signed, so SmartScreen warns on first run until the
files build reputation. A code-signing certificate removes that.

## Microsoft Store (first submission)

1. **Account.** Register as an individual developer at
   [storedeveloper.microsoft.com](https://storedeveloper.microsoft.com). It's free
   (identity check with an ID photo and a selfie). Start from that URL: other
   entry points still show the old paid flow.
2. **Reserve the name.** In Partner Center, create a new **MSIX or PWA app**
   and reserve "BeNGy".
3. **Identity.** Go to Product management > Product identity and copy three values into
   `src\BeNGy\Platforms\Windows\Package.appxmanifest`:
   - `Package/Identity/Name`: replaces the placeholder (MAUI otherwise uses
     `ApplicationIdGuid`)
   - `Package/Identity/Publisher`: replaces `CN=User Name`
   - `PublisherDisplayName`: replaces `User Name`
4. **Build.** Run `powershell -File tools\packaging\build-release.ps1 -Msix`. The
   `.msix` is unsigned on purpose: the Store signs it. To install it locally for
   testing, sign a copy with a self-signed certificate that you trust on the
   test machine.
5. **Test the packaged build** before submitting:
   - Start with Windows uses the manifest's `StartupTask` (`BeNGyStartup`), not
     the registry. A user can turn it off in Task Manager > Startup apps, and the
     app can't turn it back on: the Settings toggle then reverts to Off.
   - A startup-task launch starts minimized to the tray.
   - Check the tray icon, the global hotkey, and single instance.
   - Settings start fresh: packaged MAUI Preferences live in the package's own
     storage, not where the unpackaged app keeps them.
   - Run the Windows App Certification Kit (WACK) on the package.
6. **Submission.**
   - Pricing: Free.
   - Category: Developer tools.
   - Age rating: fill in the IARC questionnaire (no user-generated content, no
     purchases).
   - Store listing: description, at least one screenshot (PNG, 1366×768 or
     larger), and the privacy policy URL from the website (`/privacy`). The
     privacy policy is required for desktop (full-trust) apps even when they
     collect nothing.
   - Submission options: explain `runFullTrust`. BeNGy is a desktop (WinUI 3)
     app that needs a global hotkey, a tray icon and per-user startup
     registration.
   - Package: upload the `.msix` (x64).

### Notes

- The tray icon's GUID includes the executable path. Windows ties a tray GUID
  to one path, and every Store update installs to a new versioned folder.
- `--minimized` exists only in the unpackaged Run-key entry. Packaged launches
  are recognised by `ExtendedActivationKind.StartupTask`.
