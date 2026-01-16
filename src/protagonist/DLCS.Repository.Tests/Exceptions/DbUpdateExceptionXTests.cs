using DLCS.Repository.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DLCS.Repository.Tests.Exceptions;

public class DbUpdateExceptionXTests
{
    [Fact]
    public void GetDatabaseError_Null_IfNoInnerException() 
        => new DbUpdateException("Sample").GetDatabaseError().Should().BeNull();

    [Fact]
    public void GetDatabaseError_UniqueViolation_ReturnsExpected()
    {
        const string constraintName = "IX_Images_Foo_Bar";
        var pgEx = new PostgresException("", "", "", PostgresErrorCodes.UniqueViolation, tableName: "Images",
            constraintName: constraintName);
        var exception = new DbUpdateException("Sample", pgEx);

        var actual = exception.GetDatabaseError();

        actual.Should().BeOfType<UniqueConstraintError>();
        var ixError = actual as UniqueConstraintError;
        ixError!.ConstraintName.Should().Be(constraintName);
        ixError.TableName.Should().Be("Images");
        ixError.ColumnNames.Should().BeEquivalentTo("Foo", "Bar");
        ixError.Exception.Should().Be(pgEx);
    }
    
    [Fact]
    public void GetDatabaseError_ForeignKeyViolation_ReturnsExpected()
    {
        const string constraintName = "FK_Images_OtherTable_Foo_Bar";
        var pgEx = new PostgresException("", "", "", PostgresErrorCodes.ForeignKeyViolation, tableName: "Images",
            constraintName: constraintName);
        var exception = new DbUpdateException("Sample", pgEx);

        var actual = exception.GetDatabaseError();

        actual.Should().BeOfType<DbForeignKeyConstraintError>();
        var fkError = actual as DbForeignKeyConstraintError;
        fkError!.ConstraintName.Should().Be(constraintName);
        fkError.TableName.Should().Be("Images");
        fkError.SecondaryTableName.Should().Be("OtherTable");
        fkError.Exception.Should().Be(pgEx);
    }
    
    [Fact]
    public void GetDatabaseError_ReturnsDbError_ForOtherErrorCodes()
    {
        var pgEx = new PostgresException("", "", "", PostgresErrorCodes.InsufficientPrivilege);
        var exception = new DbUpdateException("Sample", pgEx);

        var actual = exception.GetDatabaseError();
        actual!.ConstraintName.Should().BeNull();
        actual.TableName.Should().BeNull();
        actual.Exception.Should().Be(pgEx);
    }
}
