# CLI reference

Almost every feature of Flock is available from the `flock` command. Run `flock --help` or `flock <command> --help` for details.

:::app
This page lists the `flock` commands. The other pages show where to find each action in the app.
:::

## Setup

| Command | Description |
| --- | --- |
| `flock install [--skip-privileged]` | Set Flock up: shims on PATH, background daemon, `.test` DNS and trusted HTTPS |
| `flock uninstall [--purge]` | Remove Flock. Keeps site settings and databases unless `--purge` is given |
| `flock status` | Show the state of the daemon and its services |
| `flock restart` | Restart the daemon and all services |
| `flock logs [name] [-n 50] [-f]` | Show service logs (daemon, web, php8.5-fpm, postgres, …) |
| `flock version` | Print the Flock version |
| `flock import lerd` | Import parked folders, linked sites and default versions from Lerd |

## Sites

| Command | Description |
| --- | --- |
| `flock park [dir]` | Serve every subdirectory of `dir` as `<subdir>.test` |
| `flock unpark [dir]` | Stop serving a parked directory (alias: `forget`) |
| `flock link [name] [--php 8.4] [--secure]` | Serve the current directory as `<name>.test` |
| `flock unlink [name]` | Remove a linked site |
| `flock sites` | List linked and parked sites |
| `flock secure [name]` | Serve a site over HTTPS with a trusted certificate |
| `flock unsecure [name]` | Serve a site over plain HTTP |
| `flock domain add <domain> [site]` | Add a subdomain or alias to a site |
| `flock domain remove <domain> [site]` | Remove an extra domain (alias: `rm`) |
| `flock domain list [site]` | List a site's extra domains (alias: `ls`) |
| `flock share [name] [--allow <email>] [--debug]` | Share a site at a temporary public URL until you press Ctrl+C |
| `flock share [name] --stop` | Stop sharing a site (e.g. one shared from the app) |

## PHP & Node.js

| Command | Description |
| --- | --- |
| `flock php install <version>` | Install a PHP version, e.g. `8.5` (`--from <file>` for a local build) |
| `flock php list` | List installed PHP versions |
| `flock use <version>` | Set the default PHP version |
| `flock node install <version>` | Install Node.js: a major like `22`, or an exact `22.11.0` |
| `flock node list` | List installed Node.js versions |
| `flock node default <major>` | Set the default Node.js version |
| `flock isolate <php\|node> <version>` | Use a version for this site, on this machine only |
| `flock unisolate <php\|node>` | Remove an isolation |
| `flock isolated` | List every site's versions and where they come from |
| `flock pin <php\|node> [version]` | Write a version into the project (`.flockrc` or `.nvmrc`) |
| `flock unpin <php\|node>` | Remove a pinned version |

## Services

| Command | Description |
| --- | --- |
| `flock services` | List services and their state |
| `flock services install <service> [version] [--port N]` | Install MySQL, MariaDB, MongoDB, Redis, Meilisearch, Typesense or RustFS |
| `flock services uninstall <service> [--delete-data]` | Remove a service |
| `flock postgres install [major]` | Install PostgreSQL (default 18) |
| `flock postgres use <major>` | Switch the active PostgreSQL version |
| `flock postgres update [major]` | Update to the latest minor release |
| `flock postgres uninstall <major> [--delete-data]` | Remove a PostgreSQL version |
| `flock postgres list` | List installed versions |
| `flock postgres create-db <name>...` | Create one or more databases |
| `flock postgres install-client` | Install `psql`, `pg_dump`, `pg_restore` and friends |
| `flock valkey install` | Build and run the newest Valkey (macOS) |

## Tools

| Command | Description |
| --- | --- |
| `flock mail` | List caught emails |
| `flock mail open <id>` | Open an email in your system's mail app |
| `flock mail clear [inbox]` | Delete caught emails |
| `flock dumps [on\|off\|clear]` | Capture `dump()` / `dd()` output, or list captured dumps |
| `flock xdebug [on\|off]` | Send the requests that ask for Xdebug to a PHP pool that loads it |
| `flock xdebug breakpoints [on\|off]` | Also debug every request of a site with PhpStorm breakpoints |
| `flock debug <php args...>` | Run PHP with Xdebug, e.g. `flock debug artisan migrate` |

## Shims

These commands are put on your PATH and run the version that the current project uses:

```text
php  composer  node  npm  npx  redis-cli  valkey-cli  psql  pg_dump  pg_restore
```
