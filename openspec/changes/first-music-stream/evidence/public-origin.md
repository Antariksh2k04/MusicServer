# Task 1.2 — Public HTTPS and Google origin

Checked 2026-10-08 after task 1.1's blocked review. **Outcome: blocked; task remains unchecked.** No public hostname/IP, trusted deployed endpoint, certificate renewal evidence, or Google web-client configuration has been supplied. Configuration/source inspection found only the local diagnostic origin. Root planning documents remain unchanged.

Scheduling update: this original task 1.2 is now final-phase **7.2**; the account prerequisite is now **7.1**. Earlier labels and probe output are historical. Public-origin validation is deferred; configured development Google clients may still be needed for local real sign-in. Follow [the active task mapping](../tasks.md).

## Work and executed check

Added `scripts/check-public-origin.ps1`. With no origin configured it reports `originNotConfigured`, `trustedHttpsLiveness: false`, and `gateCompleted: false`. This no-origin check was executed successfully as an inventory operation; it is not a passing HTTPS gate.

With an operator-supplied HTTPS hostname origin, the script can GET `/api/v1/health/live` using normal OS certificate validation, a ten-second timeout, and no redirect/credential support. It rejects HTTP, IP/loopback/local/internal names, userinfo, query/fragment, and non-root paths before requests. It never registers DNS, modifies firewall rules, changes trust, provisions TLS, or sends Google proofs. Even a successful liveness probe reports only partial evidence; Google origin acceptance, no-purchase status, renewal, and mobile reachability remain false.

```powershell
# Missing origin: reports the current blocker without sending a request.
./scripts/check-public-origin.ps1

# When the actual public origin exists, supply it in the local shell:
# ./scripts/check-public-origin.ps1 -Origin $publicOrigin
```

## Evidence still needed

1. Choose a no-purchase hostname with a sustainable assignment/update process and verify its actual DNS/control terms. No hostname provider was silently selected here.
2. Once task 1.1 permits resources, point DNS to the real host and verify public HTTPS from desktop and mobile data. Record certificate issuer/hostname/expiry and renewal/storage configuration, with no keys.
3. Configure the selected Google web application client with the exact browser origin and inspect the actual saved configuration. Record a redacted acceptance check. A syntactically valid client ID or local diagnostic login is insufficient.
4. Record the trusted liveness result, public reachability, provider/no-purchase evidence, Google console acceptance, and renewal evidence before assigning a pass verdict.

Google's [Identity Services setup guide](https://developers.google.com/identity/gsi/web/guides/get-google-api-clientid) requires configuring a web client and its authorized JavaScript origins; its localhost development guidance does not establish a production origin. [Caddy's automatic HTTPS documentation](https://caddyserver.com/docs/automatic-https) describes public DNS, reachable ports, and persistent writable certificate data. Actual deployment checks still need to show these conditions hold.

## Review

Self-review caught missing internal-hostname rejection and corrected it with regression cases. Reviewed normal trust validation/no redirects, no mutation/provisioning, secret-bearing URI rejection, and the distinction between partial TLS evidence and completed acceptance. Automated negative checks do not prove valid production certificate issuance, Google sign-in, renewal, or mobile access. The user is away; account/domain configuration remains an external prerequisite rather than an inferred approval to buy or expose local diagnostics.
