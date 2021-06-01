
 - The DropZone UI can be enabled for a space in the portal UI - there's a guard so you can't turn this on if > 100 images in the space (or something, 100 ok for now)
 - Enabling it makes the drop target appear at the top
 - In the old iiif.ly, adding images from the desktop to this target drop zone uploaded them to the server for staging (giving them an origin), but didn't trigger a DLCS ingest until you pressed the button (so you assemble files in the drop zone)
 - It also allowed you to drag in external images from browser to browser; old iiif.ly just created a JSON file on disk with a reference to the dragged image, and again, didn't register with DLCS until you pressed the button
 - NEW version can go straight to ingest, whether internal or external, no "iiif-ify" button  
 - When enabled, things in that space are presented in a reorderable list (reordering makes batch metadata updates), underneath the drop zone
 - This is an entity query? Or API paging?
 - The space also has a prominent manifest link that goes to the named query (edited) 
 - string1 is used by the NQ; all images get the space id as string 1... unless you can do `manifest=space&canvas=n1&space=p1` ?

