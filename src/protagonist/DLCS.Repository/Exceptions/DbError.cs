using System;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Exceptions;

/// <summary>
/// Provides additional Database specific information about 
/// a <see cref="DbUpdateException"/> thrown by EF Core.
/// </summary>
/// <param name="TableName">The table involved, if any.</param>
/// <param name="ConstraintName">The constraint involved, if any.</param>
/// <param name="Exception">The unwrapped database provider specific exception.</param>
/// <remarks>See https://haacked.com/archive/2022/12/12/specific-db-exception for more information</remarks>
public record DbError(string? TableName, string? ConstraintName, Exception Exception);