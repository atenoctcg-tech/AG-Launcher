# AG Launcher account service

The v0.4.0 launcher requires a verified server account. The public website can be browsed without signing in. No email service or account hosting credentials were supplied, so the server is **not deployed** and the public manifest intentionally has an empty `auth.apiBaseUrl`. Launcher sign-in remains blocked until the studio completes this setup. Existing v0.3.1 users are not forced onto this release.

## Studio setup

1. Create a **private** GitHub repository, for example `atenoctcg-tech/AG-Accounts-Private`. Never put accounts.json in the public launcher repository. A fine-grained token for this private repo needs Contents read/write and Metadata read permission.
2. Deploy this directory's Dockerfile to a Node/Docker host with HTTPS, using **one instance**. Set environment variables listed in `.env.example` in the host's secret manager, not in source control.
3. Verify your sending domain with Resend, set `RESEND_API_KEY` and `MAIL_FROM`. Set `OTP_SECRET` to a long random secret. Set `ALLOWED_ORIGINS=https://atenoctcg-tech.github.io`.
4. Check `/health`, then test registering a real account, receiving its code, verifying, signing in, changing nickname/password/email, and logging out.
5. In the website's Studio admin, set Account server HTTPS URL and publish. Alternatively use the launcher's administrator setup → DESIGN & WORKSHOP. Reopen the launcher.

Accounts are serialized in private GitHub `accounts.json`; passwords use scrypt with per-user salts, OTPs are HMAC hashes, sessions are hashed. Codes expire after ten minutes and five unsuccessful guesses, resend has a one-minute cooldown, and repeated incorrect passwords lock the account temporarily. Verification codes are never returned through the API or printed to logs. Email changes take effect only after code confirmation. Password changes revoke other sessions.

Private GitHub JSON is intended for a small studio beta, not a high-volume account service: requests serialize, GitHub API/write limits apply, and each write remains in private git history. Operate one instance; conflicts fail closed. For public scale migrate the store to a transactional database, add distributed rate limiting, retention/deletion and account recovery. HTTPS termination and service secrets belong to the host. Never expose the repository token in the launcher, website or a public file.

Run `node --test` to test verification gates, code reuse/attempt limits, profile changes and session revocation. Run `node --env-file=.env server.mjs` locally with real configured services; the tests use in-memory storage and an email collector and do not send email.
