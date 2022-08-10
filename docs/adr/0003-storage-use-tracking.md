# Shared Storage Use Tracking

* Status: proposed
* Authors: Gary Tierney
* Deciders: Donald Gray, Tom Crane
* Date: 2022-08-09

Technical Story: [#280](https://github.com/dlcs/protagonist/issues/280)

## Context and Problem Statement

How can we identify frequently/recently accessed assets to be retained in the shared-storage volume, while providing an approach to evict the most appropriate assets?

This ADR relates only to tracking asset use.

## Decision Drivers

* Performance - must be able to identify assets for eviction faster than the volume is being filled.
* Consistency - ideally all uses should be tracked, rather than last use time.

## Considered Options

* Naive filesystem walk recording last access time
* Process [Lustre MDS](https://github.com/DDNStorage/lustre_manual_markdown/blob/master/03.01-Monitoring%20a%20Lustre%20File%20System.md#lfs-changelog) changelogs
* Record filesystem changes with `inotify`

## Decision Outcome

### _Pending_

## Pros and Cons of the Options

### Naive filesystem walk recording last access time

This is the same approach that is currently implemented.
A single thread periodically walks a filesystem root and records the `atime` of every file in the tree.

#### Positive Consequences

* Simple implementation.
* Low maintenance.
* Retains the same familiar (albeit potentially flawed) approach as the current scavenger.

#### Negative Consequences

* Being able to immediately evict deleted assets from a cache requires a diff with a local file tree and a past view of the file tree.
* Does not scale and is not fast. Filesystem walk speed is bottlenecked by IO system calls and the resulting networked calls from the Lustre driver. Using multiple cores will not help speed up processing time significantly.
* Accesses/uses between filesystem walks will not be recorded.

### Process [Lustre MDS](https://github.com/DDNStorage/lustre_manual_markdown/blob/master/03.01-Monitoring%20a%20Lustre%20File%20System.md#lfs-changelog) changelogs

As of Lustre 2.0 the MDS has a changelog feature that can record [all operations](https://github.com/DDNStorage/lustre_manual_markdown/blob/master/03.01-Monitoring%20a%20Lustre%20File%20System.md#lustre-changelogs) that happen in a Lustre filesystem.
Note that access time modifications and open events are not recorded by default, but can be recorded by setting the changelog mask:

```shell
> # lctl set_param mdd.lustre-MDT0000.changelog_mask=OPEN ATIME CREAT UNLINK
```

The changelog is a simple fixed size ring buffer that will begin to purge old records as the storage fills.
However, once a changelog reader has been registered Lustre will stop purging records that have not been marked as seen by all readers.

Every record stored contains the following information for every event:
```
operation_type(numerical/text) 
timestamp 
datestamp 
flags 
t=target_FID 
ef=extended_flags
u=uid:gid
nid=client_NID
p=parent_FID 
target_name
```

Caveat: `target_name` refers to the _basename_ of the file being accessed, retreiving the full path requires a lookup on `parent_FID`.

#### Positive Consequences

* Can scale across multiple nodes and cores.
* Translating open/create/delete events into a reference counting system used for a cache is straight forward.
* The only system calls made are to resolve parent pathnames, which can be cached. Filesystem driver overhead is minimal.
* Scaling processing across multiple cores is trivial by assigning each a batch size.

#### Negative Consequences

* Custom data format, requires writing a parser for the CLI output.
* Very few people seem to interact with Lustre directly at this level, making documentation scarce.
* When cache state is lost the naive filesystem walk described in the first approach is required to build a view of the filesystem. This will generate Lustre changelog records of its own, a mechanism to filter those out is needed (can be UID/GID).

### Record filesystem changes with `inotify`

`inotify` is a Linux kernel mechanism for recording filesystem events.
It is only capable of recording events for local VFS devices (i.e. you will only get events for the machine you're using `inotify` on) and is inapplicable here.

## Additional Consideration

### Priority Assets

It may be desirable to provide a set of assets that should never or rarely be evicted, regardless of time or frequency of access.
A separate set of priority files can be tracked by both solutions and excluded from the eviction set, but a mechanism for translating orchestrator asset requests into filesystem object references would be needed.