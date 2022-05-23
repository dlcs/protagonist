# Image Server Hosting

* Status: proposed
* Deciders: Donald Gray, Tom Crane
* Date: 2022-03-18

Technical Story: [#241](https://github.com/dlcs/protagonist/issues/241)

## Context and Problem Statement

How can we host our image-servers with shared-storage that is fast enough to serve image requests from, avoiding the need to co-locate resources? 

We have decided on Cantaloupe which can stream resources from S3 directly so this may be an option.

This ADR is relevant to _tile serving_ only. Not the "special-server".

## Decision Drivers

* Performance - shared storage must be fast enough for image-servers to serve requests from.
* Maintenance overhead - low cost of ownership, ideally a managed service to avoid overhead of patching, maintenance, monitoring etc.
* Price - the above needs to be balanced with price

## Considered Options

* Cantaloupe on EC2 instances using `S3Source` and caching on own volume.
* Cantaloupe on EC2 instances using shared [AWS FSx for Lustre](https://aws.amazon.com/fsx/lustre/) volume, with Orchestration happening.
* Cantaloupe on EC2 instances using shared [GlusterFS](https://docs.gluster.org/en/latest/) volume, with Orchestration happening.
* Cantaloupe on EC2 instances sharing an [EBS multi-attach](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ebs-volumes-multi.html) volume.
* Cantaloupe on single ECS host sharing underlying mounted EBS.

## Decision Outcome

Chosen option is: "Cantaloupe on EC2 instances using shared AWS FSx for Lustre volume, with Orchestration happening.".

This gives the benefits of shared storage without the management overhead.

AWS manage the underlying Lustre nodes and provide a volume that can be mounted. 

FSx for Lustre is likely more expensive per month than managing a GlusterFS volume but is significantly easier to manage ("likely" as with GlusterFS you need to manage instances and storage so would depend on the number and size of instances).

Could leverage S3 [data repositories](https://docs.aws.amazon.com/fsx/latest/LustreGuide/fsx-data-repositories.html) in the future.

### Positive Consequences

* Managed service, low maintenance.
* Good performance.
* Ease of scaling instances.
* Volume resizable via console or CLI (with some potential downtime).

### Negative Consequences

* Single AZ only + charges for cross-AZ data transfer.
* Sizing is 1.2TB, or increments 2.4TB.
* Unfamiliar offering.
* [Availability/durability](https://aws.amazon.com/fsx/lustre/faqs/#Availability_and_durability) during one day and one week are 99.9% and 99.4% respectively. Would need to handle possiblity of expected file not there.

## Pros and Cons of the Options

### Cantaloupe on EC2 instances using `S3Source` and caching on own volume.

#### Positive Consequences

* Low maintenance.
* Ease of scalability, can add or remove nodes without worrying about cleaning up.
* Less complex, no scavenger or orchestration processing required.
* Better performance than IIP, both median and average performance better. No performance spikes and less errors than IIP.
* Ease of scaling.

#### Negative Consequences

* `S3Source` is slower than `FilesystemSource`
* Caches could get out of sync.
* Slower performance than orchestrating instances.

### Cantaloupe on EC2 instances using shared GlusterFS volume, with Orchestration happening.

#### Positive Consequences

* Good performance.
* Would allow ease of scaling EC2 instances as each instance would need to attach volume then good to go.
* Could be multiple AZ, supports multiple different [volume configurations](https://docs.gluster.org/en/latest/Administrator-Guide/Setting-Up-Volumes/).

#### Negative Consequences

* Maintenance overhead (patching, monitoring etc)
* More moving parts to manage.
* Lack of experience managing GlusterFS setup.

### Cantaloupe on EC2 instances sharing an EBS multi-attach volume.

#### Positive Consequences

* Disk performance as EBS needs to be io1 or io2.
* Conceptually simple, can be treated like a normal mounted EBS volume.
* Ease of scaling.

#### Negative Consequences

* Maintenance overhead as need to use cluster-aware filesystem like [gfs2](https://documentation.suse.com/sle-ha/15-SP1/html/SLE-HA-all/cha-ha-gfs2.html) or [ocfs](https://documentation.suse.com/sle-ha/15-SP1/html/SLE-HA-all/cha-ha-ocfs2.html).
* Possibility of corruption and/or data issues between attached instances if not managed correctly.
* Relatively new, not generally available in all AWS regions.
* Single AZ for both volume and connected instances.

### Cantaloupe on single ECS host sharing underlying mounted EBS.

#### Positive Consequences

* Simple + familiar.

#### Negative Consequences

* Difficulty scaling outwith confines of box.
* Co-location issues with other services.

## Links

* [Performance metrics](https://github.com/dlcs/protagonist/issues/241#issuecomment-1069246303)