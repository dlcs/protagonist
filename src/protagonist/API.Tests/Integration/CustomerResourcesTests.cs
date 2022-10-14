using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using API.Tests.Integration.Infrastructure;
using DLCS.Repository;
using Test.Helpers.Integration;
using Test.Helpers.Integration.Infrastructure;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class CustomerResourcesTests : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsContext dbContext;
    private readonly HttpClient httpClient;
    private readonly IAmazonS3 amazonS3;

    public CustomerResourcesTests(StorageFixture storageFixture, ProtagonistAppFactory<Startup> factory)
    {
        var dbFixture = storageFixture.DbFixture;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        
        dbContext = dbFixture.DbContext;

        httpClient = factory.ConfigureBasicAuthedIntegrationTestHttpClient(dbFixture, "API-Test",
            f => f.WithLocalStack(storageFixture.LocalStackFixture));

        dbFixture.CleanUp();
        
        dbContext.NamedQueries.Add(new DLCS.Model.Assets.NamedQueries.NamedQuery
        {
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "cust-resource",
            Template = "canvas=n2&s1=p1&space=p2&n1=p3&coverpage=https://coverpage.pdf&objectname=tester"
        });
        dbContext.SaveChanges();
    }

    [Fact]
    public async Task Delete_PDF_Returns400_IfUnableToFind()
    {
        // Arrange
        var path = "/customers/99/resources/pdf/unknown";
        
        // Act
        var response = await httpClient.AsCustomer().DeleteAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Delete_PDF_Returns400_IfArgsIncorrect()
    {
        // Arrange
        var path = "/customers/99/resources/pdf/cust-resource?args=too-little";
        
        // Act
        var response = await httpClient.AsCustomer().DeleteAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task Delete_PDF_Returns200_IfArgsCorrect_NoFilesExist()
    {
        // Arrange
        var path = "/customers/99/resources/pdf/cust-resource?args=foo/10/100";
        
        // Act
        var response = await httpClient.AsCustomer().DeleteAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }
    
    [Fact]
    public async Task Delete_PDF_Returns200_AndDeletesFiles_IfArgsCorrect_FilesExist()
    {
        // Arrange
        const string controlFileKey = "99/pdf/cust-resource/foo/10/100/tester.json";
        const string projectionKey = "99/pdf/cust-resource/foo/10/100/tester";
        
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = projectionKey,
            BucketName = LocalStackFixture.OutputBucketName,
            ContentBody = "this-is-the-projection"
        });
        await amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = controlFileKey,
            BucketName = LocalStackFixture.OutputBucketName,
            ContentBody = "this-is-the-control-file"
        });
        
        var path = "/customers/99/resources/pdf/cust-resource?args=foo/10/100";
        
        // Act
        var response = await httpClient.AsCustomer().DeleteAsync(path);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        jsonDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var keys = await amazonS3.ListObjectsAsync(LocalStackFixture.OutputBucketName,
            "99/pdf/cust-resource/foo/10/100");
        keys.S3Objects.Count.Should().Be(0);
    }
}