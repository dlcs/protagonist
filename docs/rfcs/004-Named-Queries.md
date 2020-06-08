# Named Queries

## Current

You can ask the DLCS to select from images where one of the metadata values of the image matches some value, then order by one of the other values. The DLCS can return this query as a IIIF Manifest, or a PDF.

Wellcome use the PDF feature, and we use the manifest named query extensively on other projects to generate skeleton IIIF for further enrichment, starting with just the images and a little bit of metadata.


## Proposed

* Extract and refactor to new service
* Projections: IIIF 2, 3; PDF
* Revisit the named query syntax, and the tools to build named queries in the DLCS portal
* Pass in link to IIIF Manifest that _uses_ the Image API endpoints; PDF projection reads the text content of canvases and includes it in the PDF (Fireball enhancement) => proper text in PDF
* Research need for other projections like ePUB, MOBI, etc.

Note that there is a class of named queries that doesn't require that the DLCS hosts the images, but that might not be a named query any more.
