using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;
using DLCS.HydraModel;
using DLCS.Web.Auth;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Settings;
using MissingFieldException = CsvHelper.MissingFieldException;

namespace Portal.Features.Batches.Requests;

public class IngestFromCsv : IRequest<IngestFromCsvResult>
{
    public int? SpaceId { get; set; }
    public IFormFile File { get; set; }
}

public class IngestFromCsvResult
{
    [JsonIgnore] public bool IsSuccess { get; set; }

    public string[]? Errors { get; set; }

    public static IngestFromCsvResult Failure(string[] errors)
    {
        return new IngestFromCsvResult { IsSuccess = false, Errors = errors };
    }
    
    public static IngestFromCsvResult Failure(string error)
    {
        return new IngestFromCsvResult { IsSuccess = false, Errors = new []{ error } };
    }
    
    public static IngestFromCsvResult Success()
    {
        return new IngestFromCsvResult { IsSuccess = true };
    }
}

public class IngestFromCsvHandler : IRequestHandler<IngestFromCsv, IngestFromCsvResult>
{
    private readonly IDlcsClient dlcsClient;
    private readonly ILogger<IngestFromCsvHandler> logger;
    private readonly int customerId;
    private readonly int maxBatchSize;
    
    public IngestFromCsvHandler(IDlcsClient dlcsClient, IOptions<PortalSettings> portalSettings, ILogger<IngestFromCsvHandler> logger, ClaimsPrincipal currentUser)
    {
        this.dlcsClient = dlcsClient;
        this.logger = logger;
        customerId = (currentUser.GetCustomerId() ?? -1);
        maxBatchSize = portalSettings.Value.MaxBatchSize;
    }

    public async Task<IngestFromCsvResult> Handle(IngestFromCsv request, CancellationToken cancellationToken)
    {
        var (distinctRows, readErrors) = await ParseCsv(request.SpaceId, request.File);

        if (readErrors.Any())
        {
            return IngestFromCsvResult.Failure(readErrors.ToArray());   
        }

        var ingestFromCsvResult = await UploadImagesToDlcs(distinctRows);
        return ingestFromCsvResult;
    } 
    
    private async Task<(Dictionary<int, Image> distinctRows, List<string> readErrors)> ParseCsv(int? spaceId, IFormFile csvFile)
    {
        var distinctRows = new Dictionary<int, Image>();
        var readErrors = new List<string>();
        
        using (var stream = new StreamReader(csvFile.OpenReadStream()))
        using (var csv = new CsvReader(stream, CultureInfo.InvariantCulture))
        {
            await csv.ReadAsync();
            csv.ReadHeader();
            while (await csv.ReadAsync())
            {
                try
                {
                    var record = csv.GetRecord<ImageIngestModel>();
                    if (record.AssetType.ToLower() != "image") continue;
                    if (distinctRows.ContainsKey(record.Line.Value)) continue;
                    
                    if (spaceId.HasValue && spaceId != record.Space)
                    {
                        readErrors.Add(
                            $"Line {record.Line}: Space value does not match current (Expected {spaceId}, got {record.Space})");
                        continue;
                    }
                    
                    distinctRows.Add(record.Line.Value, new Image
                    {
                        CustomerId = customerId,
                        ModelId = record.Id,
                        Space = record.Space,
                        String1 = record.String1,
                        String2 = record.String2,
                        String3 = record.String3,
                        Origin = record.Origin,
                        Tags = record.Tags.Split(",",
                            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                        Roles = record.Roles.Split(",",
                            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                        MaxUnauthorised = record.MaxUnauthorized ?? -1,
                        Number1 = record.Number1,
                        Number2 = record.Number2,
                        Number3 = record.Number3,
                        InitialOrigin = record.InitialOrigin,
                        Family = AssetFamily.Image,
                        MediaType = "image/jp2"
                    });
                }
                catch (BadDataException badDataEx)
                {
                    readErrors.Add(
                        $"CSV read error: bad data found in file at row {badDataEx.Context.Parser.Row}, column {badDataEx.Context.Reader.CurrentIndex}");
                    break;
                }
                catch (MissingFieldException missingFieldEx)
                {
                    readErrors.Add(
                        $"CSV read error: row {missingFieldEx.Context.Parser.Row} in file is missing columns");
                    break;
                }
                catch (TypeConverterException typeEx)
                {
                    var fieldIndex = typeEx.Context.Reader.CurrentIndex;
                    var currentLine = csv.GetField(1);
                    readErrors.Add($"Line {currentLine}: could not parse {ImageIngestModel.FieldNames[fieldIndex]} value ({typeEx.Text})");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error parsing CSV file");
                    readErrors.Add("An error occured while parsing the CSV file");
                }
            }
        }
        
        return (distinctRows, readErrors);
    }
    
    private async Task<IngestFromCsvResult> UploadImagesToDlcs(Dictionary<int, Image> distinctRows)
    {
        var images = distinctRows
            .OrderBy(image => image.Key)
            .Select(image => image.Value)
            .ToList();


        var batchIndex = 0;
        foreach (var batch in images.Chunk(maxBatchSize))
        {
            var collection = new HydraCollection<Image>()
            {
                Members = batch
            };
            try
            {
                await dlcsClient.CreateBatch(collection);
            }
            catch (DlcsException dlcsEx)
            {
                var imagesStart = maxBatchSize * batchIndex + 1;
                var imagesEnd = maxBatchSize * batchIndex + batch.Length;
                return IngestFromCsvResult.Failure($"DLCS Error in rows {imagesStart}-{imagesEnd}: {dlcsEx.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error posting batch to DLCS");
                return IngestFromCsvResult.Failure(
                    $"DLCS Error: An error occurred while posting this batch to the DLCS");
            }
            
            batchIndex++;
        }
        
        return IngestFromCsvResult.Success();
    }
}

public class ImageIngestModel
{
    public static readonly string[] FieldNames =
    {
        "Type", "Line", "Space", "ID", "Origin", "InitialOrigin", "Reference1", "Reference2", "Reference3", "Tags",
        "Roles",
        "MaxUnauthorised", "NumberReference1", "NumberReference2", "NumberReference3"
    };

    [Index(0)] public string AssetType { get; set; }
    [Index(1)] public int? Line { get; set; }
    [Index(2)] public int Space { get; set; }
    [Index(3)] public string Id { get; set; }
    [Index(4)] public string Origin { get; set; }
    [Index(5), NullValues("")] public string? InitialOrigin { get; set; }
    [Index(6)] public string String1 { get; set; }
    [Index(7)] public string String2 { get; set; }
    [Index(8)] public string String3 { get; set; }
    [Index(9)] public string Tags { get; set; }
    [Index(10)] public string Roles { get; set; }
    [Index(11)] public int? MaxUnauthorized { get; set; }
    [Index(12)] public int? Number1 { get; set; }
    [Index(13)] public int? Number2 { get; set; }
    [Index(14)] public int? Number3 { get; set; }
}
