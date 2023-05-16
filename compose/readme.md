# Compose

This folder includes resources to run DLCS via docker-compose.

Copy `.env.dist` and to `.env` and set environment variables to customise. The provided `.env.dist` will work for running full docker-compose environment.

## `docker-compose.yml`

Run full stack locally, including localstack in place of AWS.

_There is a limitation with fireball that it will always write to AWS so PDF generation won't function correctly_

## `docker-compose.local.yml`

This contains external dependencies for running the dotnet apps locally.