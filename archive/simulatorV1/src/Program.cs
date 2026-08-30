namespace SCPU.Simulator.ConsoleUI
{
    using System.CommandLine;
    using Terminal.Gui;

    public class Program
    {
        public static Options Options { get; private set; }

        static void Main(string[] args)
        {
            Console.Title = "S-CPU Simulator";
            Argument<string> fileArgument = new Argument<string>(
                                                                   "file",
                                                                   description: "The file to load",
                                                                   getDefaultValue: () => "none"
                                                                  );

            var rootCommand = new RootCommand() { fileArgument };
            rootCommand.SetHandler(
                                    context =>
                                    {
                                        var options = new Options
                                        {
                                            File = context.ParseResult.GetValueForArgument(fileArgument)
                                        };

                                        Options = options;
                                    }
                                   );
            rootCommand.Invoke(args);

            Application.Run<MainWindow>().Dispose();
            Application.Shutdown();
        }
    }

    public struct Options
    {
        public string File { get; set; }
    }
}
