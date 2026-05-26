# Nextcloud companion app

## Role

The `cloudshot` server app adds a web gallery and per-user folder settings. The desktop client remains capable of uploading screenshots with no server app installed.

## Local development environment

`tools/dev-nextcloud/docker-compose.yml` launches Nextcloud 33 with PostgreSQL and Redis. After the containers start, install Nextcloud in the browser once and copy the app folder into the container:

```powershell
cd tools\dev-nextcloud
docker compose up -d
docker compose cp ..\..\src\server\cloudshot nextcloud:/var/www/html/custom_apps/cloudshot
docker compose exec -u www-data nextcloud php occ app:enable cloudshot
```

After editing PHP/Vue sources, copy the folder again or bind-mount it during active development.

## Frontend build

```powershell
cd src\server\cloudshot
npm install
npm run build
```

The generated assets are placed in `js/` and `css/` for loading by the PHP template.

## App Store readiness checklist

- Keep the included AGPL license in release packages and add SPDX headers as appropriate.
- Confirm that the app name and branding do not contain `Nextcloud`.
- Test against currently supported server versions.
- Add screenshots, translations and privacy documentation.
- Package and sign release tarballs according to official publishing guidance.
- Perform a security pass: CSRF protections, route annotations, user-folder path checks, no credential storage.
