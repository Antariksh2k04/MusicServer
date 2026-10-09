# Environment prerequisite evidence

Change: `first-music-stream`. Checked: 2026-10-08.
Task 1.1 outcome: **BLOCKED / actual account access unavailable**. The last owner confirmation was that Oracle signup had not happened. The original probe below predates the completed local phase; current follow-up evidence is recorded afterward. No cloud resource has been provisioned or production gate completed.

Scheduling update, 2026-10-08: the owner explicitly deferred Oracle until the final phase. Original task 1.1 is now **7.1**, and original public-origin task 1.2 is **7.2**; both remain unchecked and no longer gate local work. Historical calls below to resume account checks are superseded by the execution order and ID mapping in [tasks.md](../tasks.md); retain these observations and earlier progress counts as historical evidence, not instructions to block local development. Existing probe JSON retains its original task labels.

## Observed local environment

This table records the original pre-scaffold probe.

| Check | Observation |
| --- | --- |
| OpenSpec | Initialized local root; `spec-driven`; apply state ready; 0/42 tracked tasks complete. All CLI-listed proposal/spec/design/task context files read. |
| Application | No source/test projects exist. No scaffold, dependency installation, or application build performed. |
| OCI access | Owner confirmed no Oracle account yet. `oci` not found on PATH; neither workspace `.oci/config` nor the standard profile `.oci/config` exists. Credential files were not read or printed. |
| .NET SDK | `dotnet --list-sdks` reports 9.0.315 and 10.0.401. Installed SDK presence does not prove Linux ARM64/deployment compatibility. |
| Node/npm | `node --version` reports v24.18.0; `npm --version` reports 11.16.0. Dependency compatibility is not verified. |
| Android tooling | `adb` and `java` not found on PATH; Android SDK environment variables unset; usual Android SDK/Studio locations not found by the probe. Installation elsewhere and physical device connection remain unverified. |
| MySQL | `mysql` not found on PATH. No database connectivity, durable disk, or restart test performed. |

## Public provider documentation checked

Public allowances describe candidates, not authenticated account evidence or available capacity. Oracle's current [Always Free resource documentation](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm) reports:

- A1 allowance: 1,500 OCPU-hours and 9,000 GB-hours monthly.
- Combined boot/block storage: 200 GB in the home region; five included volume backups.
- Always-Free-only object storage: 20 GB combined tiers; trial/paid allowance lists 10 GB Standard. Object API allowance: 50,000 monthly requests.
- Outbound transfer: 10 TB monthly.

Check existing tenancy usage, configured limits and eligibility before allocating resources; these figures are not application quotas. Home-region free compute may be out of capacity, and idle instances may be reclaimed. The proposed 1 OCPU/4 GB VM remains a candidate, not a verified allocation. Keep object storage Standard/private and leave allowance headroom.

Oracle's [Free Tier FAQ](https://www.oracle.com/cloud/free/faq/) distinguishes temporary trial credits from ongoing Always Free resources. The application cannot depend on trial-funded paid resources. Actual account type and no-charge controls remain unverified; no paid upgrade or overage was enabled.

## Account gate still needed

| Prerequisite | Current evidence / next verification |
| --- | --- |
| Account and home region | Owner confirmed the account has not been created. Signup and home-region selection are required before account-specific checks. |
| Authenticated access | No usable local access discovered. Locate configured restricted access outside source control, or obtain console evidence without secrets. |
| Eligible compute capacity | No authenticated shape/quota/availability check performed. Confirm allocation availability in the selected home region. |
| Durable MySQL storage | No volume allocation, private host, database version/connectivity, or durable restart evidence. |
| Private cloud storage | No bucket/IAM/configuration evidence; anonymous-denial test not performed. |
| Hard usage limits and headroom | Actual existing usage, account quotas, request/traffic limits, and prevention of paid usage have not been inspected. Billing alerts alone are not evidence of enforcement. |
| Public hostname / Google / device | Separate tasks 1.2–1.3 remain unchecked; no configured hostname/Google clients or exact phone build was provided. |

