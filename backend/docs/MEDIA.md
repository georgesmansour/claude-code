# Media architecture

## The core separation (why it exists)
Two kinds of media are kept **completely** separate, in the database and the code:

| | Built-in template assets | User-uploaded media |
|---|---|---|
| Owned by | the application | one invitation |
| Stored in | the templates' JSON (`templates.data`) as app-owned URLs | the `user_media` table + file storage |
| Editable by users | **never** (read-only) | fully (upload / replace / delete) |
| Code path | seeded templates; `if (IsBuiltin) 400` on write | `MediaService` + `user_media` only |

`MediaService` *only ever* writes rows to `user_media`; it has no way to touch a template
asset. This makes the "users can never modify template images" guarantee structural, not a
convention someone can forget.

## Storage — `IFileStorage`
All file I/O goes through `IFileStorage` (provider-relative paths only). The shipped
implementation is `LocalFileStorage` (configurable base path via `Storage:BasePath`), which
works the same locally and in production given a persistent volume. Swapping in Azure Blob / S3
is a single new class + DI line — nothing else changes. Files are content-addressed
(`<invitationId>/<sha256>.<ext>`); the base path is path-traversal-guarded.

## Pipeline (`MediaService.UploadAsync`)
1. **Validate** against strict content-type allow-lists (`MediaValidation`) + per-kind size caps.
   SVG is intentionally excluded (XSS vector).
2. **Optimise** images with ImageSharp: downscale to a max edge, re-encode to WebP. This shrinks
   payloads *and* strips metadata / neutralises anything malicious masquerading as an image
   (a non-image that claims `image/png` fails to decode → rejected).
3. **Hash** the final bytes (SHA-256) and **de-duplicate** per invitation (unique index on
   `(invitation_id, content_hash)`), so re-uploads reuse the existing file/row.
4. **Store** via `IFileStorage`, then insert the `user_media` row. 1 row ↔ 1 file, so deletes are
   clean (no ref-counting).

## HTTP surface
- `POST/GET/DELETE /api/client/media` — invitation owner, scoped to their own invitation via JWT.
- `POST/GET/DELETE /api/admin/invitations/{id}/media` — Super Admin, any invitation.
- `GET /api/public/media/{id}` — anonymous read; **range-enabled** streaming (video/audio seeking)
  with `Cache-Control: public, max-age=31536000, immutable` (media is immutable once created).
  Served under `/api/*` so the existing frontend proxy covers it with no extra infra.

Stored references are **relative** (`/api/public/media/<id>`) so invitations stay portable across
environments; the frontend resolves them against `API_BASE` only where needed.

## Frontend
- Admin editor: every image/video field is a drag-drop upload widget (preview, replace, delete);
  raw URL inputs were removed. The widget keeps a hidden binding input so the existing
  save/collect logic is untouched.
- Cover background **video**: `cover.video` (muted, `autoplay`, `loop`, `playsinline`,
  `preload=metadata`) overrides the cover image — mobile-optimised, never blocks first paint.
- Background **music** (`invitation-music.js`, shared by all templates): fixed bottom-left icon,
  **mute-only** toggle (never pause/restart), autoplay when allowed else on first interaction,
  loops; supports an uploaded audio file **or** a YouTube URL (audio-only, hidden surface).

## Security posture
Auth on every mutating call; strict type/size validation; image re-encoding; SVG blocked;
content-addressed filenames (no user-controlled paths); path-traversal guard; unguessable GUID
read URLs (media is meant to be public — it renders on the public invitation).

## Recommended follow-ups
- Lock down CORS (`Program.cs` currently allows any origin for dev) to the known frontend origins in production.
- Optional: magic-byte sniffing for video/audio, and a background sweep for `user_media` rows whose files are missing (the service already self-heals on read).
- Optional ffmpeg transcoding step behind `IFileStorage` if smaller videos are desired.
