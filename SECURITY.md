# Security Policy

## Reporting a vulnerability

Please report suspected vulnerabilities **privately** — do not open a public issue.

Use GitHub's [private vulnerability reporting](https://github.com/kerem-acer/latchkey/security/advisories/new):
go to the repository's **Security** tab → **Report a vulnerability**. This starts a private advisory
that only the maintainers can see.

Please include:

- The affected version(s) and platform (Windows / macOS / Linux, and the backend in use).
- A description of the issue and its impact.
- Steps to reproduce, or a minimal proof of concept.

You can expect an initial acknowledgement within a few days, and an assessment with a remediation
plan or a decline (with reasoning) shortly after.

## Supported versions

Latchkey is pre-1.0; security fixes are released against the **latest** published version. Please
upgrade to the newest release before reporting.

## Scope — what Latchkey does and does not defend

Latchkey delegates storage to the operating system's credential store and **invents no cryptography
of its own.** Its security is exactly that of the underlying store, no more. In particular:

- Any process running as your OS user can generally read secrets you stored — there is no
  cross-application authorization layer beyond what the OS provides.
- At-rest strength is whatever the OS store provides; use full-disk encryption (BitLocker, FileVault,
  LUKS) to protect the on-disk store.

Reports that reduce to these documented, by-design boundaries (see the README's "What Latchkey does
*not* protect you from" section) are not vulnerabilities. A flaw in how Latchkey calls the OS —
leaking, mishandling, or corrupting secrets relative to what the OS store guarantees — is.
