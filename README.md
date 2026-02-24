# FlourishWellness

This is the Wellness Survey app.  It uses .NET 10 and Blazor.

## Year-Based Survey Setup (important)

The survey is now grouped by year entities.

- Only one year can be ACTIVE (read/write) at a time.
- Archived years are read-only.
- Admin can archive the active year and auto-create the next year.
- New year copies the section/question layout from the old active year.
- Responses stay with their original year.

So example:

- 2026 = ACTIVE (users answer this one)
- Admin archives 2026
- App creates 2027 = ACTIVE
- 2026 stays saved as archived data

## Super Simple Production Server Steps (IIS + Windows Login)

This is the easy version that works for me.

1. On the correct server, install:
   - ASP.NET Core Hosting Bundle (.NET 10 runtime)
   - IIS role with Windows Authentication enabled

2. (FOR PRODUCTION:) Publish the app from your machine:
   - Open terminal in `FlourishWellness/FlourishWellness`
   - Run:

   ```powershell
   dotnet publish -c Release -o .\publish
   ```
   - You can deploy the ready zip file `publish.zip` in the same folder.

3. Copy the `publish` folder to the server (for example `C:\inetpub\FlourishWellness`).

4. In IIS:
   - Add a new website pointing to that folder
   - Use the port/host name you want
       - Check appsettings.json: the DefaultConnection string must point to the app.db file
   - App Pool: **No Managed Code**

5. In IIS Authentication for the site:
   - Enable **Windows Authentication**
   - Disable **Anonymous Authentication**

6. Make sure your `appsettings.json` connection string is correct on the server.

7. Browse to the site URL and test login.

If the site does not start, reboot IIS with:

```powershell
iisreset
```

---
Roles:
- Employee:  can take/view survey

- Manager: take/view survey, view results

- Admin: full control (users, survey, results, etc)


---

New users are added automatically from AD with the Employee role; an admin can change the roles in the admin area for any user. 

When you first log in, you won't be set as admin; to set yourself as admin, go to the admin login page: http://sitename.com/admin-login

Admin credentials:  admin/admin

- When you are logged in, go to the Admin tab, and update your account to Admin role in the Manage Users view. Then sign out of admin account; you'll default to your personal admin-level account automatically.
---
That is basically it.