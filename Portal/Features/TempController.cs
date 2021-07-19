using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Settings;
using DLCS.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Portal.Features
{
    // TODO - this is a temporary controller to verify integration tests
    [ApiController]
    [Route("[controller]/[action]")]
    public class TempController: Controller
    {
        private readonly IAmazonS3 amazonS3;
        private readonly DlcsContext context;
        private readonly DlcsSettings dlcsSettings;

        public TempController(IAmazonS3 amazonS3, DlcsContext context, IOptions<DlcsSettings> dlcsSettings)
        {
            this.amazonS3 = amazonS3;
            this.context = context;
            this.dlcsSettings = dlcsSettings.Value;
        }

        [HttpGet]
        public async Task<IActionResult> IndexAsync()
        {
            var listObjects = await amazonS3.ListObjectsAsync(new ListObjectsRequest
                {BucketName = dlcsSettings.OriginBucket});
            
            return new JsonResult(new
            {
                spaces = await context.Spaces.CountAsync(),
                keys = listObjects.S3Objects.Count 
            });
        }
    }
}