# Security Policy

## Supported Versions

Security updates are provided for the latest stable release of each
DiagnosticCatalog release train (`lib`, `sonar`, `netanalyzers`, `stylecop`, `codestyle`, `xunit`,
`nunit`, `mstest`, `trimming`, `aspnetcore`, `syslib`, `roslyn`, `publicapi` —
see [CONTRIBUTING.md](CONTRIBUTING.md)). The trains version independently, so
"latest stable" is read per train, not across the repository.

| Version               | Supported   |
| --------------------- | ----------- |
| Latest stable release | Yes         |
| Previous releases     | No          |
| Pre-release versions  | Best effort |

Users of unsupported versions should upgrade to the latest stable release before reporting a vulnerability.

## Reporting a Vulnerability

Please do not report security vulnerabilities through public GitHub issues, discussions, pull requests, or other public channels.

Report suspected vulnerabilities privately using GitHub's security advisory system:

[Open a private vulnerability report](https://github.com/Reefact/diagnostic-catalog/security/advisories/new)

Please include as much of the following information as possible:

* The affected package and version.
* The environment in which the vulnerability was observed.
* A description of the vulnerability and its potential impact.
* The steps required to reproduce the issue.
* A minimal proof of concept, when appropriate.
* Any known mitigations or workarounds.
* Whether the vulnerability has already been publicly disclosed.

Do not include secrets, personal data, access tokens, or information belonging to third parties in the report.

## What to Expect

After receiving a vulnerability report, the maintainers will make a reasonable effort to:

* Acknowledge receipt within 3 business days.
* Provide an initial assessment within 7 business days.
* Provide a status update at least every 14 days while the vulnerability remains unresolved.
* Coordinate a fix and public disclosure within 90 days whenever reasonably possible.

These timelines may change depending on the severity, complexity, exploitability, and availability of a safe fix. Any significant change to the disclosure timeline will be discussed with the reporter.

The reporter is asked to keep the vulnerability confidential until a fix or mitigation is available and the coordinated disclosure has been completed.

## Scope

Examples of issues that may qualify as security vulnerabilities include:

* Unauthorized access to data or functionality.
* Arbitrary or unintended code execution.
* Authentication or authorization bypasses.
* Exposure of sensitive information.
* Compromise of data integrity or availability.
* Vulnerabilities affecting the package build or release process.
* Supply-chain vulnerabilities introduced by the project.

The following are generally not considered security vulnerabilities:

* Regular bugs without a security impact.
* Feature requests.
* Documentation errors.
* Problems that affect only unsupported versions and cannot be reproduced on a supported version.
* Vulnerabilities in third-party dependencies that do not affect this project in practice.

Non-security issues should be reported through the public GitHub issue tracker.

## Disclosure Process

Once a vulnerability has been confirmed, the maintainers may create a private GitHub Security Advisory to coordinate the fix.

After a fix or appropriate mitigation is available, the maintainers may publish an advisory containing:

* A description of the vulnerability and its impact.
* The affected and corrected versions.
* Available mitigations or workarounds.
* Upgrade instructions.
* A CVE identifier when appropriate.
* Credit for the reporter, unless anonymity was requested.

Public disclosure should normally occur only after users have access to a corrected release or an effective mitigation.

## Recognition

Security researchers who report vulnerabilities in good faith will be credited in the published advisory when appropriate, unless they prefer to remain anonymous.

This project does not currently operate a paid bug bounty program.
