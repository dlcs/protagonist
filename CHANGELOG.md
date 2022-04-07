# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased](https://github.com/dlcs/protagonist/compare/master...develop)
### Added

- New Orchestrator application using [Yarp](https://microsoft.github.io/reverse-proxy/).
- Handle `/iiif-img/` paths in Orchestrator (see [#193](https://github.com/dlcs/protagonist/issues/193) and [#162](https://github.com/dlcs/protagonist/issues/162)).
- Generation of manifests from NamedQueries in IIIF 2.1 or 3 (see [#169](https://github.com/dlcs/protagonist/issues/169) and [#175](https://github.com/dlcs/protagonist/issues/175)).
- Generation of single item manifests in IIIF 2.1 or 3 (see [#183](https://github.com/dlcs/protagonist/issues/183)).
- PDF generation (see [#170](https://github.com/dlcs/protagonist/issues/170)).
- Handle `/pdf-control/` paths in Orchestrator (see [#171](https://github.com/dlcs/protagonist/issues/171)).
- Handle `/iiif-av/` paths in Orchestrator (see [#163](https://github.com/dlcs/protagonist/issues/163)).
- Handle `/file/` paths in Orchestrator.
- Handle clickthrough auth.
- Support "CustomHeaders" specified at Customer level (see [#168](https://github.com/dlcs/protagonist/issues/168)).
- Basic implementation of new Portal application.
- Initial implementation of new API application.

## [1.0.0] - 2020-03-26
### Added
- New Thumbs service.