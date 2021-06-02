using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Portal.Features.Spaces
{
    public class DropzoneController : Controller
    {
        private readonly ClaimsPrincipal currentUser;

        public DropzoneController(ClaimsPrincipal currentUser)
        {
            this.currentUser = currentUser;
        }
        
        [HttpPost]
        [Route("[controller]/{customer}/{space}/[action]")]
        public async Task<IActionResult> Local(int customer, int space, List<IFormFile> file)
        {
            if (currentUser.GetCustomerId() != customer)
            {
                throw new InvalidOperationException("Customer ID mismatch");
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