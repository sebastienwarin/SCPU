using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SCode.Compiler.Exceptions;
using SCPU.Assembler;
using SCPU.Assembler.Exporters;
using SCPU.Assembler.Model;
using System.CommandLine;
using System.Net.Http.Headers;
using System.Text;

namespace SCode.Compiler.CLI
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.Title = "S-Code Compiler";

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
                        services.AddCompiler();
                    });

                // Build Host
                using var host = builder.Build();

                // Resolve services from DI
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                var compiler = host.Services.GetRequiredService<Compiler>();
                var assembler = host.Services.GetRequiredService<Assembler>();
                var exporter = host.Services.GetRequiredService<AssemblyExportManager>();

                try
                {
                    // Create the compiler request from CLI options
                    var request = new CompileRequest
                    {
                        Source = SourceDocument.FromFile(opts.File)
                    };

                    // Run the compiler
                    var result = await compiler.CompileAsync(request);

                    // Get output data
                    byte[] outputData = [];
                    if (opts.Format == OutputFormat.Assembly)
                    {
                        outputData = Encoding.UTF8.GetBytes(await result.GeneratedAssembly.ReadAllTextAsync());
                    }
                    else // Assemble
                    {
                        // Create the assembly request and assemble
                        var asmRequest = new AssemblyRequest { Source = result.GeneratedAssembly, Defines = opts.Defines };
                        var asmResult = await assembler.AssembleAsync(asmRequest);

                        // Convert the assembly result into the chosen output format
                        outputData = exporter.Convert(asmResult, (SCPU.Assembler.Exporters.OutputFormat)opts.Format);
                    }

                    // Option "-o" : export the output data to a file
                    if (opts.Output is not null)
                    {
                        if (opts.Format == OutputFormat.Assembly)
                        {
                            logger.LogInformation("Writing generated assembly to {File}", opts.Output.FullName);
                            Directory.CreateDirectory(opts.Output.Directory!.FullName);
                            await File.WriteAllBytesAsync(opts.Output.FullName, outputData);
                        }
                        else
                        {
                            await exporter.WriteAsync(outputData, opts.Output, (SCPU.Assembler.Exporters.OutputFormat)opts.Format);
                        }
                    }

                    // Option "-u" : send the output data to a remote HTTP endpoint
                    if (opts.PostUrl is not null)
                    {
                        var fileName = opts.Output?.Name ?? (opts.File.Name + ".bin");
                        await PostResult(opts.PostUrl, outputData, fileName, logger);
                    }

                    // Option "-p" : print the output data to the console
                    if (opts.Print)
                    {
                        Console.Write(Encoding.UTF8.GetString(outputData));
                    }
                }
                catch (Exception ex)
                {
                    if (ex is not NodeCompilerException && ex is not ParserException)
                    {
                        logger.LogCritical(ex, "Fatal error: {Message}", ex.Message);
                    }
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
