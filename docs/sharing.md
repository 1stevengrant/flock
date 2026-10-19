# Sharing

Share a site at a temporary public URL to show your work to a colleague or a client. Flock opens a [Cloudflare quick tunnel](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/do-more-with-tunnels/trycloudflare/), so you get an address like `https://quiet-river-lamp.trycloudflare.com`. No account is needed.

Sharing is for previews only, never for production. A share ends when you stop it, after 8 hours, or when Flock stops, and every share gets a new random URL.

## Sharing a site

:::app
In **Settings → Sites**, select the site and click **Share…**. When the link is ready, copy it or let your colleague scan the QR code. **Stop sharing** ends it. Sites being shared are marked in the Flock panel.
:::

:::cli
```bash
flock share
```

Run it inside the project folder, or name the site: `flock share acme`. Flock copies the URL to your clipboard and prints a QR code. The site stays shared until you press **Ctrl+C**.

To stop a share you started in the app:

```bash
flock share acme --stop
```
:::

## Only letting certain people in

Without anything else, anyone with the link can open the site. To limit it to certain people, list their email addresses, or `*@domain` for everyone at a company. Cloudflare then asks visitors for a one-time code it emails to them.

:::app
Enter the addresses in **Only these people** before you click **Share**.
:::

:::cli
```bash
flock share --allow colleague@acme.com --allow '*@client.com'
```

Quote `*@…` so your shell doesn't expand it.
:::

To change who's allowed, stop the share and start a new one.

## Assets and `npm run dev`

Visitors can't reach the Vite dev server, and Flock never exposes it: it can read files from your whole project. Instead, visitors get your production build from `public/build`:

- When the build is missing or older than your sources, Flock runs `npm run build` before sharing.
- You can keep `npm run dev` running. Your browser keeps hot reloading, and visitors get the built files.
- After you change something, press **r** in the terminal (or **Rebuild assets** in the app), and visitors see the change when they reload.

This works with Laravel's Vite plugin and its default `public/build` folder. With another setup, run your build yourself before sharing.

## What visitors can't see

Your local app is set up for development, so Flock locks a few things down for shared requests:

- **Debug mode is off** for visitors: they get plain error pages, not stack traces, and Debugbar stays hidden. Your own browser keeps debug mode. When the app's config is cached (`php artisan config:cache`), Flock can't switch it off, so it refuses to share until you run `php artisan config:clear`.
- **Dev tools are blocked:** Telescope, Horizon, Pulse, Debugbar, Ignition, Clockwork, Log Viewer and Boost answer 404 at their default paths.
- **Dotfiles are blocked**, such as `.env` or `.git`, in case one ended up in `public/`.
- **Dumps don't appear in the page**, even when you show them there yourself. They're still captured in Flock.
- **Xdebug isn't triggered** by visitors, even while you're debugging the site.
- **Only the shared site is reachable**, never your other sites or Flock itself. Search engines are asked not to index it.

Everything else in the app is public, just as if you had deployed it. If something shouldn't be seen, use **Only these people**.

> `request()->ip()` is the visitor's address, and `url()` builds `https://…trycloudflare.com` links. Mail and queued jobs still use `APP_URL` from `.env`.

## Limits

Quick tunnels come with Cloudflare's limits: up to 200 requests at a time, no server-sent events (WebSockets work), and no uptime guarantee.
