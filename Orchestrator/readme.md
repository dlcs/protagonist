## Routes

The following routes are defined for YARP to handle:

* img_options - handles `OPTIONS` requests for `/iiif-img/`, proxied to deliverator
* img_infojson - handles `GET` requests for `/iiif-img/{cust}/{space}/{image}/info.json` requests, proxied to deliverator
* img_only - handles `GET` requests for `/iiif-img/{cust}/{space}/{image}` requests, proxied to deliverator as these are info.json (without info.json)
* thumbkey - handles `GET` requests for `/getThumbKey/` requests, proxied to deliverator
* thumbkey_exact - handles `GET` requests for `/getThumbKeyExactSize/` requests, proxied to deliverator
* requiresAuth - handles `GET` requests for `/requiresAuth/` requests, proxied to deliverator      
* mapCustomerToID - handles `GET` requests for `/mapCustomerToID/` requests, proxied to deliverator

> Note - YARP is initially acting as a replacement to NGINX, most of these routes will be removed.