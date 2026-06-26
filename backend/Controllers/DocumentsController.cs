using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models; // Added for BlobGetUserDelegationKeyOptions
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly BlobServiceClient _blobServiceClient;

        public DocumentsController(BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        }

        /// <summary>
        /// Generates a short-lived, identity-backed upload URL for direct-to-blob frontend uploads.
        /// </summary>
        [HttpGet("{workspaceId}/upload-url")]
        public async Task<IActionResult> GetUploadUrl(string workspaceId)
        {
            // 1. Establish targeted container and a uniquely named destination blob
            var containerClient = _blobServiceClient.GetBlobContainerClient("attachments");
            var blobClient = containerClient.GetBlobClient($"{workspaceId}/{Guid.NewGuid()}.pdf");

            try
            {
                // 2. Wrap lifetime configurations into the explicit options pattern required by the SDK
                var keyOptions = new BlobGetUserDelegationKeyOptions(DateTimeOffset.UtcNow.AddMinutes(15))
                {
                    StartsOn = DateTimeOffset.UtcNow
                };

                // Request the User Delegation Key and extract its underlying .Value payload
                var response = await _blobServiceClient.GetUserDelegationKeyAsync(keyOptions);
                var userDelegationKey = response.Value;

                // 3. Build the token scope configurations
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = containerClient.Name,
                    BlobName = blobClient.Name,
                    Resource = "b", // "b" indicates individual Blob scope assignment
                    ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(15)
                };
                
                // Grant strict write-only permission for security
                sasBuilder.SetPermissions(BlobSasPermissions.Write);

                // 4. Construct the final authenticated query string URI
                var blobUriBuilder = new BlobUriBuilder(blobClient.Uri)
                {
                    Sas = sasBuilder.ToSasQueryParameters(userDelegationKey, _blobServiceClient.AccountName)
                };

                return Ok(new 
                { 
                    uploadUrl = blobUriBuilder.ToUri().ToString(),
                    blobPath = blobClient.Name
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to delegate storage access tokens.", error = ex.Message });
            }
        }
    }
}