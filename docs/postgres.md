# PostgreSQL

Flock runs PostgreSQL natively. You can install several major versions, each with its own databases, and switch which one is active.

## Installing

:::app
Open **Settings…** from the Flock panel, go to **Services**, switch the filter to **All** and select **PostgreSQL**. Click **Install PostgreSQL 18**, or pick another major version from **Other Versions** (macOS) or **Another version** (Windows).

![PostgreSQL in Settings → Services](screenshot:settings-services-postgres)
:::

:::cli
```bash
flock postgres install        # PostgreSQL 18
flock postgres install 16     # a specific major
flock postgres list
```
:::

PostgreSQL listens on `127.0.0.1:5432` as user `postgres`, without a password.

```bash
DB_CONNECTION=pgsql
DB_HOST=127.0.0.1
DB_PORT=5432
DB_DATABASE=laravel
DB_USERNAME=postgres
DB_PASSWORD=
```

## Creating databases

:::app
Creating a database isn't in the app yet. Use the CLI command below, your database client, or let `php artisan migrate` create the database when it asks.
:::

```bash
flock postgres create-db acme acme_testing
```

## Switching versions

Only one major version runs at a time. Each version keeps its own data directory, so switching back brings back your databases.

:::app
In the PostgreSQL detail, click **Switch to 17** next to the version you want and confirm with **Switch**. The card at the top shows the **Active** version and the size of its databases.
:::

:::cli
```bash
flock postgres use 17
```
:::

## Updating and removing

:::app
Click **Update to …** next to a version to get its latest minor release, and **Check for Updates** to look for new ones. To remove a version, open its **⋯** menu and choose **Uninstall…**. Tick **Also delete its databases** to remove its data as well.
:::

:::cli
```bash
flock postgres update 18                       # latest 18.x
flock postgres uninstall 16                    # keeps the data
flock postgres uninstall 16 --delete-data
```
:::

## Client tools

`psql`, `pg_dump`, `pg_restore` and the other client tools are on your PATH and connect to Flock's server.

:::app
To install the client tools on their own, click **Install** under **Command-line tools** in the PostgreSQL detail.
:::

:::cli
To install the client tools on their own:

```bash
flock postgres install-client
```
:::
