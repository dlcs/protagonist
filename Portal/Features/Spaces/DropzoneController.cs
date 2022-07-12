using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using API.Client;
using DLCS.AWS.S3;
using DLCS.Web.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Portal.Features.Images.Requests;
using Portal.Settings;

namespace Portal.Features.Spaces
{
    public class DropzoneController : Controller
    {
        private readonly ClaimsPrincipal currentUser;
        private readonly IMediator mediator;
        private readonly IDlcsClient dlcsClient;
        private readonly PortalSettings portalSettings;

        public DropzoneController(
            ClaimsPrincipal currentUser, 
            IMediator mediator,
            IDlcsClient dlcsClient,
            IOptions<PortalSettings> portalSettings)
        {
            this.currentUser = currentUser;
            this.mediator = mediator;
            this.dlcsClient = dlcsClient;
            this.portalSettings = portalSettings.Value;
        }
        
        [HttpPost]
        [Route("[controller]/{customer}/{space}/[action]")]
        public async Task<IActionResult> Local(int customer, int space, List<IFormFile> file)
        {
            if (!portalSettings.PermitLocalDropZone)
            {
                return Forbid();
            }
            if (currentUser.GetCustomerId() != customer)
            {
                return BadRequest("Customer ID mismatch");
            }
            string? errorMessage = null; 
            string fileNameForSaving = "";
            try
            {
                foreach (var formFile in file)
                {
                    if (formFile.Length > 0)
                    {
                        //Save file content goes here
                        fileNameForSaving = Path.GetFileName(formFile.FileName) ?? formFile.FileName;
                        fileNameForSaving = fileNameForSaving.Replace(' ', '_'); 
                        
                        var target = GetTargetFilePath(customer, space, fileNameForSaving);
                        await using var stream = System.IO.File.Create(target);
                        await formFile.CopyToAsync(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            if (errorMessage == null)
            {
                return Json(new { message = "completed: " + fileNameForSaving });
            }
            return Json(new { error = errorMessage, message = "error: " + fileNameForSaving + ", " + errorMessage });
        }
        
        [HttpPost]
        [Route("[controller]/{customer}/{space}/[action]")]
        public async Task<IActionResult> Upload(int customer, int space, List<IFormFile> file)
        {
            if (currentUser.GetCustomerId() != customer)
            {
                return BadRequest("Customer ID mismatch");
            }
            
            // Give the images numbering metadata, starting at the current number of images.
            // This is not the most elegant way of doing this.
            var firstPageOfImages = await dlcsClient.GetSpaceImages(1,1, space);
            var currentIndex = firstPageOfImages.TotalItems;

            string? errorMessage = null; 
            string fileNameForSaving = "";
            try
            {
                foreach (var formFile in file)
                {
                    if (formFile.Length > 0)
                    {
                        await using var ms = new MemoryStream();
                        fileNameForSaving = Path.GetFileName(formFile.FileName);
                        await formFile.CopyToAsync(ms);
                        
                        var ingestRequest = new IngestSingleImage(space, 
                            fileNameForSaving, ms, formFile.ContentType, ++currentIndex);
                        await mediator.Send(ingestRequest);
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            if (errorMessage == null)
            {
                return Json(new { message = "completed: " + fileNameForSaving });
            }
            return Json(new { error = errorMessage, message = "error: " + fileNameForSaving + ", " + errorMessage });
        }

        private static string GetTargetFilePath(int customer, int space, string fileNameForSaving)
        {
            var targetDir = Path.Combine(Path.GetTempPath(), customer.ToString(), space.ToString());
            Directory.CreateDirectory(targetDir);
            var target = Path.Combine(targetDir, fileNameForSaving);
            return target;
        }

        [HttpPost]
        [Route("[controller]/{customer}/{space}/[action]")]
        public async Task<IActionResult> External(int customer, int space, [FromBody] ExternalImage image)
        {        
            if (currentUser.GetCustomerId() != customer)
            {
                throw new InvalidOperationException("Customer ID mismatch");
            }  
            if (string.IsNullOrWhiteSpace(image.ExternalUrl))
            {
                return Json(new { error = "No external image Url in request" });
            }
            string filename = new Uri(image.ExternalUrl).Segments[^1];
            if (string.IsNullOrWhiteSpace(filename))
            {
                return Json(new { error = "No recognisable file in request" });
            }
            image.HashCode = $"{image.ExternalUrl.GetHashCode():X}";
            string diskFilename = $"{filename}.{image.HashCode}.external.json";
            var target = GetTargetFilePath(customer, space, diskFilename);
            await System.IO.File.WriteAllTextAsync(target, JsonConvert.SerializeObject(image));

            return Json(new { message = "external: " + filename });
            
        }
    }
    
    public class ExternalImage
    {
        public string ExternalUrl { get; set; }
        public string HashCode { get; set; }
    }
}