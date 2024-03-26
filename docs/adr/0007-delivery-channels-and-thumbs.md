# Delivery Channels and Thumbs

* The `thumbs` delivery channel is ALWAYS serviced by the same application - [Thumbs](https://github.com/dlcs/protagonist/tree/main/src/protagonist/Thumbs). And more generally, the routing is simple - one application per delivery channel.
* The `thumbs` delivery channel, and therefore [Thumbs](https://github.com/dlcs/protagonist/tree/main/src/protagonist/Thumbs), always serves jpeg images no matter what the asset is. It exposes a level-0 IIIF Image service with `sizes` only. If we want moving-picture thumbs as mp4s or audio-clip thumbs, that's a new delivery channel, called something else. `clips` for example.
* They are never access controlled, so thumbs don't get made for access controlled assets.
* The point is speed, so Engine always makes the configured sizes at ingest-time. Engine can make thumbs for any asset as long as it knows how to.

This means the *policy* for the thumbs channel is always the [same syntax](https://github.com/dlcs/protagonist/issues/66#issuecomment-1609800287) - e.g., `["!1024,1024", "880,"]` because it's always making the same kind of thing. We might have a couple of extensions to that syntax to additionally specify a page index if the asset is a pdf, or a `t` time point if it's a video. (does that mean you can supply a one-off customised policy for an asset as well as a pre-canned policy id?)

So the Thumbs application never knows about Cantaloupe. It's not making decisions based on the asset content type and serving a jpeg, or proxying to SpecialServer, or doing something else.

We might have more complex dynamic image service generation for AV per frame taking advantage of live-proxying to cantaloupe, but that's a different delivery channel that we could investigate later.

Thumbs always serves a level-0 IIIF Image Service. Engine makes the thumbs using Special Server for images, pdfs and videos. Engine could make thumbs for other types of asset differently in future but Thumbs doesn't know or care. The API caller - who registers the asset - doesn't need to instruct Engine with a policy-per-content-type.

What does engine or API do if the caller asked for the `thumbs` channel for an asset that we can't make thumbs for? E.g., a spreadsheet? Do we just accept it but Engine will skip over it and therefore the /thumbs/ app will return 404 for the info.json? We may be capable of providing thumbs for a particular format later, so the caller could register it in hope and anticipation.




