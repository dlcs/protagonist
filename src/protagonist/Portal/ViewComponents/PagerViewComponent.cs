using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Portal.ViewComponents;

public class PagerViewComponent : ViewComponent
{
    public const int DefaultPageSize = 100;
    public const int DefaultWindow = 7;
    public const int DefaultEnds = 3;

    public async Task<IViewComponentResult> InvokeAsync(PagerValues values)
    {
        if (values.Total < values.Size)
        {
            // Nothing to page
            return Content(String.Empty);
        }

        // Store the model in the request for duplicated pagers
        var model = HttpContext.Items[nameof(PagerViewComponent)] as PagerModel;
        if (model == null)
        {
            var path = Request.Path;
            model = new PagerModel {Links = new List<Link>()};
            int pages = values.Total / values.Size;
            if (values.Total % values.Size > 0) pages++;
            // we don't want to loop through the pages - there could be 100,000 of them
            // instead we want the first few, then an ellipsis,
            // then the current "window", then another ellipsis, then the end
            // but only if there are enough pages to justify this.
            // prev | 1 2 3 ... 45 46 [47] 48 49 ... 654 655 656 | next
            if (pages < (2 * values.Ends) + values.Window + 2)
            {
                for (int linkPage = 1; linkPage <= pages; linkPage++)
                {
                    AddLinkToModel(model, path, linkPage, 
                        values.Index, values.Size, 
                        values.OrderBy, values.Descending);
                }
            }
            else
            {
                int windowStart = values.Index - (values.Window / 2);
                for (int linkPage = 1; linkPage <= values.Ends; linkPage++)
                {
                    AddLinkToModel(model, path, linkPage, 
                        values.Index, values.Size, 
                        values.OrderBy, values.Descending);
                }

                if (windowStart > values.Ends + 2)
                {
                    // we're not into the window yet, add an ellipsis
                    model.Links.Add(new Link {Page = null});
                }
                else
                {
                    // we're into the window already
                    AddLinkToModel(model, path, values.Ends + 1, 
                        values.Index, values.Size, 
                        values.OrderBy, values.Descending);
                }

                windowStart = Math.Max(values.Ends + 2, windowStart);
                int tail = pages - values.Ends - values.Window;
                if (windowStart >= tail)
                {
                    // just run through to the end
                    for (int linkPage = tail; linkPage <= pages; linkPage++)
                    {
                        AddLinkToModel(model, path, linkPage, 
                            values.Index, values.Size, 
                            values.OrderBy, values.Descending);
                    }
                }
                else
                {
                    for (int linkPage = windowStart; linkPage < Math.Min(windowStart + values.Window, pages); linkPage++)
                    {
                        AddLinkToModel(model, path, linkPage, 
                            values.Index, values.Size, 
                            values.OrderBy, values.Descending);
                    }

                    if (model.Links.Last().Page < pages - values.Ends)
                    {
                        model.Links.Add(new Link {Page = null});
                    }

                    for (int linkPage = pages - values.Ends + 1; linkPage <= pages; linkPage++)
                    {
                        AddLinkToModel(model, path, linkPage, 
                            values.Index, values.Size, 
                            values.OrderBy, values.Descending);
                    }
                }
            }

            HttpContext.Items[nameof(PagerViewComponent)] = model;
        }

        return View(model);
    }

    private static void AddLinkToModel(PagerModel model, PathString path, int linkPage, int currentPage,
        int pageSize, string? orderBy, bool descending)
    {
        var link = GetLink(path, currentPage, pageSize, linkPage, orderBy, descending);
        model.Links.Add(link);
        if (linkPage == currentPage - 1) model.Previous = link;
        if (linkPage == currentPage + 1) model.Next = link;
    }


    private static Link GetLink(PathString path, int currentPage, int pageSize, int linkPage, string? orderBy, bool descending)
    {
        var qs = new QueryString();
        if (linkPage > 1)
        {
            qs = qs.Add("page", linkPage.ToString());
        }

        if (pageSize != DefaultPageSize)
        {
            qs = qs.Add("pageSize", pageSize.ToString());
        }

        if (orderBy.HasText())
        {
            if (descending)
            {
                qs = qs.Add("orderByDescending", orderBy);
            }
            else
            {
                qs = qs.Add("orderBy", orderBy);
            }
        }

        var link = new Link
        {
            Current = linkPage == currentPage,
            Href = path.Add(qs),
            Page = linkPage
        };
        return link;
    }
}

public class PagerModel
{
    public int TotalPages { get; set; }
    public List<Link> Links { get; set; }
    public Link? Previous { get; set; }
    public Link? Next { get; set; }
}

public record Link
{
    public int? Page { get; set; }
    public string Href { get; set; }
    public bool Current { get; set; }
}


public class PagerValues
{
    public PagerValues(){}
    
    public PagerValues(
        int total, int index, int size,
        string? orderBy = null, bool descending = false,
        int window = PagerViewComponent.DefaultWindow, int ends = PagerViewComponent.DefaultEnds)
    {
        Total = total;
        Index = index;
        Size = size;
        OrderBy = orderBy;
        Descending = descending;
        Window = window;
        Ends = ends;
    }
    
    public int Total { get; set; }
    public int Index { get; set; }
    public int Size { get; set; }
    public string? OrderBy { get; set; }
    public bool Descending { get; set; }
    public int Window { get; set; }
    public int Ends { get; set; }
}