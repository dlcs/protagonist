# Managing dependency updates with Dependabot

* Status: proposed
* Deciders: Donald Gray, Tom Crane
* Authors: Gary Tierney
* Date: 2022-08-19

Technical Story: https://github.com/dlcs/protagonist/issues/273

## Context and Problem Statement

We need a process for handling dependabot notifications, we should receive them for both NuGet and pip.
Ideally this will involve as little interaction as possible (unless required, like an update causes tests to fail).

## Decision Drivers <!-- optional -->

* Little to no developer overhead
* Mitigating risk of automatically applying big changes

## Considered Options

* Automatically merge all `patch` versions to `develop`
* Automatically merge all incoming dependency changes to a dependency staging branch for manual review

## Decision Outcome

Pending

## Pros and Cons of the Options <!-- optional -->

### Automatically merge all `patch` versions to `develop`

A majority of pull requests created by dependabot are simple hotfixes or patches that only increment the patch version.
We rely on SemVer guarantees from upstream dependencies to identify simple changes.

With trivial changes being automatically dealt with only significant changes to dependencies are remaining.
These need to be manually reviewed but occur far less frequently than simple patches.
However, as confidence in the process grows, this could be expanded to include minor version changes.

[Example from official GitHub documentation](https://docs.github.com/en/code-security/dependabot/working-with-dependabot/automating-dependabot-with-github-actions#enable-auto-merge-on-a-pull-request)

#### Positive Consequences

* Low developer overhead
* Highlights large changes that should be reviewed
* Non-trivial changes can be cherry-picked
 
#### Negative Consequences

* Requires adherence to SemVer at a basic level for a majority of upstream dependencies. This varies between ecosystems.

### Automatically merge all incoming dependency changes to a dependency staging branch for manual review

Funnels all incoming dependabot changes into a single `dependencies` staging branch.
This branch will be reviewed by developers on a given schedule (e.g. weekly basis) and the changes from that period of time will be merged to `develop`

[Example from @stephenwf's capture model work](https://github.com/digirati-co-uk/capture-models/blob/master/.github/workflows/update-dependencies.yml)

#### Positive Consequences

* Checking individual dependency updates is no longer necessary
* Almost zero developer overhead in the best case (assuming no breakage from dependency changes)

#### Negative Consequences

* Large developer overhead in the worst case (i.e. bumping dependency X makes bumping dependency Y fail)
* Checking individual dependency updates is no longer possible
* Ignoring certain dependency versions requires going out-of-band from the workflow or crafting dependabot configurations

