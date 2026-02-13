# Compose

This folder includes resources to run DLCS via docker-compose.

Copy `.env.dist` and to `.env` and set environment variables to customise. The provided `.env.dist` will work for running full docker-compose environment.

## `docker-compose.yml`

Run full stack locally, including localstack in place of AWS.

_There is a limitation with fireball that it will always write to AWS so PDF generation won't function correctly_

## `docker-compose.local.yml`

This contains external dependencies for running the dotnet apps locally.

## `docker-compose.engine.yml`

This contains external dependencies for debugging the Engine.

[Appetiser](https://github.com/dlcs/appetiser) requires Kakadu binaries. By default this expects `kdu_src/kdu.tar` to exist. See Appetiser readme for alternative approaches for supplying Kakadu.

## `docker-compose.orchestrator.yml`

This contains external dependencies for debugging Orchestrator.

## Volumes

Services via docker-compose and via the IDE need to share a common directory (e.g. Orchestrator needs to place file in same folder that image-server reads from).

For this the `$HOME` envvar is used in a few volume definitions. This serves as a central place where the current user has read/write access to, the intention being that it points to the current users home-directory.

This may need to be setup on current system.