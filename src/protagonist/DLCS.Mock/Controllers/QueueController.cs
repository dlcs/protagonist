using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.HydraModel;
using DLCS.Mock.ApiApp;
using Hydra.Collections;
using Microsoft.AspNetCore.Mvc;

namespace DLCS.Mock.Controllers
{
    [ApiController]
    public class QueueController : ControllerBase
    {
        private readonly MockModel model;
        
        public QueueController(
            MockModel model)
        {
            this.model = model;
        }
        
        [HttpGet]
        [Route("/customers/{customerId}/queue")]
        public IActionResult Queue(int customerId)
        {
            var queue = model.Queues.SingleOrDefault(q => q.CustomerId == customerId);
            if (queue == null)
            {
                return NotFound();
            }
            return Ok(queue);
        }

        [HttpPost]
        [Route("/customers/{customerId}/queue")]
        public Batch Index(int customerId, [FromBody] HydraCollection<Image> images)
        {
            List<Image> initialisedImages = new List<Image>();

            var newBatchId = model.Batches.Select(b => b.ModelId).Max() + 1;
            var batch = new Batch(model.BaseUrl, newBatchId, customerId, DateTime.UtcNow);
            model.Batches.Add(batch);
            foreach (var incomingImage in images.Members)
            {
                var newImage = MockHelp.MakeImage(model.BaseUrl, customerId, incomingImage.Space, incomingImage.ModelId, 
                    DateTime.UtcNow, incomingImage.Origin, incomingImage.InitialOrigin,
                    0, 0, incomingImage.MaxUnauthorised, null, null, null, true, null, 
                    incomingImage.Tags, incomingImage.String1, incomingImage.String2, incomingImage.String3,
                    incomingImage.Number1, incomingImage.Number2, incomingImage.Number3,
                    model.ImageOptimisationPolicies.First().Id,
                    model.ThumbnailPolicies.First().Id);
                initialisedImages.Add(newImage);
            }
            model.Images.AddRange(initialisedImages);
            model.BatchImages.Add(batch.Id, initialisedImages.Select(im => im.Id).ToList());
            return batch;
        }
    }
}