# FlourishWellness

Survey app built with Blazor on .NET 10.

## What this app uses

- Blazor Server (`net10.0`)
- Entity Framework Core + SQL Server
- Windows Authentication (Negotiate) for normal sign-in
- Role-based access (`Employee`, `Manager`, `Admin`)

## Survey year behavior

Surveys are grouped by year.

- Only one year is active at a time.
- Active year is where users can answer/edit.
- Archived years stay saved and separate.
- Admin can archive current year and create the next one.
- New year copies section/question structure from the previous active year.
- Responses stay tied to the year they were submitted in.

Quick example:

1. 2026 is active.
2. Admin archives 2026.
3. App creates 2027 as active.
4. 2026 remains archived and cannot be edited.

## Run locally

From the repo root:

```powershell
cd .\FlourishWellness
dotnet restore
dotnet ef database update
dotnet run
```

Before running, make sure `ConnectionStrings:DefaultConnection` is set in:

- `appsettings.json`

Notes:

- App startup will fail if `DefaultConnection` is missing or empty.
- In Production, authenticated users are required by default.

## Admin access notes

- Local `admin` account is **not** auto-created.
- Windows users are auto-created in `Users` when they sign in.
- First auto-created Windows user becomes `Admin`; other users default to `Employee` role.

Suggested first setup:

1. Sign in with Windows auth.
2. Go to Admin -> Manage Users.
3. Set your AD account to the role you need.
4. If you want a fallback local admin login, create an account in Manage Users and set the role to Admin.

## IIS deployment (production)

1. Install on server:
   - IIS + Windows Authentication
   - ASP.NET Core Hosting Bundle (.NET 10)

2. Publish from `FlourishWellness`:

```powershell
dotnet publish -c Release -o .\publish
```

3. Copy `publish` to server, for example `C:\inetpub\FlourishWellness`.

4. IIS site settings:
   - Physical path = publish folder
   - App Pool = No Managed Code
   - Windows Authentication = Enabled
   - Anonymous Authentication = Disabled

5. Set the production connection string in deployed `appsettings.json`.  This is done already; if db moves, it will have to be modified.

## Repo layout

- Main app: `FlourishWellness/`
- DB/data helper scripts: `external_apps/`
