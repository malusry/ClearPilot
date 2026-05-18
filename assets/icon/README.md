# ClearPilot Icon Assets

The current app icon is based on the selected refined PNG concept. The source image is preserved separately, and the app icon PNG uses a transparent background for Windows icon use:

```text
clearpilot-icon-source.png
clearpilot-icon.png
```

The Windows `.ico` used by the C# project is generated from that PNG:

```text
clearpilot.ico
```

Regenerate it with:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\create-transparent-icon-png.ps1
powershell -ExecutionPolicy Bypass -File .\tools\generate-ico-from-png.ps1
```

Earlier vector drafts are kept under:

```text
drafts\
```
