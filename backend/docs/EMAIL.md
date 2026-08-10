# Email notifications (demo requests)

Email is **optional**. Every landing-page enquiry is stored in `demo_requests` and shown in
**Admin → Requests** with an unread badge, so nothing is lost if email is off or broken. Email is
only a convenience notification on top of that.

Use **Admin → Requests → Send test email** to verify a configuration. It reports the mail server's
own error message, which is what tells you whether the problem is auth, TLS or the recipient.

## Configuration

| Key | Meaning |
|---|---|
| `Smtp:Host` | e.g. `smtp-mail.outlook.com`. Empty = email disabled. |
| `Smtp:Port` | `587` for STARTTLS (usual), `465` for implicit TLS. |
| `Smtp:EnableSsl` | `true`. With port 587 this means **STARTTLS**; with 465, SSL-on-connect. |
| `Smtp:AuthMode` | `Basic`, `OAuth2`, or `None`. |
| `Smtp:Username` / `Password` | Basic auth only. |
| `Smtp:TenantId` / `ClientId` / `ClientSecret` / `OAuthUser` | OAuth2 only. |
| `Smtp:FromAddress` / `FromName` | Envelope sender. |
| `Smtp:NotificationRecipient` | Internal inbox that receives alerts. Kept separate from the landing page's public contact email so you never have to publish the address you monitor. |

**Never commit real secrets.** Override in production with environment variables — double
underscore maps to the config hierarchy:

```bash
export Smtp__Username="..." Smtp__Password="..." Smtp__ClientSecret="..."
```

## Why MailKit

`System.Net.Mail.SmtpClient` **cannot perform OAuth2 / Modern Auth** (SASL XOAUTH2) and is
documented by Microsoft as obsolete for new development. Since Outlook and Microsoft 365 now
require Modern Auth, the sender uses **MailKit**, which supports Basic and OAuth2 against any
provider.

## Outlook / Microsoft 365

Microsoft has been disabling **basic authentication** for SMTP AUTH. Whether `AuthMode: "Basic"`
works depends on the account:

- **Microsoft 365 (business) mailbox** — basic auth is disabled by default. Use `OAuth2`.
- **Personal outlook.com account** — historically worked with an *app password* (requires
  two-step verification enabled on the account). Microsoft has been retiring this; if the test
  email fails with `535 5.7.139 Authentication unsuccessful ... basic authentication is disabled`,
  basic auth is off for that account and you must use OAuth2 or another provider.

### OAuth2 (Microsoft 365 / Exchange Online)

The implemented flow is **client credentials** (app-only), which suits a server sending mail
unattended:

1. Entra ID (Azure AD) → **App registrations** → New registration. Note the
   **Application (client) ID** and **Directory (tenant) ID**.
2. **Certificates & secrets** → new client secret → note the value.
3. **API permissions** → APIs my organization uses → **Office 365 Exchange Online** →
   *Application permissions* → `SMTP.SendAsApp` → **Grant admin consent**.
4. Grant the service principal send rights on the mailbox (Exchange Online PowerShell):
   ```powershell
   New-ServicePrincipal -AppId <client-id> -ObjectId <object-id>
   Add-MailboxPermission -Identity "noreply@yourdomain.com" -User <object-id> -AccessRights FullAccess
   ```
5. Configure:
   ```jsonc
   "Smtp": {
     "Host": "smtp.office365.com", "Port": 587, "EnableSsl": true,
     "AuthMode": "OAuth2",
     "TenantId": "...", "ClientId": "...", "ClientSecret": "...",
     "OAuthUser": "noreply@yourdomain.com",
     "FromAddress": "noreply@yourdomain.com",
     "NotificationRecipient": "you@yourdomain.com"
   }
   ```

> Client credentials require a **Microsoft 365 / Exchange Online** mailbox. A personal
> `@outlook.com` account has no tenant to register an app-only permission against, so it cannot
> use this flow.

## Recommended alternative

For a product that sends mail unattended, a transactional email provider (Resend, SendGrid,
Brevo, Mailgun, Amazon SES) is usually the better fit than a personal mailbox: a single API key
instead of the OAuth dance, proper SPF/DKIM for deliverability, and no per-account sending limits
or throttling. Most also expose plain SMTP, so only these values change:

```jsonc
"Smtp": {
  "Host": "smtp.resend.com", "Port": 587, "EnableSsl": true,
  "AuthMode": "Basic", "Username": "resend", "Password": "<api-key>",
  "FromAddress": "noreply@yourdomain.com"
}
```
