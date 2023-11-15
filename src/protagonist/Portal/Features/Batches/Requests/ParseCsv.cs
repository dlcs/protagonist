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
using DLCS.HydraModel;
using Hydra.Collections;
using MediatR;
using Microsoft.AspNetCore.Http;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;
using DLCS.Web.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Settings;
using MissingFieldException = CsvHelper.MissingFieldException;

namespace Portal.Features.Batches.Requests;

public class ParseCsv : IRequest<ParseCsvResult>
{
    public IFormFile File { get; set; }
}

public class ParseCsvResult
{
    [JsonIgnore] public bool IsSuccess { get; set; }

    public string[]? Errors { get; set; }

    public static ParseCsvResult Failure(string[] errors)
    {
        return new ParseCsvResult { IsSuccess = false, Errors = errors };
    }
    
    public static ParseCsvResult Failure(string error)
    {
        return new ParseCsvResult { IsSuccess = false, Errors = new []{ error } };
    }
    
    public static ParseCsvResult Success()
    {
        return new ParseCsvResult { IsSuccess = true };
    }
}

public class ParseCsvHandler : IRequestHandler<ParseCsv, ParseCsvResult>
{
    private readonly IDlcsClient dlcsClient;
    private readonly ILogger<ParseCsvHandler> logger;
    private readonly int customerId;
    private readonly int maxBatchSize;
    
    public ParseCsvHandler(IDlcsClient dlcsClient, IOptions<PortalSettings> portalSettings, ILogger<ParseCsvHandler> logger, ClaimsPrincipal currentUser)
    {
        this.dlcsClient = dlcsClient;
        this.logger = logger;
        customerId = (currentUser.GetCustomerId() ?? -1);
        maxBatchSize = portalSettings.Value.MaxBatchSize;
    }

    public async Task<ParseCsvResult> Handle(ParseCsv request, CancellationToken cancellationToken)
    {
        var distinctRows = new Dictionary<int, Image>();
        var readErrors = new List<string>();
        
        using (var stream = new StreamReader(request.File.OpenReadStream()))
        using (var csv = new CsvReader(stream, CultureInfo.InvariantCulture))
        {
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                try
                {
                    var record = csv.GetRecord<ImageIngestModel>();
                    if (record.AssetType.ToLower() != "image") continue;
                    if (distinctRows.ContainsKey(record.Line.Value)) continue;
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
                    return ParseCsvResult.Failure(
                        $"CSV read error: bad data found in file at line {badDataEx.Context.Parser.Row}, row {badDataEx.Context.Reader.CurrentIndex}");
                }
                catch (MissingFieldException missingFieldEx)
                {
                    return ParseCsvResult.Failure(
                        $"CSV read error: line {missingFieldEx.Context.Parser.Row} in file is missing fields");
                }
                catch (TypeConverterException typeEx)
                {
                    var fieldIndex = typeEx.Context.Reader.CurrentIndex - 1;
                    var currentLine = csv.GetField(1);
                    readErrors.Add($"Line {currentLine}: could not parse {ImageIngestModel.FieldNames[fieldIndex]} value ({typeEx.Text})");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error parsing CSV file");
                    return ParseCsvResult.Failure("An error occured while parsing the CSV file");
                }
            }
        }
        
        // Return an error if parsing fails
        if (readErrors.Any()) 
        {
            return ParseCsvResult.Failure(readErrors.ToArray());
        }
        
        // Order images
        var images = distinctRows
            .OrderBy(image => image.Key)
            .Select(image => image.Value)
            .ToList();
        
        // Split the read images into multiple batches if they surpass the maximum batch size
        var batches = images.Chunk(maxBatchSize).ToList();
        for (var i = 0; i < batches.Count; i++)
        {
            var collection = new HydraCollection<Image>()
            {
                Members = batches[i].ToArray()
            };
            try
            {
                await dlcsClient.CreateBatch(collection);
            }
            catch (DlcsException dlcsEx) // Forward errors from the API
            {
                // Get the range of images where the exception may have occured:
                var imagesStart = (maxBatchSize * i) + 1;
                var imagesEnd = (maxBatchSize * i) + batches[i].Length;
                return ParseCsvResult.Failure($"DLCS Error in images {imagesStart}-{imagesEnd}: {dlcsEx.Message}");
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Error posting batch to DLCS");
                return ParseCsvResult.Failure($"DLCS Error: An error occurred while posting this batch to the DLCS");
            }
        }
        
        return ParseCsvResult.Success();
    }
}

public class ImageIngestModel
{
    public static readonly string[] FieldNames =
    {
        "Type", "Line", "Space", "ID", "Origin", "Reference1", "Reference2", "Reference3", "Tags", "Roles",
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


