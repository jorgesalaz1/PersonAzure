using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PersonManagementFunction
{
    public class AppGetDataFunction
    {
        private readonly ILogger<AppGetDataFunction> _logger;

        public AppGetDataFunction(ILogger<AppGetDataFunction> logger)
        {
            _logger = logger;
        }

        [Function("FunctionEnvio")]
        public async Task Run([BlobTrigger("entrada/{name}", Connection ="AzureWebJobsStorage", Source = BlobTriggerSource.EventGrid)]
                              Stream stream, String name)
        {
            _logger.LogInformation("[INIT]: Archivo detectado en contenedor ENTRADA: {name}", name);
            var reader = new StreamReader(stream);
            var content =await reader.ReadToEndAsync();
            string[] separadores = { "\r\n", "\n" };
            var lineas = content.Split(separadores, StringSplitOptions.RemoveEmptyEntries);
            String connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")!;
            TableClient tableClient = new TableClient(connectionString, "Personas");
            await tableClient.CreateIfNotExistsAsync();
            foreach(var linea in lineas)
            {
                var parts = linea.Split(',');
                if (parts.Length < 3)
                {
                    _logger.LogWarning("Línea inválida: {Linea}", linea);
                    continue;
                }
                string cedula = parts[0].Trim();
                string nombre = parts[1].Trim();
                string email = parts[2].Trim();
                var persona = new PersonEntity
                {
                    PartitionKey = "Personas",
                    RowKey = cedula,
                    Nombre = nombre,
                    Email = email
                };
                await tableClient.UpsertEntityAsync(persona);
                _logger.LogInformation("Persona guardada correctamente.");
            }
            try
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                BlobContainerClient entradaContainer = blobServiceClient.GetBlobContainerClient("entrada");
                BlobContainerClient procesadosContainer = blobServiceClient.GetBlobContainerClient("procesados");
                await entradaContainer.CreateIfNotExistsAsync();
                await procesadosContainer.CreateIfNotExistsAsync();

                BlobClient sourceBlob = entradaContainer.GetBlobClient(name);
                BlobClient destBlob = procesadosContainer.GetBlobClient(name);
                await destBlob.StartCopyFromUriAsync(sourceBlob.Uri);
                await sourceBlob.DeleteIfExistsAsync();
                _logger.LogInformation("[STORAGE] Archivo {Name} movido a PROCESADOS.", name);
            }
            catch (RequestFailedException ex) when (ex.Status == 403)
            {
                _logger.LogError(ex, "[ERROR DE PERMISOS] Las credenciales de AzureWebJobsStorage no tienen acceso suficiente.");
            }
            
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogError(ex, "[ERROR DE ORIGEN] El archivo {Name} ya no existe en el contenedor 'entrada'.", name);
            }
            
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "[ERROR AZURE] Error del servicio Storage. Código: {ErrorCode}", ex.ErrorCode);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "[CONFIG ERROR] {Message}", ex.Message);
            }
                        catch (Exception ex)
            {
                _logger.LogError(ex, "[ERROR CRÍTICO NO CONTROLADO] No se pudo mover el archivo {Name}", name);
            }
        }


        [Function("UploadRealFile")]
        public async Task<IActionResult> Upload([HttpTrigger(AuthorizationLevel.Function, 
                                             "post", Route = "upload-file")] HttpRequest req)
        {
            if (!req.HasFormContentType)
            {
                return new BadRequestObjectResult(new { message = "La petición debe ser de tipo multipart/form-data con un archivo." });
            }

            var form = await req.ReadFormAsync();
            var file = form.Files["file"];

            if (file == null || file.Length == 0)
            {
                return new BadRequestObjectResult(new { message = "No se ha adjuntado ningún archivo o el archivo está vacío." });
            }


            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")!;
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

            
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("entrada");
            await containerClient.CreateIfNotExistsAsync();

           
            string fileName = file.FileName;
            BlobClient blobClient = containerClient.GetBlobClient(fileName);

           
            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, overwrite: true);
            }

            _logger.LogInformation("[UPLOAD] Archivo físico '{FileName}' subido exitosamente a 'entrada'.", fileName);

            return new OkObjectResult(new
            {
                message = $"¡Archivo físico '{fileName}' subido con éxito al contenedor 'entrada'!",
                tamanioBytes = file.Length
            });
        }


    }
}
