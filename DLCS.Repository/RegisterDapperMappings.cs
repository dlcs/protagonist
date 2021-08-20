using System.Data;
using Dapper;
using DLCS.Core.Enum;
using DLCS.Model.Customer;

namespace DLCS.Repository
{
    public static class DapperMappings
    {
        /// <summary>
        /// Register all customer TypeHandlers for Dapper 
        /// </summary>
        public static void Register()
        {
            SqlMapper.AddTypeHandler(new OriginStrategyHandler());
        }
    }
    
    public class OriginStrategyHandler : SqlMapper.TypeHandler<OriginStrategyType>
    {
        public override OriginStrategyType Parse(object value)
        {
            string? s = value.ToString();
            return string.IsNullOrEmpty(s) ? OriginStrategyType.Default : s.GetEnumFromString<OriginStrategyType>();
        }

        public override void SetValue(IDbDataParameter parameter, OriginStrategyType value)
        {
            parameter.Value = value.GetDescription();
        }
    }
}