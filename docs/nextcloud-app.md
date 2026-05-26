# Nextcloud companion app

## Role

The `nextcloudshot` server app adds a web gallery and per-user folder settings. The desktop client remains capable of uploading screenshots with no server app installed.

## Local development environment

`tools/dev-nextcloud/docker-compose.yml` launches Nextcloud 33 with PostgreSQL and Redis. After the containers start, install Nextcloud in the browser once and copy the app folder into the container under the app id directory:

```powershell
cd tools\dev-nextcloud
docker compose up -d
docker compose cp ..\..\NextCloudShot.NextcloudApp nextcloud:/var/www/html/custom_apps/nextcloudshot
docker compose exec -u www-data nextcloud php occ app:enable nextcloudshot
```

After editing PHP/Vue sources, copy the folder again or bind-mount it during active development.

## Frontend build

```powershell
cd NextCloudShot.NextcloudApp
npm install
npm run build
```

The generated assets are placed in `js/` and `css/` for loading by the PHP template.

## Real server install

Build a release package locally or from GitHub Actions, copy it to the server, and install it under the app id directory:

```powershell
cd NextCloudShot.NextcloudApp
npm ci
npm run build
cd ..
.\tools\package-nextcloud-app.ps1
```

On the Nextcloud 33 server, extract `artifacts/nextcloudshot.tar.gz` so the app root is `/var/www/html/custom_apps/nextcloudshot`, then enable it:

```bash
sudo -u www-data php /var/www/html/occ app:enable nextcloudshot
sudo -u www-data php /var/www/html/occ app:list | grep nextcloudshot
```

`tools/dev-nextcloud/docker-compose.yml` is only a local sandbox for testing against disposable Nextcloud/PostgreSQL/Redis containers. It is not needed for deployment to `nextcloud.leon2k.keenetic.pro`.

## App Store readiness checklist

- Keep the included AGPL license in release packages and add SPDX headers as appropriate.
- Public App Store name currently remains `NextCloudShot`; this intentionally carries review risk because Nextcloud guidelines disallow `Nextcloud` in app names.
- Test against currently supported server versions.
- Add screenshots, translations and privacy documentation.
- Package and sign release tarballs according to official publishing guidance.
- Perform a security pass: CSRF protections, route annotations, user-folder path checks, no credential storage.
