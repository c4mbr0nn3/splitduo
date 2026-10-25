# Security Policy

SplitDuo stores group finances, credentials, and two-factor secrets. If you find a vulnerability, report it privately rather than in a public issue, merge request, or discussion.

## Reporting a vulnerability

**Preferred:** open a [confidential issue on GitLab](https://gitlab.com/j1mm0/splitduo/-/issues/new?issue%5Bconfidential%5D=true). Confidential issues are visible only to you and the maintainer.

**Alternative:** email the maintainer at `frensinik89@yahoo.it`, the address that also appears in the commit history and in `LICENSE`.

Please include:

- The affected version, meaning the image tag you run or `VERSION` at that tag, and the deployment type (Docker Compose, with or without a reverse proxy).
- Steps to reproduce, or a proof of concept.
- The impact you believe it has, and any conditions required to trigger it.

You will get an acknowledgement within a few days. This is a side project with one maintainer, so there is no bug bounty and no guaranteed response time. Security reports are treated as the highest priority.

## Supported versions

| Version | Supported |
| --- | --- |
| Latest stable release (1.x, newest tag) | Yes. Fixes are released here. |
| Older 1.x releases | No. Upgrade to the latest stable. |
| Release candidates (`rc` tag) | Not supported. Fix reports are still welcome. |

Only the newest stable release receives security fixes. If you are on an older minor version, upgrade before reporting unless the issue also reproduces on the latest release.

## Scope

In scope, as examples:

- Authentication and session handling: JWT issuance and validation, refresh-token rotation, account lockout, 2FA (TOTP) bypass or secret disclosure.
- Authorization: accessing another group's expenses, attachments, members, or statistics; privilege escalation between group member, group admin, and system admin.
- File handling: attachment upload or download traversal, content-type confusion, storage of executable content.
- Import and export parsing: CSV or receipt input that leads to injection or arbitrary file access.
- Injection: SQL, command, template, or XSS through any user-controlled field, including email templates and translated strings.
- The AI receipt-scanning path: SSRF through `SD_AI_BASE_URL`, or leaking receipt data to an endpoint other than the configured one.

Out of scope:

- Misconfiguration of your own deployment: exposing the container without a TLS-terminating proxy, keeping the shipped placeholder JWT secret, weak admin passwords, or missing backups.
- Denial of service by volume, and findings that only reproduce with Development settings, a self-modified database, or physical access to the host.
- Vulnerabilities in third-party dependencies that do not affect SplitDuo's usage. Report those upstream, and mention them if you think SplitDuo is exploitable through them.

## Handling of secrets

The application never needs your SMTP password, AI API key, or database password in a report. Do not include real credentials, tokens, or user data; redact them. If a proof of concept requires a secret, describe how to generate an equivalent one.
