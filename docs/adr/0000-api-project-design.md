# API Project Design

* Status: proposed
* Deciders: Donald Gray
* Date: 2020-11-10

## Context and Problem Statement

### Context

How best can we structure the API project to make it easy for developers to navigate and comprehend.

## Considered Options

* Organise by Feature and use MediatR
* Traditional ASP.NET/MVC layout

## Decision Outcome

"Organise by Feature and use MediatR", because we can use MediatR to encapsulate the various use-cases of the API. Together with feature folders this should allow developers to more easily identify the capabilities of the API and target the section they need to.

## Pros and Cons of the Options

### Organise by Feature and use MediatR

MediatR doesn't do a huge amount in itself but allows controllers to easily fire off commands and request (will use 'request' for clarity from now on) without knowing how they are handled. The input/output of each request is clearly identified in the request contract. It allows a pipeline to be built for processing requests inside of MediatR, which can help with cross cutting concerns like timing or logging and helps keep handler logic clean.

If we name the requests after use cases, and use the same name for the containing .cs file, it will help to document the capabilities of the API e.g. `IngestImage`, `ReingestImage`, `CreateSpace`, `DeleteCustomer`.

This leads on to feature folders, these can help contain everything related together rather than having everything spread out. At a glance you can see this project handles Images/Spaces etc (rather than at a glance seeing it handles Controllers/Models/Views as is the traditional setup) E.g.

```bash
Features/
  Image/
    - ImageController.cs
    Commands/
      - ReingestImage.cs
      - IngestImage.cs
      - DeleteImage.cs
    Request/
      - GetImages.cs # this 1 command could allow querying by stringX, space, customer etc rather that lots of granular commands. Handler sorts logic
  /Space
    - SpaceController.cs
- Program.cs
- Startup.cs
```

Controllers are very thin using this approach, they only need to know the request to construct and have a dependency in `IMediatr` to send this command.

```cs
public class ImageController : Controller
{
  public ImageController(IMediatr mediatr){
    this.mediatr = mediatr
  }
  public Task<ActionResult> ReingestImage(string imageId){
    var ingestImageCommand = new IngestImage(imageId);
    var result = await mediatr.Send(ingestImageCommand);
    // handle result/set status code etc
  }
}
```

#### Positive Consequences

* Better organisation of application, related components together. Easier for a new developer to comprehend what's happening.
* Use-cases of system documented by classes.
* Prevent dependency explosion in controllers; controller only constructs and sends request.
* Ability to use pipeline behaviours with generic constraints to only target specific requests (this would involve using a different IoC container).

#### Negative Consequences

* Debugging. In the above example, stepping into `await mediatr.Send(ingestImageCommand);` would take you to MediatR framework code, rather than the handler for ingesting an image. This can be mitigated by having Request + Handler in same classfile but can make debugging less intuitive.
* Overuse of pipeline behaviours can lead to obfuscated and difficult to understand code. Any pipeline behavious should be well documented in readme etc.
* Overuse of `mediatr.Send(ingestImageCommand);` (e.g. controller sends to handler which sends to handler which sends to handler etc.) is awful. Best to try to stick to one single `.Send()` from the controller only.

### Traditional ASP.NET/MVC layout

This is the default project layout that is configured by default when starting a new project. Classes are stored by type (`/Controller`, `/Model`) rather than by functional area.

#### Positive Consequences

* Familiar, expected layout. Any .net developer will be aware of this approach

#### Negative Consequences

* Looking at the solution shows this is a MVC application but not _what_ it does.

## Links

* [MediatR Project](https://github.com/jbogard/MediatR)
* [MediatR Pipeline Behaviours](https://github.com/jbogard/MediatR/wiki/Behaviors)
* [MSDN Article on Feature Slices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2016/september/asp-net-core-feature-slices-for-asp-net-core-mvc)
