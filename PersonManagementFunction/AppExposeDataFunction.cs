using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Model;

namespace PersonManagementFunction
{
    public class AppExposeDataFunction
    {
        private readonly ILogger<AppExposeDataFunction> _logger;
        public AppExposeDataFunction(ILogger<AppExposeDataFunction> logger)
        {
            _logger = logger;
        }

        [Function("getPersonByIdentifier")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "personas/{cedula}")] HttpAuthorization req,
                                              String cedula)
        {
            _logger.LogInformation("[API] Consultando persona por cédula: {Cedula}", cedula);
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")!;
            TableClient tableClient = new TableClient(connectionString, "Personas");
            try
            {
                Response<PersonEntity> response = await tableClient.GetEntityAsync<PersonEntity>("Personas", cedula);
                _logger.LogInformation("[API] Persona encontrada para la cédula: {Cedula}", cedula);
                var resultado = new
                {
                    cedula = response.Value.RowKey,
                    nombre = response.Value.Nombre,
                    email = response.Value.Email
                };
                return new OkObjectResult(resultado);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning("[API] Cédula no encontrada: {Cedula}", cedula);
                return new NotFoundObjectResult(new { message = $"No se encontró un registro para la cédula {cedula}" });
            }
        }
    }
}
