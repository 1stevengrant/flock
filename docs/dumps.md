# Dumps

Flock can capture the output of `dump()` and `dd()` and show it in a separate window, instead of breaking your page layout or JSON response.

## Turning it on

You don't need to change your app or install a package: Flock sets Symfony's `VAR_DUMPER_FORMAT=server` for PHP and runs a dump server on port 9912.

:::app
Switch on **Capture dumps** in the Flock panel. The same switch is in **Settings → Dumps** and in the toolbar of the Dumps window.
:::

:::cli
```bash
flock dumps on
```

Turn it off again with `flock dumps off`.
:::

## The Dumps window

Each dump shows:

- the site and the request (method and URL) or Artisan command that produced it,
- the source file and line, with a link that opens it in your editor,
- the dumped value, expandable just like in the browser.

Flock keeps the last 500 dumps.

:::app
Click **Dumps** in the Flock panel, or **Open Dumps** in **Settings → Dumps**. On Windows, it's also in the tray icon's right-click menu.

![The Dumps window](screenshot:dumps-window)

Use **Search dumps** to filter them, and **Latest Request Only** to hide everything but the last request. Choose which editor source links open in (PhpStorm, VS Code, Cursor or Zed) from the **Editor** menu, or under **Settings → Dumps**. **Clear** removes all dumps.
:::

:::cli
Run `flock dumps` without arguments to list captured dumps in the terminal, and clear them with:

```bash
flock dumps clear
```

The Dumps window itself is part of the app.
:::
