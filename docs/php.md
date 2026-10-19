# PHP

Flock installs and manages PHP for you. Several versions live side by side, and each site uses the version it needs.

## Installing versions

PHP 8.1 and newer are supported.

:::app
Open **Settings…** from the Flock panel and go to **PHP**. Click **Install** next to a version, or **Update to …** when a newer patch release is ready. **Check now** looks for new releases.

![Settings → PHP](screenshot:settings-php)

To remove a version, open its **⋯** menu and choose **Uninstall…**.
:::

:::cli
```bash
flock php install 8.5
flock php install 8.4
flock php list
```

To install from a file you already have, use `--from`:

```bash
flock php install --from php-8.5.0-nts-Win32-vs17-x64.zip
```
:::

- **Windows:** Flock downloads the official non-thread-safe x64 builds from windows.php.net and verifies their SHA-256 checksums. Each version runs a small pool of `php-cgi` workers, and requests go to the least busy one.
- **macOS:** Flock uses static builds for Apple silicon, with PHP-FPM. When no prebuilt binary is available, Flock compiles one locally (about 5–10 minutes, using the Xcode Command Line Tools).

In the background, Flock can keep patch releases and new versions ready (built on macOS, downloaded on Windows), so updating is instant.

:::app
Turn this on or off under **Settings → PHP** with **Build updates and new PHP versions automatically** (macOS) or **Download updates and new PHP versions automatically** (Windows).
:::

## The default version

The default version is used by every site and folder that doesn't choose its own. To choose per project, see [Per-project versions](project-versions.md).

:::app
In **Settings → PHP**, click **Make Default** next to the version you want.
:::

:::cli
```bash
flock use 8.5
```
:::

## Extensions

Flock's PHP builds include the extensions most Laravel apps need:

```text
bcmath  bz2  calendar  ctype  curl  dom  exif  fileinfo  filter  ftp  gd  gmp
iconv  igbinary  intl  mbregex  mbstring  mysqli  mysqlnd  opcache  openssl
pcntl  pdo  pdo_mysql  pdo_pgsql  pdo_sqlite  pgsql  phar  posix  readline
redis  session  simplexml  sockets  sodium  sqlite3  tokenizer  xml
xmlreader  xmlwriter  xsl  zip  zlib
```

Xdebug is included as a shared extension. See [Xdebug](xdebug.md).

> The `redis` extension isn't available in the Windows builds. Use `REDIS_CLIENT=predis` there.

## php.ini

Each version has its own `php.ini`. Flock writes sensible development defaults the first time and never overwrites your changes:

| Setting | Default |
| --- | --- |
| `memory_limit` | `512M` |
| `upload_max_filesize` / `post_max_size` | `128M` |
| `display_errors` | `On` |
| `opcache.enable` | `On` |

:::app
To edit it, open the version's **⋯** menu in **Settings → PHP** and choose **Open php.ini**.
:::

:::cli
The file is `php/8.5/php.ini` inside Flock's [data directory](configuration.md#data-directory), and `php --ini` prints its path.
:::

## Composer

`composer` is included and runs with the PHP version of the current project, so `composer install` always matches the version the browser uses.
