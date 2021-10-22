using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.SimpleSystemsManagement.Model;
using Hydra.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Portal.ViewComponents
{
    public class PagerViewComponent : ViewComponent
    {
        public const int DefaultPageSize = 100;
        public const int DefaultWindow = 7;
        public const int DefaultEnds = 3;

        public async Task<IViewComponentResult> InvokeAsync(int total, int currentPage = 1,
            int pageSize = DefaultPageSize, int window = DefaultWindow, int ends = DefaultEnds)
        {
            if (total < pageSize)
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
                int pages = total / pageSize;
                if (total % pageSize > 0) pages++;
                // we don't want to loop through the pages - there could be 100,000 of them
                // instead we want the first few, then an ellipsis,
                // then the current "window", then another ellipsis, then the end
                // but only if there are enough pages to justify this.
                // prev | 1 2 3 ... 45 46 [47] 48 49 ... 654 655 656 | next
                if (pages < (2 * ends) + window + 2)
                {
                    for (int linkPage = 1; linkPage <= pages; linkPage++)
                    {
                        AddLinkToModel(model, path, linkPage, currentPage, pageSize);
                    }
                }
                else
                {
                    int windowStart = currentPage - (window / 2);
                    for (int linkPage = 1; linkPage <= ends; linkPage++)
                    {
                        AddLinkToModel(model, path, linkPage, currentPage, pageSize);
                    }

                    if (windowStart > ends + 2)
                    {
                        // we're not into the window yet, add an ellipsis
                        model.Links.Add(new Link {Page = null});
                    }
                    else
                    {
                        // we're into the window already
                        AddLinkToModel(model, path, ends + 1, currentPage, pageSize);
                    }

                    windowStart = Math.Max(ends + 2, windowStart);
                    int tail = pages - ends - window;
                    if (windowStart >= tail)
                    {
                        // just run through to the end
                        for (int linkPage = tail; linkPage <= pages; linkPage++)
                        {
                            AddLinkToModel(model, path, linkPage, currentPage, pageSize);
                        }
                    }
                    else
                    {
                        for (int linkPage = windowStart; linkPage < Math.Min(windowStart + window, pages); linkPage++)
                        {
                            AddLinkToModel(model, path, linkPage, currentPage, pageSize);
                        }

                        if (model.Links.Last().Page < pages - ends)
                        {
                            model.Links.Add(new Link {Page = null});
                        }

                        for (int linkPage = pages - ends + 1; linkPage <= pages; linkPage++)
                        {
                            AddLinkToModel(model, path, linkPage, currentPage, pageSize);
                        }
                    }
                }

                HttpContext.Items[nameof(PagerViewComponent)] = model;
            }

            return View(model);
        }

        private static void AddLinkToModel(PagerModel model, PathString path, int linkPage, int currentPage,
            int pageSize)
        {
            var link = GetLink(path, currentPage, pageSize, linkPage);
            model.Links.Add(link);
            if (linkPage == currentPage - 1) model.Previous = link;
            if (linkPage == currentPage + 1) model.Next = link;
        }


        private static Link GetLink(PathString path, int currentPage, int pageSize, int linkPage)
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
        
        public PagerValues(int total, int index, int size)
        {
            Total = total;
            Index = index;
            Size = size;
        }
        
        public int Total { get; set; }
        public int Index { get; set; }
        public int Size { get; set; }
    }
}