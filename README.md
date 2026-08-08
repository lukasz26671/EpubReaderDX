# EpubReaderDX

Cross-platform EPUB reader (MAUI Hybrid + Blazor WebAssembly).

## Web app on GitHub Pages

The WASM host lives in `EpubReader.Web`. Deploy is automated via `.github/workflows/deploy-github-pages.yml`.

### One-time GitHub setup

1. Create a GitHub repository (e.g. `EpubReaderDX`) and push this project.
2. Open **Settings → Pages**.
3. Under **Build and deployment → Source**, choose **GitHub Actions**.
4. Push to `main` (or run the workflow manually under **Actions**).

Your site URL will be:

`https://<user>.github.io/<repo>/`

Example: `https://you.github.io/EpubReaderDX/`

### What the Pages workflow does

- Publishes `EpubReader.Web` with .NET 10
- Sets `<base href="/<repo>/">` automatically (or `/` for `username.github.io` repos)
- Adds `.nojekyll` so `_framework` is not ignored by Jekyll
- Copies `index.html` → `404.html` for SPA deep-link fallback
- Deploys `wwwroot` to GitHub Pages

### Local web publish (optional)

```powershell
.\scripts\publish-github-pages.ps1 -BaseHref /EpubReaderDX/ -Output .\artifacts\web
# Static site root: .\artifacts\web\wwwroot
```

## Desktop / Android releases

`.github/workflows/release-maui.yml` builds **Windows x64** + **Android APK** and uploads them to **GitHub Releases**.

### Trigger a release

**Option A — tag push**

```powershell
git tag v1.0.0
git push origin v1.0.0
```

**Option B — Actions UI**

1. Open **Actions → Release MAUI (Windows + Android)**
2. **Run workflow**
3. Optionally type a version like `1.0.1` (creates `v1.0.1` if missing)

### Release assets

| Asset | Contents |
|-------|----------|
| `EpubReaderDX-<ver>-android.apk` | Android sideload APK |
| `EpubReaderDX-<ver>-win-x64.zip` | Unpackaged self-contained Windows app |

### Optional Android release signing

Without secrets, the APK is **debug-signed** (OK for testing/sideload).

For a proper release keystore, add repository secrets:

- `ANDROID_KEYSTORE_BASE64` — keystore file as base64
- `ANDROID_KEYSTORE_PASSWORD`
- `ANDROID_KEY_ALIAS`
- `ANDROID_KEY_PASSWORD`

Encode a keystore locally:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes(".\release.keystore")) | Set-Clipboard
```

## Notes

- EPUB files stay on-device / in the browser. Releases and Pages do not host your books.
- First Web load downloads the Blazor WASM runtime; Tailwind/Lucide load from CDN.
