using System;
using System.Collections.Generic;
using System.Linq;
using Hydra;
using Hydra.Model;

namespace DLCS.HydraModel;

public static class CommonOperations
{
    // TODO - in general, add statusCode hints everywhere


    public static Operation[] GetStandardResourceOperations(
        string idPrefix,
        string displayNameOfType,
        string vocabNameofType,
        params string[] methods)
    {
        var ops = new List<Operation>();
        foreach (var method in methods)
        {
            switch (method)
            {
                case "GET":
                    ops.Add(new Operation
                    {
                        Id = idPrefix + "_retrieve",
                        Method = method,
                        Label = "Retrieve a " + displayNameOfType,
                        Returns = vocabNameofType,
                        StatusCodes = GetStandardGetResourceStatusCodes(displayNameOfType)
                    });
                    break;

                case "PUT":
                    ops.Add(new Operation
                    {
                        Id = idPrefix + "_upsert",
                        Method = method,
                        Label = "create or replace a " + displayNameOfType,
                        Expects = vocabNameofType,
                        Returns = vocabNameofType,
                        StatusCodes = GetStandardPutResourceStatusCodes(displayNameOfType)
                    });
                    break;

                case "PATCH":
                    ops.Add(new Operation
                    {
                        Id = idPrefix + "_update",
                        Method = method,
                        Label = "Update the supplied fields of the " + displayNameOfType,
                        Expects = vocabNameofType,
                        Returns = vocabNameofType,
                        StatusCodes = GetStandardPatchResourceStatusCodes(displayNameOfType)
                    });
                    break;

                case "DELETE":
                    ops.Add(new Operation
                    {
                        Id = idPrefix + "_delete",
                        Method = method,
                        Label = "Delete the " + displayNameOfType,
                        Expects = null,
                        Returns = Names.Owl.Nothing,
                        StatusCodes = GetStandardDeleteResourceStatusCodes(displayNameOfType)
                    });
                    break;

                default:
                    throw new ArgumentException("Unknown HTTP method " + method);

            }
        }
        return ops.ToArray();

    }


    public static Operation[] GetStandardCollectionOperations(
        string idPrefix, 
        string displayNameOfCollectionType,
        string vocabNameofCollectionType,
        string description = null)
    {
        return new[]
        {
            StandardCollectionGet(idPrefix + "_collection_retrieve", "Retrieves all " + displayNameOfCollectionType, description),
            StandardCollectionPost(idPrefix + "_create", "Creates a new " + displayNameOfCollectionType, 
                null, vocabNameofCollectionType, displayNameOfCollectionType)
        };

    }
    public static Operation StandardCollectionGet(string id, string label, string description)
    {
        return new Operation
        {
            Id = id,
            Method = "GET",
            Label = label,
            Description = description,
            Returns = Names.Hydra.Collection,
            StatusCodes = GetStandardGetCollectionStatusCodes()
        };
    }


    public static Operation StandardCollectionPost(string id, string label, string description, 
        string expectsAndReturns, string displayNameOfCollectionType)
    {
        return new Operation
        {
            Id = id,
            Method = "POST",
            Label = label,
            Description = description,
            Expects = expectsAndReturns,
            Returns = expectsAndReturns,
            StatusCodes = GetStandardPostToCollectionStatusCodes(displayNameOfCollectionType)
        };
    }

    private static Status[] GetStandardGetCollectionStatusCodes()
    {
        return new[]
        {
            new Status
            {
                StatusCode = 200,
                Description = "OK"
            }
        };
    }

    public static Status[] GetStandardPostToCollectionStatusCodes(string resourceName)
    {
        return new[]
        {
            new Status
            {
                StatusCode = 201,
                Description = resourceName + " created."
            },
            new Status
            {
                StatusCode = 400,
                Description = "Bad Request"
            }
        };
    }

    public static Status[] GetStandardPatchResourceStatusCodes(string resourceName)
    {
        return new[]
        {
            new Status
            {
                StatusCode = 205,
                Description = "Accepted " + resourceName + ", reset view"
            },
            new Status
            {
                StatusCode = 400,
                Description = "Bad request"
            },
            new Status
            {
                StatusCode = 404,
                Description = "Not found"
            }
        };
    }


    private static Status[] GetStandardDeleteResourceStatusCodes(string displayNameOfCollectionType)
    {
        return new[]
        {
            new Status
            {
                StatusCode = 205,
                Description = "Accepted " + displayNameOfCollectionType + ", reset view"
            },
            new Status
            {
                StatusCode = 404,
                Description = "Not found"
            }
        };
    }
    private static Status[] GetStandardGetResourceStatusCodes(string displayNameOfCollectionType)
    {
        return new[]
        {
            new Status
            {
                StatusCode = 200,
                Description = "OK"
            },
            new Status
            {
                StatusCode = 404,
                Description = "Not found"
            }
        };
    }
    private static Status[] GetStandardPutResourceStatusCodes(string displayNameOfCollectionType)
    {
        return new[]
        {
            new Status
            {
                StatusCode = 200,
                Description = "OK"
            },
            new Status
            {
                StatusCode = 201,
                Description = "Created " + displayNameOfCollectionType
            },
            new Status
            {
                StatusCode = 404,
                Description = "Not found"
            }
        };
    }

    public static Operation WithMethod(this Operation[] operations, string method)
    {
        return operations.FirstOrDefault(op => op.Method == method);
    } 
}
