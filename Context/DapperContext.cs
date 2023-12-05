using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace H2HAPICore.Context
{
    public class DapperContext
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionStringEform;
        private readonly string _connectionStringH2H;
        private readonly string _connectionStringBO;

        public DapperContext(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionStringEform = _configuration.GetConnectionString("EformConnection");
            _connectionStringH2H = _configuration.GetConnectionString("H2HConnection");
            _connectionStringBO = _configuration.GetConnectionString("S21PlusConnection");
        }

        public IDbConnection CreateConnectionEform()
            => new SqlConnection(_connectionStringEform);

        public IDbConnection CreateConnectionH2H()
            => new SqlConnection(_connectionStringH2H);

        public IDbConnection CreateConnectionBO()
            => new SqlConnection(_connectionStringBO);
    }
}