Resume task 1.1 after the owner completes signup and account/home-region/access details are available. Record actual account evidence and the verified pass/fail decision before marking it complete or provisioning. Cloud streaming/native playback gates and dependent feature work remain pending. No source, root planning documents, or task checkboxes were changed during this original prerequisite check.

## Subsequent local-phase decision — 2026-10-08

The owner explicitly approved local development before Oracle signup. Proposal/design/tasks now include section 0, preserving every original external acceptance gate. The application and tooling rows above describe the original probe, not the current scaffold. See [local-development.md](local-development.md) for subsequent local implementation and validation. The account-specific gate in task 1.1 still has no pass decision, and no cloud resources were provisioned.

## Task 1.1 follow-up and review — 2026-10-08

The owner requested review/fix cycles for the next three tracked tickets. After resolving the four local defects, task 1.1 was revisited with read-only local checks. `scripts/check-prerequisites.ps1 -Scope Cloud` inventories executable/configuration presence without reading keys/config contents, authenticating, or provisioning. It skips network paths to avoid implicit filesystem authentication. It reports `gateCompleted: false`; finding a config is not proof of a working account.

Executed result: OCI executable absent from PATH; workspace/profile config files absent; custom OCI config environment variable unset. No home region, tenancy state, entitlements, available capacity, or authenticated IAM/bucket evidence exists. Task 1.1 remains **blocked and unchecked**. These observations cannot establish whether an account was created elsewhere since the owner's last reply.

Oracle's public [Always Free documentation](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm) was rechecked. Console eligibility/home-region limits and actual usage must be inspected before choosing resources. [Compartment quota documentation](https://docs.oracle.com/en-us/iaas/Content/Quotas/Concepts/resourcequotas.htm) describes administrator-set resource limits; their existence does not prove every cost/operation is constrained in this tenancy.

Once actual account access is available, complete this operational record before provisioning:

| Evidence | Required recorded observation |
| --- | --- |
| Account | Home region and free/trial/upgraded state, with identifiers/secrets redacted. No dependence on temporary paid trial resources. |
| Compute | Eligible candidate shape/image/architecture, proposed CPU/memory, existing usage, remaining entitlement, and availability. Capacity is not inferred from advertised free allowance. |
| Durable disk | Combined existing/planned boot/block volumes plus backup use and headroom; private persistent MySQL/staging/key placement. |
| Private objects | Standard tier, region, permissions plan, byte/operation/traffic budgets including existing use, and eventual anonymous-denial evidence. |
| No-charge enforcement | Account restrictions, concrete resource quotas/IAM and application admission settings; scope/limitations explicitly assessed. Alerts alone are insufficient. |
| Verdict | Pass/fail per acceptance condition, remaining blockers, reviewer/date, and whether any provisioning is authorized within the verified limits. |

Self-review checked absence of credential reads/OCI execution/resource writes, redacted output, and unconditional incomplete-gate status. No paid upgrade, topology substitution, or account creation was attempted. Continue only independent prerequisite investigation; the external cloud gate still blocks dependent deployment/feature acceptance. Tasks 1.2 and 1.3 findings are in [public-origin.md](public-origin.md) and [device-matrix.md](device-matrix.md).

## Prerequisite-tool verification

Executed `node --test tests/scripts/prerequisites.test.cjs`: **10 passed, zero failed/skipped**. Black-box PowerShell checks cover redacted custom-config inventory, explicit incomplete account/device gates, absent-origin blocking, and rejection of HTTP/IP/credential/query/path/local/internal origin inputs. The combined inventory and no-origin probe were rerun after the final local-path/hostname corrections with the same observations. No real public TLS success or physical-device result was simulated.

Across this follow-up: frontend 20, backend 30, prerequisite scripts 10 tests passed; frontend build passed with the existing bundle-size warning. The normal backend build initially encountered a locked executable; the isolated backend test build passed. OpenSpec progress remains **4/46**, and tasks 1.1–1.3 are still false in the CLI's task list. This completes the available investigation/review cycle for these three tickets, not their external acceptance. Resume from task 1.1 when actual account evidence is available.
