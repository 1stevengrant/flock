# Uninstalling

:::app
To remove Flock, open **Settings…** from the Flock panel, go to **About** and click **Uninstall Flock…** (macOS) or **Uninstall…** (Windows), then confirm with **Uninstall**. On macOS, click **Quit Flock** when it's done to move the app to the Trash.
:::

:::cli
To remove Flock, run:

```bash
flock uninstall
```
:::

This removes:

- the background daemon and its startup entry,
- Flock's entry on your PATH,
- the `.test` DNS configuration and the trust in Flock's certificate authority,
- the menu bar / tray app,
- downloaded PHP, Node and service binaries.

**Your sites' settings and databases are kept**, so reinstalling Flock brings everything back.

## Removing everything

To also delete your databases, caught mail, certificates and settings:

:::app
Tick **Also delete my sites' settings and databases** before you click **Uninstall**.
:::

:::cli
```bash
flock uninstall --purge
```
:::

> This can't be undone. Back up any databases you want to keep first, for example with `pg_dump` or `mysqldump`.

Your project folders are never touched, and `.flockrc` files stay in your projects.
