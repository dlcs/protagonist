# Analytics

## Context

The DLCS is concerned with serving image (and AV) assets. However, the varying use cases of how these assets are consumed can wildly affect usage patterns. Ideally the DLCS will be able to support these but in reality it needs to offer enough configurable options to cope with different use cases. Be that wholly different uses cases as detailed below, or a temporary spike in traffic due to a new collection being advertised.

For example a Museum may have 100 image that are all large and detailed. Users will tend to open an image and spend a lot of time looking at a single item. This is very different to a user of a Library, where they have 20,000 digitised books, all with multiple pages. These will be a lot smaller than any of the Museum images. How the user interacts will also differ - they may "leaf" through multiple pages in a book, rather that dwelling at a single image.

With this in mind we need deeper insights into how the data is being used to allow it to be handled correctly.

## Architecture

- what is being requested
- what is being orchestrated - how often and what size
- what % of resources are served from cache
- presented in dashboard
- avg length of time in fileshare
- churn of fileshare