using System.CommandLine;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SCPU.Assembler.Exporters;
using SCPU.Assembler.Model;

namespace SCPU.Assembler.CLI
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.Title = "S-CPU Assembler";

            // Configure CLI
            var rootCommand = CommandLineDefinition.BuildRootCommand();
            rootCommand.SetAction(async parseResult =>
            {
                // Get CLI options
                var opts = CommandLineDefinition.Bind(parseResult);

                // Configure Host with logging
                var builder = Host.CreateDefaultBuilder(args)
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                        if (opts.Quiet)
                        {
                            logging.SetMinimumLevel(LogLevel.None);
                        }
                    })
                    .ConfigureServices((context, services) =>
                    {
                        services.AddAssembler();
                    });

                // Build Host
                using var host = builder.Build();

                // Resolve services from DI
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                var assembler = host.Services.GetRequiredService<Assembler>();
                var exporter = host.Services.GetRequiredService<AssemblyExportManager>();

                try
                {
                    // Create the assembly request from CLI options
                    var request = new AssemblyRequest
                    {
                        Source = SourceDocument.FromFile(opts.File),
                        Defines = opts.Defines
                    };

                    // Run the assembler
                    var result = await assembler.AssembleAsync(request);

                    // Convert the assembly result into the chosen output format
                    var payload = exporter.Convert(result, opts.Format);

                    // Option "-o" : export the result to a file
                    if (opts.Output is not null)
                    {
                        await exporter.WriteAsync(payload, opts.Output, opts.Format);
                    }

                    // Option "-u" : send the assembled payload to a remote HTTP endpoint
                    if (opts.PostUrl is not null)
                    {
                        var fileName = opts.Output?.Name ?? (opts.File.Name + ".bin");
                        await PostResult(opts.PostUrl, payload, fileName, logger);
                    }

                    // Option "-p" : print the assembled payload to the console
                    if (opts.Print)
                    {
                        Console.Write(Encoding.UTF8.GetString(payload));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Fatal error: {Message}", ex.Message);
                    Environment.Exit(1);
                }
            });

            // Parse command line & invoke action
            ParseResult parseResult = rootCommand.Parse(args);
            return await parseResult.InvokeAsync();
        }

        /// <summary>
        /// Uploads the assembled payload to the specified HTTP endpoint using a multipart/form-data POST.
        /// </summary>
        /// <param name="postUrl">The target URL where the payload will be uploaded.</param>
        /// <param name="payload">The assembled binary payload as a byte array.</param>
        /// <param name="filename">The filename to associate with the uploaded payload.</param>
        /// <param name="logger">Logger instance used to record success or failure information.</param>
        /// <remarks>
        /// The payload is sent as a <c>multipart/form-data</c> request with 
        /// <c>application/octet-stream</c> as the file content type.  
        /// If the request fails, the exception is caught and logged as an error.
        /// </remarks>
        private static async Task PostResult(Uri postUrl, byte[] payload, string filename, ILogger logger)
        {
            try
            {
                using var client = new HttpClient();
                using var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(payload);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                content.Add(fileContent, "file", $"'{filename}'");

                using var response = await client.PostAsync(postUrl, content);
                response.EnsureSuccessStatusCode();

                logger.LogInformation("Uploaded firmware to {PostUrl} (file: {fileName}).", postUrl, filename);
            }
            catch (Exception ex)
            {
                logger.LogError("Unable to upload firmware to {PostUrl}: {Message}", postUrl, ex.Message);
            }
        }
    }
}
