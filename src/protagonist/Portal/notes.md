
A space can be toggled between Manifest mode and normal. It's normal by default. Toggling is a "convert to manifest mode" button. This is replaced by a "Convert to normal mode" button when in manifest mode. Mode is persisted using defaultTags on Space: `dlcs:manifestSpace`

If the space is in Manifest mode, it has two additional links - to the named query Manifest, and to that manifest viewed in the UV. It also does automatic setting of `n1` (and probably `s1` see below) and well as offering _ordering_ UI (see below). If not in manifest mode, Space list is in whatever order the DLCS pages by (for now).

The Dropzone appears in both modes; no need to forbid it for non-manifest spaces - still useful to upload.

Unlike iiif.ly, I now don't think there's an intermediate "iiif-ify these images" call to action. Currently (2021-06-03 08:19), the drop of a file immediately uploads it to server /tmp directory, but doesn't ingest it. I think it should put it to S3 immediately (this might still involve /tmp on the way, or might be streaming the FormFile straight to the storage bucket) AND then ingest immediately. This means that you might occasionally add something to the Space you didn't mean to, but I think that's going to be rare; it's low cost, you just delete it. That button there now feels unnecessary.

(The rows also get a Trashcan/Delete call to action, with a confirmation step (really really?))

The iiif.ly code used to use a batch-to-queue if there was a set of images, or just add them directly if there was only one. (Entity Counter issues?)

Relevant old iiif.ly code:
Create an "image set" (which becomes a manifest):
https://github.com/dlcs/dlcs-net/blob/master/iiifly/Controllers/ImagesController.cs#L128

This concept is unnecessary because the Space is the manifest.

Old DLCS client:
https://github.com/dlcs/dlcs-net/blob/master/iiifly/Dlcs/Dlcs.cs

This means that sometimes when you are looking at the Space page, some of its images are _in progress_.
This is like the Manifestation page on DDS, except that we're not syncing some external view with the DLCS view; there is only the DLCS view.

This means we need a visual indication of which images are in progress (from a Batch report?) so you don't get nervous waiting for their thumbnails to appear. Images ingested here should go on the priority queue, if we use the queue. The DDS page has progress bars (one per batch) and row colours+icons; I don't think we need the progress bars, just the row colours.

### Ordering

`number1` governs the sequence ordering. If it's possible to use the Space as the aggregator, we don't need any other metadata fields set:

`space&canvas=n1&space=p1`

However, that might not be possible and we need to use string1 as well:

`canvas=n1&space=p1&s1=p2` (This NQ already exists, it's called "manifest")

The ordering can be set in the UI. Each row has buttons (small icons):
↑ Move up
↓ Move down
⇈ Move to top
⇊ Move to bottom

You can also drag the rows about in the table. - https://bootsnipp.com/snippets/P2pn5
(no research done on if this is the best approach!)

When in Manifest mode, the rows in the UI are ordered by `n1`
By default any new images are given `n1 = max(n1) + 1` so they appear at the end.

Not sure when ordering changes get persisted - might be a bit much to do it on every change (every drag...)

More likely, the page is aware when an uncommitted ordering change (or changes) has been made and will show a "Save Order" button.
This does a batch patch with the new set of `n1` values.

### Label and Description for Manifest

Well these are just tags too!
 - dlcs:This is my label
 - dlcs:This is the description

Stretch goal - include these in the named query (VERY stretch, this is absolute last priority, UI snags are higher)

### Happy path

1. I have some scans in a folder
2. I drag them onto the dropzone (all at once? one by one?)
3. They trickle into view in the Space list below the dropzone as I watch
4. They have blue coloured bars initially, these gradually all turn white
5. When they are all done I can view my manifest by clicking the link.

*2* suggests that we batch multiple drops (iiif.ly has code for this) and assign n1 based on... what? The time they are received/finished might be misleading because of different file sizes. The file name itself might be a more useful ordering.

*4* suggests that there is some degree of dynamic updating of the page; the row view is refreshed outside of page-level requests. Some background poll, gets data, redraws the table.

We should keep the dropzone visible with the users files in it, rather than force them to reload and lose that view (which also breaks running uploads).
But, as files finish ingest (really finish ingest), we could remove them from the dropzone. This would be nice visual feedback, and nicely delayed to convey the server work going on 

* file is being uploaded, you see Dropzone's progress bar
* file finishes uploading, a second later you see an new row appear in the Space list; it's blue
* the row remains blue for a few seconds (Tizer etc) and then turns white. When it does so, the Dropzone thumbnail for that image also disappears. The image's own thumb is now available in the row.


(Also need to fix .tooltip() which gives the larger thumb on mouseover)
