using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.HydraModel;
using DLCS.HydraModel.Settings;
using DLCS.Mock.ApiApp;
using Hydra.Collections;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DLCS.Mock.Controllers
{
    [ApiController]
    public class SpaceImagesController : ControllerBase
    { 
        private readonly MockModel model;
        private readonly HydraSettings settings;
        
        public SpaceImagesController(
            IOptions<HydraSettings> options, 
            MockModel model)
        {
            this.model = model;
            settings = options.Value;
        }
        
        [HttpGet]
        [Route("/customers/{customerId}/spaces/{spaceId}/images/{id}")]
        public IActionResult Image(int customerId, int spaceId, string id = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                var images = model.Images.Where(im => im.CustomerId == customerId && im.Space == spaceId).ToList();
                var string1 = Request.Query["string1"];
                if (!string.IsNullOrWhiteSpace(string1))
                {
                    images = images.Where(im => im.String1 == string1).ToList();
                }
                AutoAdvance(images);
                var hc = new Collection<Image> {Members = images.ToArray()};
                //var hc = new Collection<JObject>();
                //hc.Members = images.Select(im => im.GetCollectionForm()).ToArray();
                hc.TotalItems = hc.Members.Length;
                hc.Id = Request.GetDisplayUrl();
                return Ok(hc);
            }
            else
            {
                var image = model.Images.SingleOrDefault(
                    im => im.CustomerId == customerId
                    && im.Space == spaceId
                    && im.ModelId == id);
                return Ok(image);
            }
        }

        private void AutoAdvance(List<Image> images)
        {
            var now = DateTime.Now;
            var tenSeconds = new TimeSpan(0, 0, 10);
            foreach (var image in images)
            {
                if (!image.Queued.HasValue)
                {
                    if (now - image.Created > tenSeconds)
                    {
                        image.Queued = now;
                    }
                }
                else if (!image.Dequeued.HasValue)
                {
                    if (now - image.Queued > tenSeconds)
                    {
                        image.Dequeued = now;
                    }
                }
                else if (!image.Finished.HasValue)
                {
                    if (now - image.Dequeued > tenSeconds)
                    {
                        image.Finished = now;
                    }
                }
            }
        }

        //[HttpGet]
        //// GET: SpaceImages
        //public Collection<Image> Image(int customerId, int spaceId, string string1 = null)
        //{
        //    var images = GetModel().Images.Where(im => im.CustomerId == customerId && im.Space == spaceId);
        //    if (string1 != null)
        //    {
        //        images = images.Where(im => im.String1 == string1);
        //    }
        //    var hc = new Collection<Image>();
        //    hc.Members = images.ToArray();
        //    hc.TotalItems = hc.Members.Length;
        //    hc.Id = Request.RequestUri.ToString();
        //    return hc;
        //}

        [HttpPut]
        [Route("/customers/{customerId}/spaces/{spaceId}/images/{id}")]
        public Image Image(int customerId, int spaceId, string id, [FromBody]Image incomingImage)
        {
            var newImage = new Image(settings, customerId, spaceId, incomingImage.ModelId,
                    DateTime.Now, incomingImage.Origin, incomingImage.InitialOrigin,
                    0, 0, incomingImage.MaxUnauthorised, null, null, null, true, null,
                    incomingImage.Tags, incomingImage.String1, incomingImage.String2, incomingImage.String3,
                    incomingImage.Number1, incomingImage.Number2, incomingImage.Number3,
                    model.ImageOptimisationPolicies.First().Id,
                    model.ThumbnailPolicies.First().Id);
            model.Images.Add(newImage);
            return newImage;
        }
    }
}