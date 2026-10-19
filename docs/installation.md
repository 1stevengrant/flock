# Installation

Download the installer for your platform from the [download page](https://flockphp.com/download).

## Windows

**Requirements:** Windows 10 or 11 (x64; Arm64 runs under emulation) and the [Microsoft Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe), which PHP needs.

1. Run `Flock-Setup-x64.exe`. Windows asks for administrator approval once.
2. Optionally keep **Open Flock when I sign in** checked.
3. The tray app opens and runs the setup.

The setup:

- adds Flock's `bin` folder (with the `php`, `composer`, `node` and `npm` shims) to your PATH,
- registers the **Flock Daemon** scheduled task, which starts at sign-in without a console window,
- routes `*.test` to Flock's DNS server with a name-resolution policy rule, and installs a small helper service that keeps a `# BEGIN Flock` block in your hosts file as a fallback,
- adds Flock's certificate authority to the Trusted Root store.

### Command-line install

You can also install without the app: put `flock.exe` and `flock-helper.exe` in a folder and run

```bash
.\flock.exe install
```

## macOS

**Requirements:** macOS 15 Sequoia or newer, on Apple silicon (Intel Macs are not supported). The Xcode Command Line Tools (`xcode-select --install`) are needed when Flock compiles a PHP version locally.

1. Open `Flock.dmg` and drag **Flock** to Applications.
2. Start Flock. A bird appears in your menu bar and the first-run setup opens.
3. Click **Set Up Flock** and enter your password once, so Flock can add the DNS resolver and trust its certificate authority.

> Preview builds aren't notarized yet. If macOS refuses to open the app, right-click it in Applications, choose **Open** and confirm.

The setup adds the shims to your PATH, registers a launchd agent for the daemon, writes `/etc/resolver/test` so `.test` names go to Flock's DNS server, and trusts the certificate authority in the System keychain.

## Checking the installation

:::app
Click the bird in the menu bar (macOS) or the tray icon (Windows). The top of the panel should read **Running**, followed by the Flock version, and every service listed under **Services** should have a green dot.

![The Flock panel with every service running](screenshot:panel-running)

Open a **new** terminal (so it picks up the new PATH). `php -v` and `node -v` should print Flock's versions.
:::

:::cli
Open a **new** terminal (so it picks up the new PATH) and run:

```bash
flock status
```

It shows the daemon, web server, DNS and every service, and whether `.test` names resolve. `php -v` and `node -v` should print Flock's versions.
:::

## Ports

Flock needs ports **80** and **443** for the web server. If another program uses them, for example another local environment, IIS or Apache, Flock tells you which program it is.

:::app
Stop that program, then click **Restart** at the bottom of the Flock panel.
:::

:::cli
Stop that program and run `flock restart`.
:::

## Coming from Lerd

If you used Lerd before, import your parked folders, linked sites and default versions with:

```bash
flock import lerd
```

:::app
The import is only available from the CLI. Run it once in a terminal, and the imported sites show up in the app.
:::
