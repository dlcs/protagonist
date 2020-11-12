# The shape of traffic

It’s obvious that the number of site visitors is related to the load on infrastructure, and that we need to consider these raw human numbers when designing systems for asset delivery. The more visitors, the more image requests.

It’s also obvious that user interfaces generate load on servers, and choices made in the design of user interfaces have a bearing on the contribution that any individual user will place on server infrastructure. This is true of anything on the web, but is amplified when delivering digital objects. Different user interfaces generate very different kinds of load. Different digital objects generate different types of load, within the same user interface, because they have more or bigger images than others. Choices made by designers in how users interact with digital objects, individually or in aggregate, and choices made by client-side developers when implementing those designs, can have dramatically different outcomes for the scale and shape of traffic that user activity unleashes on the infrastructure, especially where image servers are involved. 

Another factor to consider is the distribution of requests for images across a collection. Some images are viewed far more than others. One of the treasures of an Art Museum, versus the 517th page of an obscure book in a Library.

We can’t design infrastructure without understanding this traffic. Sometimes, there will be a tradeoff between cost and performance. We need to understand the traffic to inform design decisions.

* How will it respond to these different types of load?
* Is it optimised for one type of experience over another?
* How do we ensure that all the different experiences of digital objects make users happy (at least as far as performance goes)?

Different usage patterns require different allocation of resources and possibly different architectures. Usage patterns may change gradually over time, or dramatically overnight (a library website redesign, a new viewer, new functionality making more use of images). Infrastructure that has been carefully tuned for one set of load patterns might have to adapt to new ones.

As an open standard, IIIF adds third-party consumption of assets into the picture. That is, even if you know the various user experiences you are offering to consume your IIIF resources, you don’t know what experiences are being offered by others using assets served from your infrastructure. While this is on the whole a good thing, you would like to know what other people are doing with your content, and also guard your infrastructure against abuse.

[Interaction Patterns](https://github.com/dlcs/protagonist/issues?q=is%3Aissue+label%3A%22Interaction+Pattern%22) are captured in GitHub.

