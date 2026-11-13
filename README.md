# review-agent

An AI code review agent that reviews GitHub pull requests against a team's own rules and proposes
line-level comments. A developer approves, edits or discards each proposal in a web UI, and only approved
comments are published to GitHub.

## Build and test

Requires the .NET 10 SDK.

```
./verify.ps1
```
