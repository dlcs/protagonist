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

public class ImageIngestModel
{
    [Index(0)] public string AssetType { get; set; }
    [Index(1)] public int? Line { get; set; }
    [Index(2)] public int Space { get; set; }
    [Index(3)] public string Id { get; set; }
    [Index(4)] public string Origin { get; set; }
    [Index(5)] public string InitialOrigin { get; set; }
    [Index(6)] public string String1 { get; set; }
    [Index(7)] public string String2 { get; set; }
    [Index(8)] public string String3 { get; set; }
    [Index(9)] public string Tags { get; set; }
    [Index(10)] public string Roles { get; set; }
    [Index(11)] public int MaxUnauthorized { get; set; }
    [Index(12)] public int? Number1 { get; set; }
    [Index(13)] public int? Number2 { get; set; }
    [Index(14)] public int? Number3 { get; set; }
}

public class ParseCsv : IRequest<ParseCsvResult>
{
    public int? SpaceId { get; set; }
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
                var currentLine = csv.GetField(1);
                try
                {
                    var record = csv.GetRecord<ImageIngestModel>();
                    if (record.AssetType.ToLower() != "image") continue;
                    if (distinctRows.ContainsKey(record.Line.Value)) continue;
                    distinctRows.Add(record.Line.Value, new Image
                    {
                        CustomerId = customerId,
                        Space = record.Space,
                        String1 = record.String1,
                        String2 = record.String2,
                        String3 = record.String3,
                        Origin = record.Origin,
                        //Tags = tags,
                        //Roles = roles,
                        MaxUnauthorised = record.MaxUnauthorized,
                        Number1 = record.Number1,
                        Number2 = record.Number2,
                        Number3 = record.Number3,
                        InitialOrigin = record.InitialOrigin,
                        Family = AssetFamily.Image,
                        MediaType = "image/jp2"
                    });
                }
                catch (CsvHelperException csvEx)
                {
                    var context = csvEx.Context;
                    var fieldIndex = context.Reader.CurrentIndex;
                    var readErrorPrefix = $"Line {currentLine}:";
                    
                    switch (csvEx)
                    {
                        case BadDataException badDataEx:
                        {
                            readErrors.Add($"{readErrorPrefix} field {context.Reader.HeaderRecord?[fieldIndex]} contains bad data");
                            break;
                        }
                        case MissingFieldException:
                        {
                            if (context.Parser.Row != 1)
                            {
                                readErrors.Add($"{readErrorPrefix} field {fieldIndex} is missing");
                            }
                            break;
                        }
                        case TypeConverterException typeEx:
                        {
                            readErrors.Add($"{readErrorPrefix} could not parse {context.Reader.HeaderRecord?[fieldIndex]} value '{typeEx.Text}'");
                            break;
                        }
                        case FieldValidationException validationEx:
                        {
                            readErrors.Add($"{readErrorPrefix} {validationEx.Message}'");
                            break;
                        }
                    }
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
            var currentLine = maxBatchSize * i;
            var lineRange = currentLine + (batches[i].Length - 1);
            var collection = new HydraCollection<Image>()
            {
                Members = batches[i].ToArray()
            };
            try
            {
                var response = await dlcsClient.CreateBatch(collection);
            }
            
            catch (DlcsException dlcsEx) // Forward errors from the API
            {
                return ParseCsvResult.Failure($"DLCS Error: {dlcsEx.Message}");
            }
            catch
            {
                return ParseCsvResult.Failure($"DLCS Error: An error occurred while posting this batch to the DLCS");
            }
        }
        
        return ParseCsvResult.Success();
    }
}


