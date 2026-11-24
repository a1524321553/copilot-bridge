# Local customization maintenance

## Branch and upstream

Use `codex/local-customizations` for local work. The upstream repository is
`https://github.com/hooyao/copilot-bridge`; do not commit local changes directly
on `main` or work from a detached HEAD.

The personal fork is `https://github.com/a1524321553/copilot-bridge`.
`origin` points to this fork for pushing local branches; `upstream` points to
`hooyao/copilot-bridge` for fetching original-project changes. The maintenance
branch tracks its same-named branch on `origin`, while local `main` tracks
`upstream/main`. Repository-local GitHub authentication uses the personal
account, and the repository's default push remote is `origin`.

On 2026-11-24, local changes from `749e435` (v0.5.30-beta) were checkpointed in
`d90c144`, then upstream `4e4050a` (v0.5.31-beta, PR #100) was merged. The
`codex/customizations-pre-main-20261124` branch preserves that checkpoint.
The original `feature/live-models-endpoint` branch remains at its original base.
Nothing was pushed to upstream and the installed bridge was not replaced.

## Upstream additions adopted

PR #100 adds exact Responses support for GPT-6.2 Astra/Sol, GPT-5.6 Luna Utility,
and MAI-Code 2. Both GPT-6.2 models have complete pinned official Codex picker
resources, minimum client version 0.166.0, and low/medium/high/xhigh/max choices.
Ultra is filtered using live backend facts. Inconsistent discovery capacities
retain official context and compaction values. Utility and MAI-Code 2 establish
explicit-ID inference, without invented Codex picker metadata.

Upstream removes five exact profiles after documented retirement probes:
`gpt-5.4`, `gpt-5.4-mini`, `gpt-5.5`, `gpt-5.6-terra`, and `gpt-5-mini`.
There are now 16 exact Responses profiles. Grok 4.6/4.7 observations are recorded
as backend facts; no new Grok route or native Codex compatibility is promised.

The SDK pin is now .NET 10.0.404. CI and releases use macos-15 arm64 instead
of the retired macos-14 hosted runner. See
[the upstream verification report](model-sync-2026-11-18-verification.md).

## Customizations retained

| Customization | Upstream status at 4e4050a | Decision |
| --- | --- | --- |
| Generic `GET /v1/models` account discovery | Upstream provides the Claude-filtered `/cc/v1/models` and strict `/codex/models`, but no generic route | Keep for inspecting all live account model IDs and capabilities |
| Controlled `gpt-5.6-sol-fast` picker alias | Exact inference profile exists upstream; no picker alias or official same-slug resource is supplied | Keep the reviewed Sol-resource alias, gated by exact validated live discovery, picker flag, Responses support, display name, and coherent limits |
| Empty default `Routing.Locations` | Upstream still ships `gpt-5.6-sol -> gpt-6-astra` | Keep direct selection so the chosen model retains its identity and the Sol picker row does not depend on another model's inconsistent limits |
| Packaged-default and explicit-Astra test scenarios | Required by our differing default route policy | Keep the scenarios and their regression tests |
| Installed-client/AOT GPT-6.2 behavior case | Upstream's November case targets its reviewed client and custom OAuth credential source | Keep the configurable current-client/version-3-credential case as a local execution check |

The Sol Fast alias changes only the official Sol slug and live display name,
then applies reviewed backend-owned availability, capacity and effort metadata.
It does not fabricate a new instruction template. A future complete official
same-slug resource takes precedence. Remove the alias once upstream or the
official client catalog supplies equivalent complete picker support.

Updates preserve existing installations' whole `Routing.Locations` array.
Changing this branch's packaged default does not clear an operator's existing
Sol-to-Astra alias.

## Customizations retired

The local GPT-6.2 routing, profiles, official supplements, Ultra filtering, and
backend snapshot changes are replaced by upstream's implementation. Those
production files now match upstream exactly, apart from the independent Sol Fast
extension in the catalog projector. The duplicate `ResponsesProbe.Gpt62.cs` and
`Gpt62ModelCatalogTests.cs` were removed; upstream's November probes, request
tests, picker tests and snapshot guards cover those contracts. Previous local
implementation and evidence remain recoverable from the checkpoint and ignored
`tests/behavior-runs/` directory.

## Account-specific live result

The 2026-11-24 account check advertises GPT-6.2 Astra/Sol, Sol Fast, and MAI-Code 2.
GPT-5.6 Luna Utility is absent from discovery and an exact inference probe returns
`model_not_available_for_integrator` for `vscode-chat`. This does not establish
global retirement. Its upstream profile remains intact. Consequently the full
November live comparison is not green on this account: it stops at Utility's
integrator denial. Do not regenerate the backend snapshot from this denial or
weaken the test to hide it. MAI-Code 2 accepts live requests on this account.

## Verification on the merged tree

- Solution build: zero warnings and errors; all 2020 unit tests passed.
- Windows x64 Native AOT publish succeeded for the bridge and updater. The
  bridge is 15,409,664 bytes; no runtime installation was changed.
- Real installed Codex 0.171.0-alpha.13 listed and selected GPT-6.2 Sol/Astra
  through the JIT bridge, completed the CSV/Decimal task, and echoed six/seven
  matching custom exec outputs respectively.
- The same client listed and selected Sol Fast through the new AOT artifact,
  completing five matching call/output pairs (one namespaced list_agents and
  four custom exec calls). The packaged-default AOT case visibly listed exact
  GPT-5.6 Sol, used that exact upstream model, and completed three custom exec
  round-trips. Both expected canaries were present in real tool results.
- All four manifest-selected client SQLite windows were nonempty and had zero
  router/dispatch fatals, ERROR rows, or retries. No stdout execution aborts or
  unmatched tool calls were found.
- The captured live-catalog projection check passed. The full November live
  comparison failed on Utility's account/integrator denial as described above;
  this failure remains visible. Separate live effort observations confirmed
  MAI-Code 2 acceptance and the same exact Utility denial.

Private evidence is under ignored `tests/behavior-runs/maintenance-20261124/`.
The run manifests are
`codex-gpt62-picker-gpt-6.2-sol-20261124-053336-894.json`,
`codex-gpt62-picker-gpt-6.2-astra-20261124-053419-086.json`,
`codex-sol-fast-model-support-20261124-053441-373.json`, and
`codex-gpt56-published-defaults-20261124-053505-031.json` in the manifests
directory. Client logs and complete traces remain untracked.

## Future synchronization

Commit or otherwise preserve in-progress work before merging. Then run:

```powershell
git switch codex/local-customizations
git status --short
git fetch upstream --prune --tags
git merge --no-ff upstream/main
dotnet build CopilotBridge.slnx
dotnet test tests/CopilotBridge.UnitTests
git push
```

Resolve model-catalog overlaps in favor of current upstream facts, retaining
only the independent customizations above. Do not infer model retirement from
discovery absence. Use the `copilot-model-sync` and `real-client-verify` skills
for protocol/profile changes and native-client acceptance. Review both parent
diffs before committing a merge, and confirm that `upstream/main` is an ancestor
of the resulting branch. The current task's runtime installation remains
operator-controlled; building or merging does not deploy it.
