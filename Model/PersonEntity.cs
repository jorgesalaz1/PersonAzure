using Azure;
using Azure.Data.Tables;
namespace Model
{
    public class PersonEntity : ITableEntity 
    {
        public string PartitionKey { get; set; } = "Personas";
        public string RowKey { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string Email { get; set; } = null!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
