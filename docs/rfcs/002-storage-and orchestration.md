# Storage and Orchestration

Revisit research into desired storage behaviour (assess the market).

Not S3fs-Fuse; we want a cofigurable volume backed by S3 where the orchestration is managed by AWS, or Azure, or whoever offers the service.

_Like_ EFS, but don't need the globally available bit;
_Like_ EFS but fast enough to be useful for image serving/orchestration (write wait too long)

