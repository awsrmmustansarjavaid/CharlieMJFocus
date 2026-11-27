# Branding

The Charlie MJ Focus identity: a **focus target** (cyan ring with crosshair ticks) around the **CMJ** monogram,
with a forward-motion arrow. Palette: deep navy, electric cyan, white.

| File | Use |
| --- | --- |
| `assets/branding/CharlieMJFocus-Logo-Master.png` | Original full-size artwork. **Edit this one; regenerate the rest from it.** |
| `assets/CharlieMJFocus-Logo.png` | 1024 px logo with rounded corners (README, presentations) |
| `assets/CharlieMJFocus.ico` | Windows icon: 16, 20, 24, 32, 40, 48, 64, 128 and 256 px |
| `src/CharlieMJFocus.App/Assets/CharlieMJFocus.ico` | Same icon, embedded in the EXE, window and tray |
| `src/CharlieMJFocus.App/Assets/CharlieMJFocus-Logo-256.png` | In-app logo (sidebar and Settings → About) |
| `assets/branding/installer-wizard.bmp`, `installer-small.bmp` | Setup wizard artwork (Inno Setup needs BMP) |
| `assets/branding/social-preview.png` | 1280 x 640 image for GitHub |

## GitHub social preview

Repository → **Settings → General → Social preview → Edit → Upload an image** and choose
`assets/branding/social-preview.png`. (GitHub does not allow this to be set from a file in the repository.)

## Replacing the logo

Put the new artwork over `CharlieMJFocus-Logo-Master.png` (square, at least 1024 px), then regenerate the derived files
with any tool that can produce a multi-size `.ico` (for example ImageMagick:
`magick CharlieMJFocus-Logo-Master.png -define icon:auto-resize=256,128,64,48,40,32,24,20,16 CharlieMJFocus.ico`).
Copy the `.ico` to both `assets/` and `src/CharlieMJFocus.App/Assets/`, and re-export the PNGs and BMPs listed above.

## In the app

The accent colour is separate from the logo and user-selectable (Appearance). The default accent is blue (`#526DE5`);
choose `#00B8D4` for the cyan of the logo.
