using System.Diagnostics;
using System.Text;

namespace Bannerlord.ModUpdater.Cli
{
    internal class CliExecutor
    {
        private readonly string _baseCommand;

        public CliExecutor(string baseCommand)
        {
            _baseCommand = baseCommand;
        }

        public string ExecuteCommand(string workingDirectory, string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                FileName = _baseCommand,
                Arguments = arguments,
                RedirectStandardOutput = true,
            };

            Console.WriteLine($"{_baseCommand} {arguments}");

            using var process = new Process() { StartInfo = processInfo };

            var outputBuilder = new StringBuilder();
            process.OutputDataReceived += (sender, args) =>
            {
                outputBuilder.AppendLine(args.Data);
                Console.WriteLine(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Command '{_baseCommand} {arguments}' exited with code {process.ExitCode}");
            }

            return outputBuilder.ToString();
        }
    }
}
